using DomainPilot.App;
using DomainPilot.Core;

var validator = new UserProvisioningValidator();
var planBuilder = new PowerShellPlanBuilder();
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

Assert("generated provisioning plan remains dry-run", () =>
{
    var result = validator.Validate(ValidRequest());
    var script = planBuilder.BuildBulkUserPlan([result]);
    return script.Contains("New-ADUser", StringComparison.Ordinal)
        && script.Contains("-WhatIf", StringComparison.Ordinal)
        && !script.Contains("Domain Admins", StringComparison.OrdinalIgnoreCase);
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
