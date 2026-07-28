using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DomainPilot.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly Regex _samAccountNamePattern = new("^[a-z][a-z0-9._-]{2,19}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private string _statusMessage = "Ready. Prototype uses dry-run generation and audit logging.";
    private string _lookupSummary = "Search a user to see likely last sign-in device, IP, and support context.";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        SeedData();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UserProvisioningRequest> BulkUsers { get; } = [];

    public ObservableCollection<SetupCheck> SetupChecks { get; } = [];

    public ObservableCollection<DeviceSession> DeviceSessions { get; } = [];

    public ObservableCollection<AdminActionTemplate> ActionTemplates { get; } = [];

    public ObservableCollection<AuditEntry> AuditEvents { get; } = [];

    public string OperatorName => Environment.UserName;

    public string SafetyMode => "Dry-run enforced";

    public string EnvironmentStatus => "Lab profile active";

    public int QueuedUserCount => BulkUsers.Count;

    public int ValidUserCount => BulkUsers.Count(user => user.ValidationStatus == "Ready");

    public int ManagedDeviceCount => DeviceSessions.Select(session => session.ComputerName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public int AuditEventCount => AuditEvents.Count;

    public string LookupSummary
    {
        get => _lookupSummary;
        set => SetField(ref _lookupSummary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string TrainingGuide { get; } = """
        DomainPilot is designed for technician clarity:

        1. Confirm RSAT Active Directory tools are installed on the admin workstation.
        2. Run the console as a delegated admin account, not a daily driver account.
        3. Validate OU paths, security groups, profile shares, and workstation restrictions before generating actions.
        4. Preview generated PowerShell. A second reviewer should approve bulk changes in production.
        5. Export audit logs after every batch so each account, device, and policy action is traceable.

        Production expansion ideas:
        - Add signed script packages and block unsigned custom actions.
        - Connect last-PC lookup to domain controller 4624 events, endpoint management inventory, or SIEM data.
        - Store technician roles in AD groups and enforce least privilege inside the app.
        - Require change-ticket IDs for destructive or privileged actions.
        """;

    private void SeedData()
    {
        BulkUsers.Clear();
        BulkUsers.Add(new UserProvisioningRequest("jmartinez", "Jordan", "Martinez", "OU=HelpDesk,OU=Users,DC=corp,DC=example,DC=com", "GG-VPN;GG-HelpDesk", @"\\files01\profiles\jmartinez", "HD-PC-014;HD-PC-019"));
        BulkUsers.Add(new UserProvisioningRequest("akhan", "Avery", "Khan", "OU=Finance,OU=Users,DC=corp,DC=example,DC=com", "GG-FinanceApps;GG-MFA-Enforced", @"\\files01\profiles\akhan", "FIN-PC-022"));
        BulkUsers.Add(new UserProvisioningRequest("temp.user", "Temp", "User", "Users", "Domain Admins", "C:\\Profiles\\temp.user", ""));

        SetupChecks.Clear();
        SetupChecks.Add(new SetupCheck("Workstation", "RSAT Active Directory module installed", "Planned", "Needed for Get-ADUser, New-ADUser, and group membership checks."));
        SetupChecks.Add(new SetupCheck("Identity", "Technician account is delegated through role groups", "Required", "Avoids running as Domain Admin for routine account tasks."));
        SetupChecks.Add(new SetupCheck("Servers", "Profile path share exists and has least-privilege ACLs", "Required", "Prevents broken first sign-in and accidental exposure of user data."));
        SetupChecks.Add(new SetupCheck("Policies", "Password, lockout, and workstation logon policies documented", "Required", "Makes restrictions predictable before accounts are created."));
        SetupChecks.Add(new SetupCheck("Logging", "Domain controller sign-in events forwarded or queryable", "Recommended", "Enables last-PC and IP lookup without guessing."));

        DeviceSessions.Clear();
        DeviceSessions.Add(new DeviceSession("jmartinez", "HD-PC-014", "10.34.18.42", DateTime.Now.AddMinutes(-38), "Security Event 4624"));
        DeviceSessions.Add(new DeviceSession("akhan", "FIN-PC-022", "10.20.44.91", DateTime.Now.AddHours(-2), "Endpoint inventory sync"));
        DeviceSessions.Add(new DeviceSession("jmartinez", "HD-PC-019", "10.34.18.57", DateTime.Now.AddDays(-1), "Security Event 4624"));

        ActionTemplates.Clear();
        ActionTemplates.Add(new AdminActionTemplate(
            "Create bulk users",
            "Medium",
            "Account Operators",
            "Creates staged users with profile paths, group memberships, workstation restrictions, and forced password reset.",
            BuildBulkUserScript()));
        ActionTemplates.Add(new AdminActionTemplate(
            "Disable stale account",
            "High",
            "Identity Admin",
            "Disables account, removes risky groups, records ticket ID, and moves object to a disabled-users OU.",
            "Disable-ADAccount -Identity '<samAccountName>' -WhatIf\nMove-ADObject -Identity '<distinguishedName>' -TargetPath 'OU=Disabled,DC=corp,DC=example,DC=com' -WhatIf"));
        ActionTemplates.Add(new AdminActionTemplate(
            "Apply workstation restriction",
            "Medium",
            "Help Desk Lead",
            "Limits interactive logon to approved computers for sensitive or shared-role accounts.",
            "Set-ADUser -Identity '<samAccountName>' -LogonWorkstations '<PC01,PC02>' -WhatIf"));
        ActionTemplates.Add(new AdminActionTemplate(
            "Collect remote support context",
            "Low",
            "Help Desk",
            "Pulls device, IP, lockout, and recent sign-in indicators before a technician remotes into a PC.",
            "Get-ADUser '<samAccountName>' -Properties LastLogonDate,LockedOut,PasswordExpired\nGet-WinEvent -FilterHashtable @{LogName='Security'; Id=4624} -MaxEvents 50"));

        AddAudit("Application started", "Info", "Seeded prototype data and dry-run controls.");
        ValidateBulkUsers();
        ScriptPreviewBox.Text = BuildBulkUserScript();
        ActionTemplateGrid.SelectedIndex = 0;
    }

    private void LoadSampleUsers_Click(object sender, RoutedEventArgs e)
    {
        SeedData();
        AddAudit("Loaded sample CSV", "Info", "Sample provisioning rows loaded for validation training.");
        StatusMessage = "Sample CSV data loaded. Edit the grid or validate the rows.";
    }

    private void ValidateBulkUsers_Click(object sender, RoutedEventArgs e)
    {
        ValidateBulkUsers();
        AddAudit("Validated bulk users", "Info", $"{ValidUserCount} of {QueuedUserCount} rows are ready.");
        StatusMessage = $"{ValidUserCount} ready row(s), {QueuedUserCount - ValidUserCount} row(s) need review.";
    }

    private void GenerateBulkScript_Click(object sender, RoutedEventArgs e)
    {
        ValidateBulkUsers();
        ScriptPreviewBox.Text = BuildBulkUserScript();
        AddAudit("Generated PowerShell plan", "Info", "Created dry-run provisioning script preview with -WhatIf.");
        StatusMessage = "Generated a safe PowerShell plan. Review it before adapting for production.";
    }

    private void LookupUser_Click(object sender, RoutedEventArgs e)
    {
        var query = LookupTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            LookupSummary = "Enter a username before searching.";
            return;
        }

        var matches = DeviceSessions
            .Where(session => session.UserName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(session => session.LastSeen)
            .ToList();

        LookupSummary = matches.Count == 0
            ? $"No recent device sessions found for {query}. Check log forwarding, endpoint inventory, and spelling."
            : $"{matches[0].UserName} was most recently seen on {matches[0].ComputerName} at {matches[0].IpAddress}. Source: {matches[0].Source}. Verify with the user before remote access.";

        AddAudit("Looked up user device", "Info", $"Search term '{query}' returned {matches.Count} session(s).");
        StatusMessage = "Lookup completed with explicit source context.";
    }

    private void ActionTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionTemplateGrid.SelectedItem is not AdminActionTemplate template)
        {
            return;
        }

        ActionPreviewBox.Text = template.ScriptPreview;
        StatusMessage = $"Selected action: {template.Name}. Risk: {template.RiskLevel}.";
    }

    private void ScanWindowsLogs_Click(object sender, RoutedEventArgs e)
    {
        AddAudit("Simulated Windows log scan", "Info", "Would query Security 4624, 4740, 4720, and 4732 events from approved sources.");
        AddAudit("Training reminder", "Warning", "Ensure event forwarding or SIEM ingestion is configured before relying on last-PC data.");
        StatusMessage = "Simulated log scan recorded. Production build should use approved event sources.";
    }

    private void ExportAuditLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export DomainPilot audit log",
            FileName = $"domainpilot-audit-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            StatusMessage = "Audit export cancelled.";
            return;
        }

        File.WriteAllText(dialog.FileName, BuildAuditCsv(), Encoding.UTF8);
        AddAudit("Exported audit log", "Info", $"Wrote audit log to {dialog.FileName}.");
        StatusMessage = $"Audit log exported to {dialog.FileName}.";
    }

    private void ValidateBulkUsers()
    {
        foreach (var user in BulkUsers)
        {
            var issues = new List<string>();

            if (!_samAccountNamePattern.IsMatch(user.SamAccountName))
            {
                issues.Add("Username must be 3-20 safe characters and start with a letter.");
            }

            if (!user.OrganizationalUnit.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("Use a full distinguished OU path.");
            }

            if (user.ProfilePath.Length > 0 && !user.ProfilePath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                issues.Add("Profile path should be a UNC path.");
            }

            if (user.Groups.Contains("Domain Admins", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("Privileged groups require separate approval.");
            }

            if (string.IsNullOrWhiteSpace(user.Groups))
            {
                issues.Add("At least one approved role group is required.");
            }

            user.ValidationStatus = issues.Count == 0 ? "Ready" : "Review";
            user.ValidationMessage = issues.Count == 0 ? "Safe to include in dry-run plan." : string.Join(" ", issues);
        }

        RefreshCounts();
    }

    private string BuildBulkUserScript()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# DomainPilot generated dry-run plan");
        builder.AppendLine("# Review with a second technician before removing -WhatIf.");
        builder.AppendLine("Import-Module ActiveDirectory");
        builder.AppendLine("$TemporaryPassword = Read-Host 'Temporary password' -AsSecureString");
        builder.AppendLine();

        foreach (var user in BulkUsers.Where(item => item.ValidationStatus == "Ready"))
        {
            var displayName = $"{user.FirstName} {user.LastName}";
            builder.AppendLine($"# {displayName} ({user.SamAccountName})");
            builder.AppendLine($"New-ADUser -SamAccountName '{Escape(user.SamAccountName)}' -Name '{Escape(displayName)}' -GivenName '{Escape(user.FirstName)}' -Surname '{Escape(user.LastName)}' -Path '{Escape(user.OrganizationalUnit)}' -AccountPassword $TemporaryPassword -Enabled $true -ChangePasswordAtLogon $true -ProfilePath '{Escape(user.ProfilePath)}' -WhatIf");

            foreach (var group in SplitMultiValue(user.Groups))
            {
                builder.AppendLine($"Add-ADGroupMember -Identity '{Escape(group)}' -Members '{Escape(user.SamAccountName)}' -WhatIf");
            }

            if (!string.IsNullOrWhiteSpace(user.AllowedWorkstations))
            {
                var workstations = string.Join(",", SplitMultiValue(user.AllowedWorkstations));
                builder.AppendLine($"Set-ADUser -Identity '{Escape(user.SamAccountName)}' -LogonWorkstations '{Escape(workstations)}' -WhatIf");
            }

            builder.AppendLine();
        }

        if (BulkUsers.All(item => item.ValidationStatus != "Ready"))
        {
            builder.AppendLine("# No rows are currently ready. Validate and fix review notes first.");
        }

        return builder.ToString();
    }

    private string BuildAuditCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,Actor,Action,Severity,Message");
        foreach (var entry in AuditEvents)
        {
            builder.AppendLine($"{Csv(entry.Timestamp.ToString("O"))},{Csv(entry.Actor)},{Csv(entry.Action)},{Csv(entry.Severity)},{Csv(entry.Message)}");
        }

        return builder.ToString();
    }

    private void AddAudit(string action, string severity, string message)
    {
        AuditEvents.Insert(0, new AuditEntry(DateTime.Now, OperatorName, action, severity, message));
        RefreshCounts();
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(QueuedUserCount));
        OnPropertyChanged(nameof(ValidUserCount));
        OnPropertyChanged(nameof(ManagedDeviceCount));
        OnPropertyChanged(nameof(AuditEventCount));
    }

    private static IEnumerable<string> SplitMultiValue(string value)
    {
        return value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Escape(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class UserProvisioningRequest : INotifyPropertyChanged
{
    private string _validationStatus = "Pending";
    private string _validationMessage = "Not yet validated.";

    public UserProvisioningRequest(string samAccountName, string firstName, string lastName, string organizationalUnit, string groups, string profilePath, string allowedWorkstations)
    {
        SamAccountName = samAccountName;
        FirstName = firstName;
        LastName = lastName;
        OrganizationalUnit = organizationalUnit;
        Groups = groups;
        ProfilePath = profilePath;
        AllowedWorkstations = allowedWorkstations;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string SamAccountName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string OrganizationalUnit { get; set; }

    public string Groups { get; set; }

    public string ProfilePath { get; set; }

    public string AllowedWorkstations { get; set; }

    public string ValidationStatus
    {
        get => _validationStatus;
        set => SetField(ref _validationStatus, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetField(ref _validationMessage, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record SetupCheck(string Area, string Requirement, string Status, string HelpText);

public sealed record DeviceSession(string UserName, string ComputerName, string IpAddress, DateTime LastSeen, string Source);

public sealed record AdminActionTemplate(string Name, string RiskLevel, string RequiredRole, string Description, string ScriptPreview);

public sealed record AuditEntry(DateTime Timestamp, string Actor, string Action, string Severity, string Message);
