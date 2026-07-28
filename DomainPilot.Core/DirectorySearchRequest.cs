namespace DomainPilot.Core;

/// <summary>
/// Represents a bounded, read-only directory search requested by a technician.
/// </summary>
public sealed record DirectorySearchRequest(
    string Query,
    DirectoryObjectType ObjectType,
    int MaximumResults);
