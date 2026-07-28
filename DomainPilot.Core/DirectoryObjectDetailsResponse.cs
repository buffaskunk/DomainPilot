namespace DomainPilot.Core;

/// <summary>
/// Returns detailed display attributes for one previously located directory object.
/// </summary>
public sealed record DirectoryObjectDetailsResponse(
    DirectoryObjectSummary Object,
    IReadOnlyList<DirectoryAttributeValue> Attributes,
    DirectoryDataSource Source);
