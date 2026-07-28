using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Exposes directory reads only. Write operations intentionally do not belong to this contract.
/// </summary>
public interface IReadOnlyDirectoryGateway
{
    AdministrationMode Mode { get; }

    Task<DirectorySearchResponse> SearchAsync(
        DirectorySearchRequest request,
        CancellationToken cancellationToken);

    Task<DirectoryObjectDetailsResponse> GetDetailsAsync(
        string objectId,
        CancellationToken cancellationToken);
}
