namespace DomainPilot.Core;

/// <summary>
/// Represents an immutable, auditable snapshot of a provisioning preflight.
/// </summary>
public sealed record ProvisioningBatchPreflightResult(
    string BatchId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ProvisioningPreflightRowResult> Rows,
    DirectoryDataSource Source,
    TimeSpan Duration)
{
    public int ReadyCount => Rows.Count(row => row.IsReady);

    public int ReviewCount => Rows.Count - ReadyCount;
}
