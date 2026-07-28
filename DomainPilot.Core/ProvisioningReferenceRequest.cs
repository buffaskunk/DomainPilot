namespace DomainPilot.Core;

/// <summary>
/// Collects unique directory references for one bulk lookup. Providers should resolve this
/// request as a batch to avoid one network query per imported user.
/// </summary>
public sealed record ProvisioningReferenceRequest(
    IReadOnlyList<string> AccountNames,
    IReadOnlyList<string> OrganizationalUnits,
    IReadOnlyList<string> Groups,
    IReadOnlyList<string> Workstations);
