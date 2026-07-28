using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using DomainPilot.App;
using DomainPilot.Core;
using DomainPilot.Infrastructure;
using Microsoft.Win32;

namespace DomainPilot.Desktop;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly UserProvisioningValidator _validator = new();
    private readonly UserProvisioningCsvImporter _csvImporter = new();
    private readonly PowerShellPlanBuilder _planBuilder = new();
    private readonly UserProvisioningValidationReportBuilder _reportBuilder = new();
    private readonly EnvironmentReadinessService _environmentReadiness = new();
    private readonly ILocalEnvironmentInspector _localEnvironment = new WindowsLocalEnvironmentInspector();
    private readonly IEnvironmentProfileStore _environmentProfileStore = new JsonEnvironmentProfileStore();
    private readonly EnvironmentProfileValidator _environmentProfileValidator = new();
    private readonly DirectoryExplorerService _directoryExplorer = new(new DemoReadOnlyDirectoryGateway());
    private readonly IActiveDirectoryGateway _activeDirectory = new DemoActiveDirectoryGateway();
    private readonly IAuditLogService _auditLog;
    private CancellationTokenSource? _directorySearchCancellation;
    private CancellationTokenSource? _directoryDetailsCancellation;
    private EnvironmentProfile _environmentProfile = new(
        "Local workstation",
        AdministrationMode.Demo,
        string.Empty,
        string.Empty,
        PreferLocalSite: true);
    private LocalEnvironmentSnapshot? _lastLocalSnapshot;
    private string _statusMessage = "Ready. Demo mode uses safe sample data and does not query your domain.";
    private string _lookupSummary = "Search a user to see likely last sign-in device, IP, and support context.";
    private string _importSummary = "Load the bundled example or import a UTF-8 CSV file. No Active Directory actions run during import.";
    private string _environmentStatus = "Demo data only";
    private string _environmentTarget = "Not detected - local checks have not run.";
    private string _environmentNetworkState = "No network discovery has run.";
    private string _discoveryPreviewSummary = "Run local checks first, then preview the exact DNS and directory reads proposed for a future milestone.";
    private DirectoryObjectType _selectedDirectoryObjectType = DirectoryObjectType.All;
    private string _directorySearchSummary = "Search the fictional directory by name, account, description, or distinguished name.";
    private string _directorySourceSummary = "Provider: Demo read-only directory. No domain or network source is connected.";
    private string _directorySelectedObject = "Select a result to inspect its directory attributes.";

    public MainWindow()
    {
        InitializeComponent();
        _auditLog = new InMemoryAuditLogService(OperatorName);
        DataContext = this;
        SeedData();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProvisioningRowViewModel> BulkUsers { get; } = [];

    public ObservableCollection<SetupCheck> SetupChecks { get; } = [];

    public ObservableCollection<DeviceSession> DeviceSessions { get; } = [];

    public ObservableCollection<AdminActionTemplate> ActionTemplates { get; } = [];

    public ObservableCollection<AuditEntry> AuditEvents { get; } = [];

    public ObservableCollection<EnvironmentReadinessResult> EnvironmentChecks { get; } = [];

    public ObservableCollection<DomainDiscoveryPlanStep> DiscoveryPlanSteps { get; } = [];

    public ObservableCollection<DirectoryObjectSummary> DirectorySearchResults { get; } = [];

    public ObservableCollection<DirectoryAttributeValue> DirectoryObjectAttributes { get; } = [];

    public IReadOnlyList<DirectoryObjectType> DirectoryObjectTypes { get; } =
        Enum.GetValues<DirectoryObjectType>();

    public string OperatorName => Environment.UserName;

    public string SafetyMode => $"{_activeDirectory.Mode} mode";

    public string EnvironmentStatus
    {
        get => _environmentStatus;
        set => SetField(ref _environmentStatus, value);
    }

    public string EnvironmentProfileName
    {
        get => _environmentProfile.Name;
        set
        {
            var safeValue = value ?? string.Empty;
            if (_environmentProfile.Name == safeValue)
            {
                return;
            }

            _environmentProfile = _environmentProfile with { Name = safeValue };
            OnPropertyChanged();
        }
    }

    public string EnvironmentProfileMode => _environmentProfile.Mode.ToString();

    public string EnvironmentRouting => _environmentProfile.PreferLocalSite
        ? "Prefer local AD site"
        : "Manual controller";

    public string EnvironmentTarget
    {
        get => _environmentTarget;
        set => SetField(ref _environmentTarget, value);
    }

    public string EnvironmentNetworkState
    {
        get => _environmentNetworkState;
        set => SetField(ref _environmentNetworkState, value);
    }

    public string DiscoveryPreviewSummary
    {
        get => _discoveryPreviewSummary;
        set => SetField(ref _discoveryPreviewSummary, value);
    }

    public DirectoryObjectType SelectedDirectoryObjectType
    {
        get => _selectedDirectoryObjectType;
        set => SetField(ref _selectedDirectoryObjectType, value);
    }

    public string DirectorySearchSummary
    {
        get => _directorySearchSummary;
        set => SetField(ref _directorySearchSummary, value);
    }

    public string DirectorySourceSummary
    {
        get => _directorySourceSummary;
        set => SetField(ref _directorySourceSummary, value);
    }

    public string DirectorySelectedObject
    {
        get => _directorySelectedObject;
        set => SetField(ref _directorySelectedObject, value);
    }

    public int QueuedUserCount => BulkUsers.Count;

    public int ValidUserCount => BulkUsers.Count(user => user.ValidationStatus == "Ready");

    public int ManagedDeviceCount => DeviceSessions.Select(session => session.ComputerName).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    public int AuditEventCount => AuditEvents.Count;

    public string ImportSummary
    {
        get => _importSummary;
        set => SetField(ref _importSummary, value);
    }

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

        Production safety note:
        This public build runs in demo mode. It intentionally does not query your workplace domain, domain controllers, or production computers.
        """;

    private void SeedData()
    {
        LoadBundledSample(writeAudit: false);
        RefreshEnvironmentChecks(_environmentReadiness.CreatePendingResults());
        LoadEnvironmentProfile();

        SetupChecks.Clear();
        SetupChecks.Add(new SetupCheck("Workstation", "RSAT Active Directory module installed", "Verify", "Run the measured local check from the Environment tab."));
        SetupChecks.Add(new SetupCheck("Identity", "Technician account is delegated through role groups", "Required", "Avoids running as Domain Admin for routine account tasks."));
        SetupChecks.Add(new SetupCheck("Servers", "Profile path share exists and has least-privilege ACLs", "Required", "Prevents broken first sign-in and accidental exposure of user data."));
        SetupChecks.Add(new SetupCheck("Policies", "Password, lockout, and workstation logon policies documented", "Required", "Makes restrictions predictable before accounts are created."));
        SetupChecks.Add(new SetupCheck("Logging", "Domain controller sign-in events forwarded or queryable", "Recommended", "Enables last-PC and IP lookup without guessing."));

        RefreshDeviceSessions(_activeDirectory.GetRecentDeviceSessions(string.Empty));

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

        AddAudit("Application started", "Info", "Seeded demo data and dry-run controls.");
        ValidateBulkUsers();
        ScriptPreviewBox.Text = BuildBulkUserScript();
        ActionTemplateGrid.SelectedIndex = 0;
    }

    private void RunLocalChecks_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _lastLocalSnapshot = _localEnvironment.Inspect();
            RefreshEnvironmentChecks(_environmentReadiness.Evaluate(_lastLocalSnapshot));

            var targetDomain = _lastLocalSnapshot.IsDomainJoined
                ? _lastLocalSnapshot.JoinedDomain
                : string.Empty;
            _environmentProfile = _environmentProfile with { DomainName = targetDomain };
            EnvironmentTarget = string.IsNullOrWhiteSpace(targetDomain)
                ? "No joined domain detected."
                : targetDomain;
            EnvironmentNetworkState = "Local checks completed. No DNS, LDAP, Kerberos, event-log, or remote-computer request was sent.";
            EnvironmentStatus = "Local checks only";
            DiscoveryPreviewSummary = "Local checks are complete. Select Preview Domain Discovery to review future network activity; previewing does not execute it.";
            DiscoveryPlanSteps.Clear();

            AddAudit(
                "Ran local environment checks",
                "Info",
                "Inspected local Windows, identity, DNS configuration, domain-join state, and RSAT files without network discovery.");
            StatusMessage = "Local readiness checks completed. No domain controller or remote system was contacted.";
        }
        catch (Exception exception)
        {
            RefreshEnvironmentChecks(
            [
                new EnvironmentReadinessResult(
                    "Local inspection",
                    "Collect workstation readiness",
                    EnvironmentReadinessStatus.Blocked,
                    exception.GetType().Name,
                    "The check failed safely. Review the local audit entry and retry after correcting workstation access.")
            ]);
            EnvironmentNetworkState = "Local inspection stopped safely. No domain discovery was attempted.";
            AddAudit("Local environment check failed", "Error", exception.GetType().Name);
            StatusMessage = "Local checks could not complete. Domain discovery remains unavailable.";
        }
    }

    private void PreviewDomainDiscovery_Click(object sender, RoutedEventArgs e)
    {
        if (_lastLocalSnapshot is null)
        {
            StatusMessage = "Run Local Checks before creating a domain-discovery preview.";
            return;
        }

        var preview = _environmentReadiness.BuildDiscoveryPreview(_environmentProfile, _lastLocalSnapshot);
        DiscoveryPlanSteps.Clear();
        foreach (var step in preview.Steps)
        {
            DiscoveryPlanSteps.Add(step);
        }

        DiscoveryPreviewSummary = preview.Summary;
        EnvironmentNetworkState = "Preview created. No network request was sent and the active mode remains Demo.";
        AddAudit("Previewed domain discovery", "Info", "Displayed a non-executable plan containing no write operation.");
        StatusMessage = "Read-only discovery preview created. Nothing was executed.";
    }

    private void SaveEnvironmentProfile_Click(object sender, RoutedEventArgs e)
    {
        var normalizedProfile = _environmentProfile with
        {
            Name = _environmentProfile.Name.Trim(),
            DomainName = _environmentProfile.DomainName.Trim(),
            PreferredDomainController = _environmentProfile.PreferredDomainController.Trim()
        };
        var issues = _environmentProfileValidator.Validate(normalizedProfile);
        if (issues.Count > 0)
        {
            StatusMessage = string.Join(" ", issues);
            return;
        }

        try
        {
            _environmentProfileStore.Save(normalizedProfile);
            ApplyEnvironmentProfile(normalizedProfile);
            AddAudit("Saved environment profile", "Info", "Saved routing metadata locally without credentials.");
            StatusMessage = "Environment profile saved under your local application data. No credentials or network data were stored.";
        }
        catch (Exception exception)
        {
            AddAudit("Environment profile save failed", "Error", exception.GetType().Name);
            StatusMessage = "The local environment profile could not be saved. Check access to your local application data folder.";
        }
    }

    private async void SearchDirectory_Click(object sender, RoutedEventArgs e)
    {
        _directorySearchCancellation?.Cancel();
        _directorySearchCancellation?.Dispose();
        _directorySearchCancellation = new CancellationTokenSource();

        DirectorySearchResults.Clear();
        DirectoryObjectAttributes.Clear();
        DirectorySelectedObject = "Select a result to inspect its directory attributes.";
        DirectorySearchSummary = "Searching the demo directory...";

        try
        {
            var request = new DirectorySearchRequest(
                DirectorySearchTextBox.Text,
                SelectedDirectoryObjectType,
                MaximumResults: 100);
            var response = await _directoryExplorer.SearchAsync(
                request,
                _directorySearchCancellation.Token);

            foreach (var item in response.Items)
            {
                DirectorySearchResults.Add(item);
            }

            DirectorySearchSummary = response.Items.Count == 0
                ? "No matching fictional directory objects were found."
                : $"{response.Items.Count} result(s) returned in {response.Duration.TotalMilliseconds:0.0} ms"
                    + (response.WasTruncated ? "; result limit reached." : ".");
            DirectorySourceSummary = FormatDirectorySource(response.Source);
            AddAudit(
                "Searched read-only directory",
                "Info",
                $"Demo provider returned {response.Items.Count} {SelectedDirectoryObjectType} result(s).");
            StatusMessage = "Directory search completed against fictional demo data. No domain was queried.";
        }
        catch (ArgumentException exception)
        {
            DirectorySearchSummary = exception.Message;
            StatusMessage = "Directory search was not sent because the request needs correction.";
        }
        catch (TimeoutException exception)
        {
            DirectorySearchSummary = exception.Message;
            AddAudit("Directory search timed out", "Warning", "The read-only provider exceeded its operation limit.");
            StatusMessage = "Directory search timed out safely.";
        }
        catch (OperationCanceledException)
        {
            DirectorySearchSummary = "Directory search cancelled.";
            StatusMessage = "Directory search cancelled.";
        }
        catch (Exception exception)
        {
            DirectorySearchSummary = $"Directory search failed safely: {exception.GetType().Name}.";
            AddAudit("Directory search failed", "Error", exception.GetType().Name);
            StatusMessage = "Directory search failed without changing any environment.";
        }
    }

    private void CancelDirectorySearch_Click(object sender, RoutedEventArgs e)
    {
        _directorySearchCancellation?.Cancel();
        _directoryDetailsCancellation?.Cancel();
        StatusMessage = "Directory operation cancellation requested.";
    }

    private async void DirectoryResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DirectoryResultsGrid.SelectedItem is not DirectoryObjectSummary selected)
        {
            return;
        }

        _directoryDetailsCancellation?.Cancel();
        _directoryDetailsCancellation?.Dispose();
        _directoryDetailsCancellation = new CancellationTokenSource();
        DirectoryObjectAttributes.Clear();
        DirectorySelectedObject = $"Loading {selected.ObjectType}: {selected.Name}...";

        try
        {
            var response = await _directoryExplorer.GetDetailsAsync(
                selected.ObjectId,
                _directoryDetailsCancellation.Token);
            foreach (var attribute in response.Attributes)
            {
                DirectoryObjectAttributes.Add(attribute);
            }

            DirectorySelectedObject = $"{response.Object.ObjectType}: {response.Object.Name} ({response.Object.AccountName})";
            DirectorySourceSummary = FormatDirectorySource(response.Source);
            StatusMessage = "Directory details loaded from fictional demo data.";
        }
        catch (OperationCanceledException)
        {
            DirectorySelectedObject = "Directory detail request cancelled.";
        }
        catch (TimeoutException)
        {
            DirectorySelectedObject = "Directory detail request timed out safely.";
            AddAudit("Directory details timed out", "Warning", "The read-only provider exceeded its operation limit.");
            StatusMessage = "Directory details timed out safely.";
        }
        catch (Exception exception)
        {
            DirectorySelectedObject = $"Details failed safely: {exception.GetType().Name}.";
            AddAudit("Directory details failed", "Error", exception.GetType().Name);
            StatusMessage = "Directory details could not be loaded.";
        }
    }

    private void LoadSampleUsers_Click(object sender, RoutedEventArgs e)
    {
        LoadBundledSample(writeAudit: true);
    }

    private void ImportUsers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import DomainPilot bulk users",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
        {
            StatusMessage = "CSV import cancelled.";
            return;
        }

        ImportCsvFile(dialog.FileName, Path.GetFileName(dialog.FileName), writeAudit: true);
    }

    private void SaveCsvTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save DomainPilot bulk-user CSV template",
            FileName = "domainpilot-bulk-users.csv",
            Filter = "CSV files (*.csv)|*.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            StatusMessage = "CSV template save cancelled.";
            return;
        }

        try
        {
            var templatePath = Path.Combine(AppContext.BaseDirectory, "samples", "bulk-users.template.csv");
            File.Copy(templatePath, dialog.FileName, overwrite: true);
            AddAudit("Saved CSV template", "Info", "Copied the fictional bulk-user template to a technician-selected file.");
            StatusMessage = "CSV template saved. Replace the fictional example values, save the file, and then select Import CSV.";
        }
        catch (IOException exception)
        {
            AddAudit("CSV template save failed", "Error", exception.GetType().Name);
            StatusMessage = "The CSV template could not be saved. Check the destination and try again.";
        }
        catch (UnauthorizedAccessException)
        {
            AddAudit("CSV template save failed", "Error", "Access denied.");
            StatusMessage = "Access to the selected template destination was denied.";
        }
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

    private void ExportValidationReport_Click(object sender, RoutedEventArgs e)
    {
        ValidateBulkUsers();

        var dialog = new SaveFileDialog
        {
            Title = "Export DomainPilot bulk-user review",
            FileName = $"domainpilot-user-review-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            StatusMessage = "Review report export cancelled.";
            return;
        }

        var rows = BulkUsers.Select(user =>
            (user.SourceLine, _validator.Validate(user.ToRequest())));
        File.WriteAllText(dialog.FileName, _reportBuilder.Build(rows));
        AddAudit("Exported bulk-user review", "Info", $"Wrote {BulkUsers.Count} reviewed row(s) to a technician-selected file.");
        StatusMessage = $"Review report exported to {dialog.FileName}.";
    }

    private void LookupUser_Click(object sender, RoutedEventArgs e)
    {
        var query = LookupTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            LookupSummary = "Enter a username before searching.";
            return;
        }

        var matches = _activeDirectory.GetRecentDeviceSessions(query);
        RefreshDeviceSessions(matches);

        LookupSummary = matches.Count == 0
            ? $"No recent demo device sessions found for {query}. Production lookup will require an approved event or inventory source."
            : $"{matches[0].UserName} was most recently seen on {matches[0].ComputerName} at {matches[0].IpAddress}. Source: {matches[0].Source}. Verify with the user before remote access.";

        AddAudit("Looked up user device", "Info", $"Search term '{query}' returned {matches.Count} demo session(s).");
        StatusMessage = "Lookup completed using demo gateway. No domain was queried.";
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
        StatusMessage = "Simulated log scan recorded. No local or domain logs were queried.";
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

        File.WriteAllText(dialog.FileName, _auditLog.ExportCsv());
        AddAudit("Exported audit log", "Info", $"Wrote audit log to {dialog.FileName}.");
        StatusMessage = $"Audit log exported to {dialog.FileName}.";
    }

    private void ValidateBulkUsers()
    {
        foreach (var user in BulkUsers)
        {
            var result = _validator.Validate(user.ToRequest());
            user.ValidationStatus = result.Status;
            user.ValidationMessage = result.Message;
        }

        RefreshCounts();
    }

    private string BuildBulkUserScript()
    {
        var results = BulkUsers.Select(user => _validator.Validate(user.ToRequest()));
        return _planBuilder.BuildBulkUserPlan(results);
    }

    private void LoadBundledSample(bool writeAudit)
    {
        var samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "bulk-users.sample.csv");
        ImportCsvFile(samplePath, "bundled example", writeAudit);
    }

    private void ImportCsvFile(string filePath, string displayName, bool writeAudit)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var result = _csvImporter.Import(stream);

            // A bad schema should not destroy a queue the technician may already have reviewed.
            var hasFileLevelError = result.Issues.Any(issue =>
                issue.Severity == ValidationSeverity.Error && !issue.SourceLine.HasValue);
            if (hasFileLevelError || result.Rows.Count == 0)
            {
                ImportSummary = BuildImportSummary(displayName, result);
                StatusMessage = $"Could not import {displayName}. The existing queue was preserved.";
                if (writeAudit)
                {
                    AddAudit("CSV import rejected", "Warning", $"Rejected {displayName}: {ImportSummary}");
                }

                return;
            }

            BulkUsers.Clear();
            foreach (var row in result.Rows)
            {
                BulkUsers.Add(ProvisioningRowViewModel.FromCsvRow(row));
            }

            ValidateBulkUsers();
            ScriptPreviewBox.Text = BuildBulkUserScript();
            ImportSummary = BuildImportSummary(displayName, result);
            StatusMessage = $"Imported {BulkUsers.Count} row(s) from {displayName}. Review validation notes before generating a plan.";

            if (writeAudit)
            {
                AddAudit("Imported bulk-user CSV", "Info", $"{displayName}: {result.Summary}");
            }
        }
        catch (IOException exception)
        {
            ImportSummary = $"Import failed: {exception.Message}";
            StatusMessage = "The CSV could not be opened. Check that it is not locked and that you have access.";
            if (writeAudit)
            {
                AddAudit("CSV import failed", "Error", $"Could not read {displayName}: {exception.Message}");
            }
        }
        catch (UnauthorizedAccessException exception)
        {
            ImportSummary = $"Import failed: {exception.Message}";
            StatusMessage = "Access to the selected CSV was denied.";
            if (writeAudit)
            {
                AddAudit("CSV import failed", "Error", $"Access denied for {displayName}.");
            }
        }
        catch (Exception exception)
        {
            // File parsing is an operator-facing boundary; unexpected input must fail closed without closing the app.
            ImportSummary = $"Import failed safely: {exception.Message}";
            StatusMessage = "DomainPilot could not parse the selected CSV. The existing queue was preserved.";
            if (writeAudit)
            {
                AddAudit("CSV import failed", "Error", $"Unexpected parser error for {displayName}: {exception.GetType().Name}.");
            }
        }
    }

    private static string BuildImportSummary(string displayName, UserProvisioningCsvImportResult result)
    {
        var details = result.Issues
            .Take(3)
            .Select(issue => issue.SourceLine.HasValue
                ? $"Row {issue.SourceLine}: {issue.Message}"
                : issue.Message);
        var suffix = result.Issues.Count > 3
            ? $" Plus {result.Issues.Count - 3} more issue(s)."
            : string.Empty;

        return $"{displayName}: {result.Summary} {string.Join(" ", details)}{suffix}".Trim();
    }

    private void AddAudit(string action, string severity, string message)
    {
        _auditLog.Add(action, severity, message);
        AuditEvents.Clear();

        foreach (var entry in _auditLog.Entries)
        {
            AuditEvents.Add(entry);
        }

        RefreshCounts();
    }

    private void RefreshDeviceSessions(IEnumerable<DeviceSession> sessions)
    {
        DeviceSessions.Clear();
        foreach (var session in sessions)
        {
            DeviceSessions.Add(session);
        }

        RefreshCounts();
    }

    private void RefreshEnvironmentChecks(IEnumerable<EnvironmentReadinessResult> results)
    {
        EnvironmentChecks.Clear();
        foreach (var result in results)
        {
            EnvironmentChecks.Add(result);
        }
    }

    private void LoadEnvironmentProfile()
    {
        try
        {
            var savedProfile = _environmentProfileStore.Load();
            if (savedProfile is null)
            {
                return;
            }

            ApplyEnvironmentProfile(savedProfile);
            EnvironmentNetworkState = "Credential-free local profile loaded. No network discovery has run.";
            AddAudit("Loaded environment profile", "Info", "Loaded local routing metadata without credentials.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            EnvironmentNetworkState = "Saved profile could not be loaded. DomainPilot remains in Demo mode with no network activity.";
            AddAudit("Environment profile load failed", "Warning", exception.GetType().Name);
        }
    }

    private void ApplyEnvironmentProfile(EnvironmentProfile profile)
    {
        _environmentProfile = profile;
        EnvironmentTarget = string.IsNullOrWhiteSpace(profile.DomainName)
            ? "Not detected - run local checks."
            : profile.DomainName;
        OnPropertyChanged(nameof(EnvironmentProfileName));
        OnPropertyChanged(nameof(EnvironmentProfileMode));
        OnPropertyChanged(nameof(EnvironmentRouting));
    }

    private static string FormatDirectorySource(DirectoryDataSource source)
    {
        var dataKind = source.IsSynthetic ? "fictional data" : "read-only environment data";
        return $"{source.Provider} | {source.Environment} | {source.Server} | {source.Mode} | {dataKind} | {source.RetrievedAt:yyyy-MM-dd HH:mm:ss zzz}";
    }

    protected override void OnClosed(EventArgs e)
    {
        _directorySearchCancellation?.Cancel();
        _directorySearchCancellation?.Dispose();
        _directoryDetailsCancellation?.Cancel();
        _directoryDetailsCancellation?.Dispose();
        base.OnClosed(e);
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(QueuedUserCount));
        OnPropertyChanged(nameof(ValidUserCount));
        OnPropertyChanged(nameof(ManagedDeviceCount));
        OnPropertyChanged(nameof(AuditEventCount));
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

public sealed class ProvisioningRowViewModel : INotifyPropertyChanged
{
    private string _validationStatus = "Pending";
    private string _validationMessage = "Not yet validated.";

    public ProvisioningRowViewModel(long sourceLine, string samAccountName, string firstName, string lastName, string organizationalUnit, string groups, string profilePath, string allowedWorkstations)
    {
        SourceLine = sourceLine;
        SamAccountName = samAccountName;
        FirstName = firstName;
        LastName = lastName;
        OrganizationalUnit = organizationalUnit;
        Groups = groups;
        ProfilePath = profilePath;
        AllowedWorkstations = allowedWorkstations;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public long SourceLine { get; }

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

    public UserProvisioningRequest ToRequest()
    {
        return new UserProvisioningRequest(SamAccountName, FirstName, LastName, OrganizationalUnit, Groups, ProfilePath, AllowedWorkstations);
    }

    public static ProvisioningRowViewModel FromCsvRow(UserProvisioningCsvRow row)
    {
        var request = row.Request;
        return new ProvisioningRowViewModel(
            row.SourceLine,
            request.SamAccountName,
            request.FirstName,
            request.LastName,
            request.OrganizationalUnit,
            request.Groups,
            request.ProfilePath,
            request.AllowedWorkstations);
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
