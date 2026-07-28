using DomainPilot.App;
using DomainPilot.Core;
using DomainPilot.Infrastructure;
using System.Xml.Linq;

var validator = new UserProvisioningValidator();
var csvImporter = new UserProvisioningCsvImporter();
var planBuilder = new PowerShellPlanBuilder();
var reportBuilder = new UserProvisioningValidationReportBuilder();
var environmentReadiness = new EnvironmentReadinessService();
var demoDirectory = new DemoReadOnlyDirectoryGateway();
var directoryExplorer = new DirectoryExplorerService(demoDirectory);
var provisioningPreflight = new BulkProvisioningPreflightService(demoDirectory, validator);
var approvalPackageBuilder = new ProvisioningApprovalPackageBuilder();
var failures = new List<string>();

Assert("valid row is ready", () =>
{
    var result = validator.Validate(ValidRequest());
    return result.IsReady && result.Status == "Ready";
});

Assert("bad OU requires review", () =>
{
    var request = ValidRequest() with { OrganizationalUnit = "Users" };
    var result = validator.Validate(request);
    return !result.IsReady && result.Message.Contains("distinguished OU path", StringComparison.OrdinalIgnoreCase);
});

Assert("local profile path is rejected", () =>
{
    var request = ValidRequest() with { ProfilePath = "C:\\Profiles\\jmartinez" };
    var result = validator.Validate(request);
    return !result.IsReady && result.Message.Contains("UNC path", StringComparison.OrdinalIgnoreCase);
});

Assert("privileged groups are blocked", () =>
{
    var request = ValidRequest() with { Groups = "Domain Admins" };
    var result = validator.Validate(request);
    return !result.IsReady && result.Message.Contains("blocked from bulk workflows", StringComparison.OrdinalIgnoreCase);
});

Assert("missing person name requires review", () =>
{
    var request = ValidRequest() with { FirstName = string.Empty };
    var result = validator.Validate(request);
    return !result.IsReady && result.Message.Contains("First name is required", StringComparison.OrdinalIgnoreCase);
});

Assert("generated provisioning plan remains dry-run", () =>
{
    var result = validator.Validate(ValidRequest());
    var script = planBuilder.BuildBulkUserPlan([result]);
    return script.Contains("New-ADUser", StringComparison.Ordinal)
        && script.Contains("-WhatIf", StringComparison.Ordinal)
        && !script.Contains("Domain Admins", StringComparison.OrdinalIgnoreCase);
});

Assert("CSV import handles quoted distinguished names", () =>
{
    const string csv = """
        SamAccountName,FirstName,LastName,OrganizationalUnit,Groups,ProfilePath,AllowedWorkstations
        jmartinez,Jordan,Martinez,"OU=HelpDesk,OU=Users,DC=corp,DC=example,DC=com","GG-VPN;GG-HelpDesk","\\files01\profiles\jmartinez","HD-PC-014;HD-PC-019"
        """;
    using var stream = CsvStream(csv);
    var result = csvImporter.Import(stream);
    return result.Rows.Count == 1
        && result.Rows[0].SourceLine == 2
        && result.Rows[0].Request.OrganizationalUnit.Contains("DC=example", StringComparison.Ordinal);
});

Assert("CSV import rejects a missing required header", () =>
{
    const string csv = """
        SamAccountName,FirstName,LastName,Groups,ProfilePath,AllowedWorkstations
        jmartinez,Jordan,Martinez,GG-VPN,\\files01\profiles\jmartinez,HD-PC-014
        """;
    using var stream = CsvStream(csv);
    var result = csvImporter.Import(stream);
    return result.Rows.Count == 0
        && result.Issues.Any(issue =>
            issue.Field == "OrganizationalUnit"
            && issue.Severity == ValidationSeverity.Error);
});

Assert("CSV import skips rows with the wrong column count", () =>
{
    const string csv = """
        SamAccountName,FirstName,LastName,OrganizationalUnit,Groups,ProfilePath,AllowedWorkstations
        jmartinez,Jordan
        """;
    using var stream = CsvStream(csv);
    var result = csvImporter.Import(stream);
    return result.Rows.Count == 0
        && result.Issues.Any(issue => issue.Message.Contains("row was skipped", StringComparison.OrdinalIgnoreCase));
});

Assert("packaged CSV template is immediately importable", () =>
{
    var templatePath = Path.Combine(AppContext.BaseDirectory, "samples", "bulk-users.template.csv");
    using var stream = File.OpenRead(templatePath);
    var result = csvImporter.Import(stream);
    return result.Rows.Count == 1
        && !result.HasErrors
        && validator.Validate(result.Rows[0].Request).IsReady;
});

