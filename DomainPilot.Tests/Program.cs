using DomainPilot.App;
using DomainPilot.Core;
using DomainPilot.Infrastructure;
using System.Xml.Linq;

var validator = new UserProvisioningValidator();
var csvImporter = new UserProvisioningCsvImporter();
var planBuilder = new PowerShellPlanBuilder();
var reportBuilder = new UserProvisioningValidationReportBuilder();
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

static MemoryStream CsvStream(string content)
{
    return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
}
