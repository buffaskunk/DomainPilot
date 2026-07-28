using System.Text.RegularExpressions;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Prevents malformed or unsafe routing values from entering persisted environment profiles.
/// </summary>
public sealed partial class EnvironmentProfileValidator
{
    [GeneratedRegex(@"^(?=.{1,253}$)(?!-)(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$", RegexOptions.IgnoreCase)]
    private static partial Regex DnsNamePattern();

    public IReadOnlyList<string> Validate(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length > 64)
        {
            issues.Add("Profile name is required and cannot exceed 64 characters.");
        }

        if (!string.IsNullOrWhiteSpace(profile.DomainName) && !DnsNamePattern().IsMatch(profile.DomainName))
        {
            issues.Add("Domain name must be a valid DNS name.");
        }

        if (!string.IsNullOrWhiteSpace(profile.PreferredDomainController)
            && !DnsNamePattern().IsMatch(profile.PreferredDomainController))
        {
            issues.Add("Preferred domain controller must be a valid DNS name.");
        }

        if (profile.Mode == AdministrationMode.Live)
        {
            issues.Add("Live mode profiles cannot be activated until write approvals and rollback controls are implemented.");
        }

        return issues;
    }
}
