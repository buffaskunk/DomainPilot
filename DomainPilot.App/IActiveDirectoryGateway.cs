using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Boundary for Active Directory operations. Implementations must make their mode explicit so live access cannot be hidden behind a UI click.
/// </summary>
public interface IActiveDirectoryGateway
{
    AdministrationMode Mode { get; }

    IReadOnlyList<DeviceSession> GetRecentDeviceSessions(string userName);
}
