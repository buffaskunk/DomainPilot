using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Translates workstation facts into technician guidance and builds a non-executable discovery preview.
/// </summary>
public sealed class EnvironmentReadinessService
{
    public IReadOnlyList<EnvironmentReadinessResult> CreatePendingResults()
    {
        return
        [
            Pending("Platform", "Supported Windows workstation"),
            Pending("Identity", "Computer and operator domain context"),
            Pending("DNS", "Local DNS suffix is available"),
            Pending("Tools", "RSAT Active Directory PowerShell module"),
            Pending("Safety", "Local inspection performs no network activity")
        ];
    }

    public IReadOnlyList<EnvironmentReadinessResult> Evaluate(LocalEnvironmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return
        [
            new EnvironmentReadinessResult(
                "Platform",
                "Supported Windows workstation",
                snapshot.IsWindows ? EnvironmentReadinessStatus.Pass : EnvironmentReadinessStatus.Blocked,
                snapshot.OperatingSystem,
                snapshot.IsWindows
                    ? "Windows is available for RSAT and integrated authentication."
                    : "Run DomainPilot on a supported Windows administrative workstation."),
            new EnvironmentReadinessResult(
                "Identity",
                "Computer and operator domain context",
                snapshot.IsDomainJoined ? EnvironmentReadinessStatus.Pass : EnvironmentReadinessStatus.Warning,
                snapshot.IsDomainJoined
                    ? $"{snapshot.MachineName} is joined to {snapshot.JoinedDomain}; operator: {snapshot.CurrentIdentity}."
                    : $"{snapshot.MachineName} did not report a domain join; operator: {snapshot.CurrentIdentity}.",
                snapshot.IsDomainJoined
                    ? "The joined domain can be proposed as a future read-only target."
                    : "Demo mode remains available. Join an authorized admin workstation before domain discovery."),
            new EnvironmentReadinessResult(
                "DNS",
                "Local DNS suffix is available",
                string.IsNullOrWhiteSpace(snapshot.DnsSuffix)
                    ? EnvironmentReadinessStatus.Warning
                    : EnvironmentReadinessStatus.Pass,
                string.IsNullOrWhiteSpace(snapshot.DnsSuffix) ? "No DNS suffix was reported." : snapshot.DnsSuffix,
                "Active Directory discovery depends on DNS. This check reads local configuration only."),
            new EnvironmentReadinessResult(
                "Tools",
                "RSAT Active Directory PowerShell module",
                snapshot.IsActiveDirectoryModuleInstalled
                    ? EnvironmentReadinessStatus.Pass
                    : EnvironmentReadinessStatus.Warning,
                snapshot.ActiveDirectoryModuleEvidence,
                snapshot.IsActiveDirectoryModuleInstalled
                    ? "The module is available for future reviewed operations."
                    : "Install the RSAT Active Directory tools before enabling directory workflows."),
            new EnvironmentReadinessResult(
                "Safety",
                "Local inspection performs no network activity",
                snapshot.NetworkActivityPerformed
                    ? EnvironmentReadinessStatus.Blocked
                    : EnvironmentReadinessStatus.Pass,
                snapshot.NetworkActivityPerformed
                    ? "The inspector reported network activity."
                    : "No DNS, LDAP, Kerberos, event-log, or remote-computer request was performed.",
                "Domain discovery remains a separate, explicit operation.")
        ];
    }

    public DomainDiscoveryPreview BuildDiscoveryPreview(
        EnvironmentProfile profile,
        LocalEnvironmentSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(snapshot);

        var target = string.IsNullOrWhiteSpace(profile.DomainName)
            ? "an operator-approved domain"
            : profile.DomainName;
        var steps = new List<DomainDiscoveryPlanStep>
        {
            new(
                1,
                "Local workstation",
                "Reconfirm local identity, domain-join state, and DNS configuration.",
                "Local API and file reads only.",
                IsNetworkActivity: false,
                CanModifyEnvironment: false),
            new(
                2,
                "DNS",
                $"Request standard Active Directory service records for {target}.",
                "A small number of normal DNS lookups; no address-range or port scan.",
                IsNetworkActivity: true,
                CanModifyEnvironment: false),
            new(
                3,
                "Windows DC Locator",
                "Request a site-aware domain controller candidate.",
                "Normal domain-controller locator traffic using the workstation's Windows identity.",
                IsNetworkActivity: true,
                CanModifyEnvironment: false),
            new(
                4,
                "Selected domain controller",
                "Read RootDSE naming contexts, capabilities, and server identity.",
                "One base-scope directory read; no user, group, computer, or OU enumeration.",
                IsNetworkActivity: true,
                CanModifyEnvironment: false),
            new(
                5,
                "DomainPilot",
                "Display the selected site, controller, timing, and any failed prerequisite.",
                "Local presentation and in-memory audit entry only.",
                IsNetworkActivity: false,
                CanModifyEnvironment: false)
        };

        return new DomainDiscoveryPreview(
            profile,
            $"Preview only. Current mode remains {profile.Mode}. The proposed discovery would use {snapshot.CurrentIdentity} against {target}, perform low-volume DNS and directory reads, and contain no write operation.",
            steps);
    }

    private static EnvironmentReadinessResult Pending(string area, string check)
    {
        return new EnvironmentReadinessResult(
            area,
            check,
            EnvironmentReadinessStatus.NotRun,
            "Not checked.",
            "Select Run Local Checks. This does not contact the network.");
    }
}
