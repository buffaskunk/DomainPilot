namespace DomainPilot.Core;

/// <summary>
/// Provides a reviewable preview of future read-only discovery, including its expected network impact.
/// </summary>
public sealed record DomainDiscoveryPreview(
    EnvironmentProfile Profile,
    string Summary,
    IReadOnlyList<DomainDiscoveryPlanStep> Steps)
{
    public bool ContainsWrites => Steps.Any(step => step.CanModifyEnvironment);
}
