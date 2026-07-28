namespace DomainPilot.Core;

/// <summary>
/// Provides the common fields needed to compare directory search results across object types.
/// </summary>
public sealed record DirectoryObjectSummary(
    string ObjectId,
    DirectoryObjectType ObjectType,
    string Name,
    string AccountName,
    string DistinguishedName,
    string Status,
    string Description);
