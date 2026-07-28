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

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            issues.Add(new ValidationIssue(nameof(request.FirstName), "First name is required.", ValidationSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(request.LastName))
        {
            issues.Add(new ValidationIssue(nameof(request.LastName), "Last name is required.", ValidationSeverity.Error));
        }

        if (!request.OrganizationalUnit.StartsWith("OU=", StringComparison.OrdinalIgnoreCase)
            || !request.OrganizationalUnit.Contains(",DC=", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(nameof(request.OrganizationalUnit), "Use a full distinguished OU path including domain components.", ValidationSeverity.Error));
        }

        if (request.ProfilePath.Length > 0 && !request.ProfilePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            issues.Add(new ValidationIssue(nameof(request.ProfilePath), "Profile path should be a UNC path.", ValidationSeverity.Error));
        }

        if (string.IsNullOrWhiteSpace(request.Groups))
        {
            issues.Add(new ValidationIssue(nameof(request.Groups), "At least one approved role group is required.", ValidationSeverity.Error));
        }

        foreach (var group in MultiValueParser.Parse(request.Groups))
        {
            if (BlockedBulkGroups.Contains(group, StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(nameof(request.Groups), $"Privileged group '{group}' is blocked from bulk workflows.", ValidationSeverity.Error));
            }
        }

        return new UserProvisioningValidationResult(request, issues);
    }
}
