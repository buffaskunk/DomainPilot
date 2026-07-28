namespace DomainPilot.Core;

/// <summary>
/// Captures local workstation facts without contacting DNS, Active Directory, or another computer.
/// </summary>
public sealed record LocalEnvironmentSnapshot(
    DateTimeOffset CheckedAt,
    string OperatingSystem,
    bool IsWindows,
    string MachineName,
    string CurrentIdentity,
    bool IsDomainJoined,
    string JoinedDomain,
    string DnsSuffix,
    bool IsActiveDirectoryModuleInstalled,
    string ActiveDirectoryModuleEvidence,
    bool NetworkActivityPerformed);
