using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Boundary for local workstation inspection. Implementations must not perform network discovery.
/// </summary>
public interface ILocalEnvironmentInspector
{
    LocalEnvironmentSnapshot Inspect();
}
