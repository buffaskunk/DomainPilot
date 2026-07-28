using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Boundary for a single read-only domain-controller locator request.
/// </summary>
public interface IReadOnlyDomainControllerDiscovery
{
    DomainControllerDiscoveryResult Discover(DomainControllerDiscoveryRequest request);
}
