namespace DomainPilot.Core;

/// <summary>
/// Shows where a user was seen and which source produced that evidence.
/// </summary>
public sealed record DeviceSession(string UserName, string ComputerName, string IpAddress, DateTime LastSeen, string Source);
