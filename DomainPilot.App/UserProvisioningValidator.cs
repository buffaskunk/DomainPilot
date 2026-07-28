using System.Text.RegularExpressions;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Centralizes account row validation so the desktop UI, tests, and future import pipeline all enforce the same rules.
/// </summary>
public sealed class UserProvisioningValidator
{
    private static readonly Regex SamAccountNamePattern = new("^[a-z][a-z0-9._-]{2,19}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] BlockedBulkGroups = ["Domain Admins", "Enterprise Admins", "Schema Admins"];

    public UserProvisioningValidationResult Validate(UserProvisioningRequest request)
    {
        var issues = new List<ValidationIssue>();

        if (!SamAccountNamePattern.IsMatch(request.SamAccountName))
        {
            issues.Add(new ValidationIssue(nameof(request.SamAccountName), "Username must be 3-20 safe characters and start with a letter.", ValidationSeverity.Error));
        }

        if (!request.OrganizationalUnit.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(nameof(request.OrganizationalUnit), "Use a full distinguished OU path.", ValidationSeverity.Error));
        }

        if (request.ProfilePath.Length > 0 && !request.ProfilePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(nameof(request.ProfilePath), "Profile path should be a UNC path.", ValidationSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(request.Groups))
        {
            issues.Add(new ValidationIssue(nameof(request.Groups), "At least one approved role group is required.", ValidationSeverity.Error));
        }

        foreach (var blockedGroup in BlockedBulkGroups)
        {
            if (request.Groups.Contains(blockedGroup, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(nameof(request.Groups), $"Privileged group '{blockedGroup}' is blocked from bulk workflows.", ValidationSeverity.Error));
            }
        }

        return new UserProvisioningValidationResult(request, issues);
    }
}
