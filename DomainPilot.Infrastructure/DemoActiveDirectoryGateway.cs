using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Demo gateway for public builds and work-network development. It never queries the current domain.
/// </summary>
public sealed class DemoActiveDirectoryGateway : IActiveDirectoryGateway
{
    private readonly List<DeviceSession> _sessions =
    [
        new("jmartinez", "HD-PC-014", "10.34.18.42", DateTime.Now.AddMinutes(-38), "Demo Security Event 4624"),
        new("akhan", "FIN-PC-022", "10.20.44.91", DateTime.Now.AddHours(-2), "Demo endpoint inventory sync"),
        new("jmartinez", "HD-PC-019", "10.34.18.57", DateTime.Now.AddDays(-1), "Demo Security Event 4624")
    ];

    public AdministrationMode Mode => AdministrationMode.Demo;

    public IReadOnlyList<DeviceSession> GetRecentDeviceSessions(string userName)
    {
        return _sessions
            .Where(session => session.UserName.Contains(userName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(session => session.LastSeen)
            .ToList();
    }
}
