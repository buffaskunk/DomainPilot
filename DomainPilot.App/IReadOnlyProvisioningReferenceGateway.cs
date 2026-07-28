using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Defines the only directory operation needed by provisioning preflight: resolving a bounded
/// set of references without creating, updating, moving, or deleting directory objects.
/// </summary>
public interface IReadOnlyProvisioningReferenceGateway
{
    AdministrationMode Mode { get; }

    Task<ProvisioningReferenceSnapshot> ResolveAsync(
        ProvisioningReferenceRequest request,
        CancellationToken cancellationToken);
}
