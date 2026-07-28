namespace DomainPilot.Core;

/// <summary>
/// Reports which requested references were found and identifies the directory source used.
/// Values are returned rather than provider-specific objects to keep the application boundary
/// read-only and portable across future Active Directory or cloud providers.
/// </summary>
public sealed record ProvisioningReferenceSnapshot(
    IReadOnlyList<string> ExistingAccountNames,
    IReadOnlyList<string> ExistingOrganizationalUnits,
    IReadOnlyList<string> ExistingGroups,
    IReadOnlyList<string> ExistingWorkstations,
    DirectoryDataSource Source);
