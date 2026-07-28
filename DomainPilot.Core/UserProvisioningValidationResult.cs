namespace DomainPilot.Core;

/// <summary>
/// Captures validation output for a single provisioning row without mutating the original request.
/// </summary>
public sealed record UserProvisioningValidationResult(
    UserProvisioningRequest Request,
    IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsReady => Issues.All(issue => issue.Severity != ValidationSeverity.Error);

    public string Status => IsReady ? "Ready" : "Review";

    public string Message => Issues.Count == 0
        ? "Safe to include in dry-run plan."
        : string.Join(" ", Issues.Select(issue => issue.Message));
}
