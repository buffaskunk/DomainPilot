using System.Text;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Builds auditable PowerShell previews. Generated commands intentionally include -WhatIf until a future live executor removes it under policy control.
/// </summary>
public sealed class PowerShellPlanBuilder
{
    public string BuildBulkUserPlan(IEnumerable<UserProvisioningValidationResult> validationResults)
    {
        var readyRows = validationResults.Where(result => result.IsReady).Select(result => result.Request).ToList();
        var builder = new StringBuilder();

        builder.AppendLine("# DomainPilot generated dry-run plan");
        builder.AppendLine("# Review with a second technician before removing -WhatIf.");
        builder.AppendLine("Import-Module ActiveDirectory");
        builder.AppendLine("$TemporaryPassword = Read-Host 'Temporary password' -AsSecureString");
        builder.AppendLine();

        foreach (var user in readyRows)
        {
            var displayName = $"{user.FirstName} {user.LastName}";
            builder.AppendLine($"# {displayName} ({user.SamAccountName})");
            builder.AppendLine($"New-ADUser -SamAccountName '{Escape(user.SamAccountName)}' -Name '{Escape(displayName)}' -GivenName '{Escape(user.FirstName)}' -Surname '{Escape(user.LastName)}' -Path '{Escape(user.OrganizationalUnit)}' -AccountPassword $TemporaryPassword -Enabled $true -ChangePasswordAtLogon $true -ProfilePath '{Escape(user.ProfilePath)}' -WhatIf");

            foreach (var group in MultiValueParser.Parse(user.Groups))
            {
                builder.AppendLine($"Add-ADGroupMember -Identity '{Escape(group)}' -Members '{Escape(user.SamAccountName)}' -WhatIf");
            }

            if (!string.IsNullOrWhiteSpace(user.AllowedWorkstations))
            {
                var workstations = string.Join(",", MultiValueParser.Parse(user.AllowedWorkstations));
                builder.AppendLine($"Set-ADUser -Identity '{Escape(user.SamAccountName)}' -LogonWorkstations '{Escape(workstations)}' -WhatIf");
            }

            builder.AppendLine();
        }

        if (readyRows.Count == 0)
        {
            builder.AppendLine("# No rows are currently ready. Validate and fix review notes first.");
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
