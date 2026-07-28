namespace DomainPilot.Core;

/// <summary>
/// Returns a bounded result set together with source and timing information.
/// </summary>
public sealed record DirectorySearchResponse(
    IReadOnlyList<DirectoryObjectSummary> Items,
    DirectoryDataSource Source,
    TimeSpan Duration,
    bool WasTruncated);