Assert("validation report neutralizes CSV formula-like values", () =>
{
    var request = ValidRequest() with { FirstName = "=Example" };
    var result = validator.Validate(request);
    var report = reportBuilder.Build([(2, result)]);
    return report.Contains("\"'=Example\"", StringComparison.Ordinal)
        && report.Contains("\"Safe to include in dry-run plan.\"", StringComparison.Ordinal);
});

Assert("audit report neutralizes CSV formula-like values", () =>
{
    var auditLog = new InMemoryAuditLogService("=operator");
    auditLog.Add("+action", "Info", "@message");
    var report = auditLog.ExportCsv();
    return report.Contains("\"'=operator\"", StringComparison.Ordinal)
        && report.Contains("\"'+action\"", StringComparison.Ordinal)
        && report.Contains("\"'@message\"", StringComparison.Ordinal);
});

Assert("read-only text boxes do not write to view-model properties", () =>
{
    var xamlPath = Path.Combine(AppContext.BaseDirectory, "ui", "MainWindow.xaml");
    var document = XDocument.Load(xamlPath);
    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    return document
        .Descendants(presentation + "TextBox")
        .Where(element => string.Equals((string?)element.Attribute("IsReadOnly"), "True", StringComparison.OrdinalIgnoreCase))
        .Select(element => (string?)element.Attribute("Text"))
        .Where(text => text?.Contains("{Binding", StringComparison.Ordinal) == true)
        .All(text => text!.Contains("Mode=OneWay", StringComparison.Ordinal));
});

Assert("display-only grids are explicitly read-only", () =>
{
    var xamlPath = Path.Combine(AppContext.BaseDirectory, "ui", "MainWindow.xaml");
    var document = XDocument.Load(xamlPath);
    XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    var editableGrid = document
        .Descendants(presentation + "DataGrid")
        .Single(element => string.Equals((string?)element.Attribute("ItemsSource"), "{Binding BulkUsers}", StringComparison.Ordinal));

    return document
        .Descendants(presentation + "DataGrid")
        .Where(element => element != editableGrid)
        .All(element => string.Equals((string?)element.Attribute("IsReadOnly"), "True", StringComparison.OrdinalIgnoreCase));
});

Assert("local readiness reports domain and RSAT evidence without network activity", () =>
{
    var snapshot = ExampleEnvironmentSnapshot();
    var results = environmentReadiness.Evaluate(snapshot);
    return results.Any(result =>
            result.Area == "Identity"
            && result.Status == EnvironmentReadinessStatus.Pass)
        && results.Any(result =>
            result.Area == "Tools"
            && result.Status == EnvironmentReadinessStatus.Pass)
        && results.Any(result =>
            result.Area == "Safety"
            && result.Status == EnvironmentReadinessStatus.Pass);
});

Assert("domain discovery preview cannot modify an environment", () =>
{
    var profile = new EnvironmentProfile(
        "Test profile",
        AdministrationMode.Demo,
        "corp.example.com",
        string.Empty,
        PreferLocalSite: true);
    var preview = environmentReadiness.BuildDiscoveryPreview(profile, ExampleEnvironmentSnapshot());

    return !preview.ContainsWrites
        && preview.Steps.Count == 5
        && preview.Steps.Any(step => step.Source == "DNS" && step.IsNetworkActivity)
        && preview.Steps.Any(step => step.Source == "Selected domain controller" && step.IsNetworkActivity)
        && preview.Steps.All(step => !step.CanModifyEnvironment);
});

Assert("Windows local inspector declares that it performed no network activity", () =>
{
    var snapshot = new WindowsLocalEnvironmentInspector().Inspect();
    return !snapshot.NetworkActivityPerformed
        && !string.IsNullOrWhiteSpace(snapshot.MachineName)
        && !string.IsNullOrWhiteSpace(snapshot.CurrentIdentity);
});

