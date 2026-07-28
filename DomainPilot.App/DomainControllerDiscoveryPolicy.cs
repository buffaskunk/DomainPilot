using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Blocks domain discovery until its target is valid and an operator has explicitly approved the request.
/// </summary>
public sealed class DomainControllerDiscoveryPolicy
{
    private readonly EnvironmentProfileValidator _profileValidator = new();

    public IReadOnlyList<string> Validate(DomainControllerDiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<string>();
        var profileIssues = _profileValidator.Validate(new EnvironmentProfile(
            "Discovery validation",
            AdministrationMode.DryRun,
            request.DomainName,
            string.Empty,
            request.PreferLocalSite));

        issues.AddRange(profileIssues);
        if (string.IsNullOrWhiteSpace(request.DomainName))
        {
            issues.Add("A target domain is required.");
        }

        if (!request.OperatorApproved)
        {
            issues.Add("The operator must approve the displayed discovery plan.");
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
