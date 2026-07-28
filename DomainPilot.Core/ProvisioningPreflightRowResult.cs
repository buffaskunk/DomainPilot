namespace DomainPilot.Core;

/// <summary>
/// Combines local validation and read-only directory findings for one proposed user.
/// </summary>
public sealed record ProvisioningPreflightRowResult(
    long SourceLine,
    UserProvisioningRequest Request,
    IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsReady => Issues.All(issue => issue.Severity != ValidationSeverity.Error);

    public string Status => IsReady ? "Ready" : "Review";

    public string Message => Issues.Count == 0
        ? "Local and directory-reference checks passed."
        : string.Join(" ", Issues.Select(issue => issue.Message));
}
