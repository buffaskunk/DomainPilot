using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Persists environment routing preferences only; implementations must never store credentials.
/// </summary>
public interface IEnvironmentProfileStore
{
    EnvironmentProfile? Load();

    void Save(EnvironmentProfile profile);
}