Assert("demo directory searches users and reports a synthetic source", () =>
{
    var response = directoryExplorer
        .SearchAsync(
            new DirectorySearchRequest("martinez", DirectoryObjectType.User, 100),
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    return response.Items.Count == 1
        && response.Items[0].AccountName == "jmartinez"
        && response.Source.IsSynthetic
        && response.Source.Mode == AdministrationMode.Demo;
});

Assert("demo directory returns categorized object details", () =>
{
    var details = directoryExplorer
        .GetDetailsAsync("demo-computer-helpdesk14", CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    return details.Object.ObjectType == DirectoryObjectType.Computer
        && details.Attributes.Any(attribute =>
            attribute.Category == "Operating system"
            && attribute.Name == "Name")
        && details.Attributes.Any(attribute =>
            attribute.Name == "Last known IPv4");
});

Assert("packaged CSV template passes fictional directory preflight", () =>
{
    var templatePath = Path.Combine(AppContext.BaseDirectory, "samples", "bulk-users.template.csv");
    using var stream = File.OpenRead(templatePath);
    var imported = csvImporter.Import(stream);
    var result = provisioningPreflight
        .RunAsync(
            imported.Rows.Select(row => new ProvisioningInputRow(row.SourceLine, row.Request)),
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    return result.Rows.Count == 1
        && result.Rows[0].IsReady
        && result.Source.IsSynthetic
        && result.ReadyCount == 1;
});

Assert("provisioning preflight blocks an existing account", () =>
{
    var result = provisioningPreflight
        .RunAsync(
            [new ProvisioningInputRow(2, ValidRequest())],
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    return !result.Rows[0].IsReady
        && result.Rows[0].Issues.Any(issue =>
            issue.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase));
});

Assert("provisioning preflight identifies every missing directory reference", () =>
{
    var request = TemplateRequest() with
    {
        SamAccountName = "new.user",
        OrganizationalUnit = "OU=Missing,OU=Users,DC=corp,DC=example,DC=com",
        Groups = "GG-Missing",
        AllowedWorkstations = "PC-MISSING-001"
    };
    var result = provisioningPreflight
        .RunAsync(
            [new ProvisioningInputRow(2, request)],
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    var message = result.Rows[0].Message;

    return !result.Rows[0].IsReady
        && message.Contains("target OU", StringComparison.OrdinalIgnoreCase)
        && message.Contains("GG-Missing", StringComparison.Ordinal)
        && message.Contains("PC-MISSING-001", StringComparison.Ordinal);
});

Assert("provisioning preflight detects duplicate usernames with one provider call", () =>
{
    var gateway = new RecordingProvisioningReferenceGateway();
    var service = new BulkProvisioningPreflightService(gateway, validator);
    var first = TemplateRequest() with { SamAccountName = "duplicate.user" };
    var second = TemplateRequest() with { SamAccountName = "DUPLICATE.USER" };
    var result = service
        .RunAsync(
            [
                new ProvisioningInputRow(2, first),
                new ProvisioningInputRow(3, second)
            ],
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();

    return gateway.CallCount == 1
        && gateway.LastRequest?.AccountNames.Count == 1
        && result.Rows.All(row =>
            !row.IsReady
            && row.Message.Contains("more than once", StringComparison.OrdinalIgnoreCase));
});

Assert("provisioning preflight converts provider cancellation into a timeout", () =>
{
    var service = new BulkProvisioningPreflightService(
        new SlowProvisioningReferenceGateway(),
        validator,
        TimeSpan.FromMilliseconds(20));
    try
    {
        service
            .RunAsync(
                [new ProvisioningInputRow(2, TemplateRequest())],
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return false;
    }
    catch (TimeoutException)
    {
        return true;
    }
});

Assert("approval package contains dry-run evidence and no embedded secret", () =>
{
    var result = provisioningPreflight
        .RunAsync(
            [new ProvisioningInputRow(2, TemplateRequest())],
            CancellationToken.None)
        .GetAwaiter()
        .GetResult();
    var validations = result.Rows.Select(row =>
        new UserProvisioningValidationResult(row.Request, row.Issues));
    var script = planBuilder.BuildBulkUserPlan(validations);
    var package = approvalPackageBuilder.Build(result, script);

    return package.Contains("\"rollbackGuidance\"", StringComparison.Ordinal)
        && package.Contains("\"batchId\"", StringComparison.Ordinal)
        && package.Contains("-WhatIf", StringComparison.Ordinal)
        && package.Contains("dry-run review artifact", StringComparison.OrdinalIgnoreCase)
        && !package.Contains("Secret123!", StringComparison.Ordinal);
});

Assert("directory explorer blocks one-character broad searches", () =>
{
    try
    {
        directoryExplorer
            .SearchAsync(
                new DirectorySearchRequest("a", DirectoryObjectType.All, 100),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return false;
    }
    catch (ArgumentException exception)
    {
        return exception.Message.Contains("at least two characters", StringComparison.OrdinalIgnoreCase);
    }
});

Assert("directory explorer converts provider cancellation into a timeout", () =>
{
    var slowExplorer = new DirectoryExplorerService(
        new SlowReadOnlyDirectoryGateway(),
        TimeSpan.FromMilliseconds(20));
    try
    {
        slowExplorer
            .SearchAsync(
                new DirectorySearchRequest("test", DirectoryObjectType.All, 100),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        return false;
    }
    catch (TimeoutException)
    {
        return true;
    }
});

Assert("LDAP filter values escape every RFC 4515 special character", () =>
{
    var escaped = LdapFilterValueEscaper.Escape("a*(b)\\c\0");
    return escaped == "a\\2a\\28b\\29\\5cc\\00";
});

Assert("environment profile store round-trips routing data without credentials", () =>
{
    var testDirectory = Path.Combine(Path.GetTempPath(), $"DomainPilot.Tests-{Guid.NewGuid():N}");
    var profilePath = Path.Combine(testDirectory, "profile.json");
    Directory.CreateDirectory(testDirectory);

    try
    {
        var store = new JsonEnvironmentProfileStore(profilePath);
        var profile = new EnvironmentProfile(
            "Test workstation",
            AdministrationMode.Demo,
            "corp.example.com",
            "dc01.corp.example.com",
            PreferLocalSite: true);
        store.Save(profile);
        var loaded = store.Load();
        var json = File.ReadAllText(profilePath);

        return loaded == profile
            && !json.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("credential", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("token", StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory);
        }
    }
});

Assert("DC discovery policy requires explicit operator approval", () =>
{
    var policy = new DomainControllerDiscoveryPolicy();
    var blocked = policy.Validate(new DomainControllerDiscoveryRequest(
        "corp.example.com",
        PreferLocalSite: true,
        OperatorApproved: false));
    var approved = policy.Validate(new DomainControllerDiscoveryRequest(
        "corp.example.com",
        PreferLocalSite: true,
        OperatorApproved: true));

    return blocked.Any(issue => issue.Contains("approve", StringComparison.OrdinalIgnoreCase))
        && approved.Count == 0;
});

if (failures.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("DomainPilot validation tests failed:");
    foreach (var failure in failures)
    {
        Console.WriteLine($"- {failure}");
    }

    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("DomainPilot validation tests passed.");
Console.ResetColor();
return 0;

void Assert(string name, Func<bool> test)
{
    try
    {
        if (!test())
        {
            failures.Add(name);
        }
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
    }
}

static UserProvisioningRequest ValidRequest()
{
    return new UserProvisioningRequest(
        "jmartinez",
        "Jordan",
        "Martinez",
        "OU=HelpDesk,OU=Users,DC=corp,DC=example,DC=com",
        "GG-VPN;GG-HelpDesk",
        @"\\files01\profiles\jmartinez",
        "HD-PC-014;HD-PC-019");
}

static UserProvisioningRequest TemplateRequest()
{
    return new UserProvisioningRequest(
        "sample.user",
        "Sample",
        "User",
        "OU=Staff,OU=Users,DC=corp,DC=example,DC=com",
        "GG-Standard-Users",
        @"\\files01\profiles\sample.user",
        "PC-DEMO-001");
}

static MemoryStream CsvStream(string content)
{
    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
}

static LocalEnvironmentSnapshot ExampleEnvironmentSnapshot()
{
    return new LocalEnvironmentSnapshot(
        DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
        "Microsoft Windows",
        IsWindows: true,
        "TECH-PC-001",
        "CORP\\technician",
        IsDomainJoined: true,
        "corp.example.com",
        "corp.example.com",
        IsActiveDirectoryModuleInstalled: true,
        @"C:\Windows\System32\WindowsPowerShell\v1.0\Modules\ActiveDirectory\ActiveDirectory.psd1",
        NetworkActivityPerformed: false);
}

sealed class SlowReadOnlyDirectoryGateway : IReadOnlyDirectoryGateway
{
    public AdministrationMode Mode => AdministrationMode.Demo;

    public async Task<DirectorySearchResponse> SearchAsync(
        DirectorySearchRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        throw new InvalidOperationException("The timeout test should cancel before this line.");
    }

    public Task<DirectoryObjectDetailsResponse> GetDetailsAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}

sealed class RecordingProvisioningReferenceGateway : IReadOnlyProvisioningReferenceGateway
{
    public AdministrationMode Mode => AdministrationMode.Demo;

    public int CallCount { get; private set; }

    public ProvisioningReferenceRequest? LastRequest { get; private set; }

    public Task<ProvisioningReferenceSnapshot> ResolveAsync(
        ProvisioningReferenceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CallCount++;
        LastRequest = request;

        return Task.FromResult(new ProvisioningReferenceSnapshot(
            [],
            request.OrganizationalUnits,
            request.Groups,
            request.Workstations,
            TestSource()));
    }

    private static DirectoryDataSource TestSource()
    {
        return new DirectoryDataSource(
            "Recording test provider",
            "Fictional test directory",
            "TEST-DC-01",
            DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
            AdministrationMode.Demo,
            IsSynthetic: true);
    }
}

sealed class SlowProvisioningReferenceGateway : IReadOnlyProvisioningReferenceGateway
{
    public AdministrationMode Mode => AdministrationMode.Demo;

    public async Task<ProvisioningReferenceSnapshot> ResolveAsync(
        ProvisioningReferenceRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        throw new InvalidOperationException("The timeout test should cancel before this line.");
    }
}
