using System.Diagnostics;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Provides a complete fictional directory for training, UI testing, and public demonstrations.
/// </summary>
public sealed class DemoReadOnlyDirectoryGateway :
    IReadOnlyDirectoryGateway,
    IReadOnlyProvisioningReferenceGateway
{
    private readonly IReadOnlyList<DemoDirectoryEntry> _entries = CreateEntries();

    public AdministrationMode Mode => AdministrationMode.Demo;

    public Task<DirectorySearchResponse> SearchAsync(
        DirectorySearchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        var matches = _entries
            .Where(entry =>
                request.ObjectType == DirectoryObjectType.All
                || entry.Summary.ObjectType == request.ObjectType)
            .Where(entry => Matches(entry.Summary, request.Query))
            .OrderBy(entry => entry.Summary.ObjectType)
            .ThenBy(entry => entry.Summary.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var resultItems = matches
            .Take(request.MaximumResults)
            .Select(entry => entry.Summary)
            .ToList();

        stopwatch.Stop();
        return Task.FromResult(new DirectorySearchResponse(
            resultItems,
            CreateSource(),
            stopwatch.Elapsed,
            matches.Count > resultItems.Count));
    }

    public Task<DirectoryObjectDetailsResponse> GetDetailsAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = _entries.FirstOrDefault(candidate =>
            candidate.Summary.ObjectId.Equals(objectId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException("The selected demo directory object no longer exists.");

        return Task.FromResult(new DirectoryObjectDetailsResponse(
            entry.Summary,
            entry.Attributes,
            CreateSource()));
    }

    public Task<ProvisioningReferenceSnapshot> ResolveAsync(
        ProvisioningReferenceRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // This emulates one bounded provider request so the application can exercise production
        // batching behavior without contacting DNS, LDAP, a domain controller, or a workstation.
        var accounts = FindRequestedValues(
            request.AccountNames,
            DirectoryObjectType.User,
            entry => entry.Summary.AccountName);
        var organizationalUnits = FindRequestedValues(
            request.OrganizationalUnits,
            DirectoryObjectType.OrganizationalUnit,
            entry => entry.Summary.DistinguishedName);
        var groups = FindRequestedValues(
            request.Groups,
            DirectoryObjectType.Group,
            entry => entry.Summary.AccountName);
        var workstations = FindRequestedValues(
            request.Workstations,
            DirectoryObjectType.Computer,
            entry => entry.Summary.Name);

        return Task.FromResult(new ProvisioningReferenceSnapshot(
            accounts,
            organizationalUnits,
            groups,
            workstations,
            CreateSource()));
    }

    private static bool Matches(DirectoryObjectSummary summary, string query)
    {
        return summary.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || summary.AccountName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || summary.DistinguishedName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || summary.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static DirectoryDataSource CreateSource()
    {
        return new DirectoryDataSource(
            "Demo read-only directory",
            "Fictional corp.example.com",
            "DEMO-DC-01",
            DateTimeOffset.Now,
            AdministrationMode.Demo,
            IsSynthetic: true);
    }

    private IReadOnlyList<string> FindRequestedValues(
        IEnumerable<string> requestedValues,
        DirectoryObjectType objectType,
        Func<DemoDirectoryEntry, string> valueSelector)
    {
        var knownValues = _entries
            .Where(entry => entry.Summary.ObjectType == objectType)
            .Select(valueSelector)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requestedValues
            .Where(knownValues.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<DemoDirectoryEntry> CreateEntries()
    {
        return
        [
            Entry(
                new("demo-user-jmartinez", DirectoryObjectType.User, "Jordan Martinez", "jmartinez", "CN=Jordan Martinez,OU=HelpDesk,OU=Users,DC=corp,DC=example,DC=com", "Enabled", "Help desk technician"),
                ("Identity", "Enabled", "True"),
                ("Identity", "User principal name", "jmartinez@corp.example.com"),
                ("Account", "Locked out", "False"),
                ("Account", "Password last set", "2026-06-15 09:42"),
                ("Account", "Password expires", "2026-09-13 09:42"),
                ("Access", "Groups", "GG-HelpDesk; GG-VPN"),
                ("Profile", "Profile path", @"\\files01\profiles\jmartinez"),
                ("Restrictions", "Allowed workstations", "HD-PC-014; HD-PC-019"),
                ("Activity", "Last logon metadata", "2026-07-28 08:51 (demo replicated value)")),
            Entry(
                new("demo-user-akhan", DirectoryObjectType.User, "Avery Khan", "akhan", "CN=Avery Khan,OU=Finance,OU=Users,DC=corp,DC=example,DC=com", "Enabled", "Finance analyst"),
                ("Identity", "Enabled", "True"),
                ("Identity", "User principal name", "akhan@corp.example.com"),
                ("Account", "Locked out", "False"),
                ("Access", "Groups", "GG-FinanceApps; GG-MFA-Enforced"),
                ("Profile", "Profile path", @"\\files01\profiles\akhan"),
                ("Restrictions", "Allowed workstations", "FIN-PC-022")),
            Entry(
                new("demo-user-temp", DirectoryObjectType.User, "Temporary User", "temp.user", "CN=Temporary User,OU=Staging,OU=Users,DC=corp,DC=example,DC=com", "Disabled", "Intentionally restricted training account"),
                ("Identity", "Enabled", "False"),
                ("Account", "Locked out", "False"),
                ("Access", "Groups", "GG-Temporary-Access"),
                ("Governance", "Review note", "Account requires sponsor approval before activation.")),
            Entry(
                new("demo-computer-helpdesk14", DirectoryObjectType.Computer, "HD-PC-014", "HD-PC-014$", "CN=HD-PC-014,OU=HelpDesk,OU=Workstations,DC=corp,DC=example,DC=com", "Enabled", "Help desk workstation"),
                ("Identity", "DNS host name", "hd-pc-014.corp.example.com"),
                ("Operating system", "Name", "Windows 11 Enterprise"),
                ("Operating system", "Version", "23H2"),
                ("Activity", "Last logon metadata", "2026-07-28 08:48 (demo)"),
                ("Network", "Last known IPv4", "10.34.18.42"),
                ("Location", "AD site", "Building-A")),
            Entry(
                new("demo-computer-fin22", DirectoryObjectType.Computer, "FIN-PC-022", "FIN-PC-022$", "CN=FIN-PC-022,OU=Finance,OU=Workstations,DC=corp,DC=example,DC=com", "Enabled", "Finance workstation"),
                ("Identity", "DNS host name", "fin-pc-022.corp.example.com"),
                ("Operating system", "Name", "Windows 11 Enterprise"),
                ("Activity", "Last logon metadata", "2026-07-28 07:16 (demo)"),
                ("Network", "Last known IPv4", "10.20.44.91"),
                ("Location", "AD site", "Building-B")),
            Entry(
                new("demo-computer-training01", DirectoryObjectType.Computer, "PC-DEMO-001", "PC-DEMO-001$", "CN=PC-DEMO-001,OU=Training,OU=Workstations,DC=corp,DC=example,DC=com", "Enabled", "Fictional training workstation"),
                ("Identity", "DNS host name", "pc-demo-001.corp.example.com"),
                ("Operating system", "Name", "Windows 11 Enterprise"),
                ("Location", "AD site", "Training-Lab")),
            Entry(
                new("demo-group-helpdesk", DirectoryObjectType.Group, "GG-HelpDesk", "GG-HelpDesk", "CN=GG-HelpDesk,OU=Role Groups,OU=Groups,DC=corp,DC=example,DC=com", "Security", "Delegated help desk role"),
                ("Group", "Scope", "Global"),
                ("Group", "Category", "Security"),
                ("Membership", "Demo direct members", "3"),
                ("Governance", "Owner", "IT Operations")),
            Entry(
                new("demo-group-vpn", DirectoryObjectType.Group, "GG-VPN", "GG-VPN", "CN=GG-VPN,OU=Access Groups,OU=Groups,DC=corp,DC=example,DC=com", "Security", "VPN access entitlement"),
                ("Group", "Scope", "Global"),
                ("Group", "Category", "Security"),
                ("Membership", "Demo direct members", "24"),
                ("Governance", "Approval", "Manager and security team")),
            Entry(
                new("demo-group-finance-apps", DirectoryObjectType.Group, "GG-FinanceApps", "GG-FinanceApps", "CN=GG-FinanceApps,OU=Role Groups,OU=Groups,DC=corp,DC=example,DC=com", "Security", "Fictional finance application entitlement"),
                ("Group", "Scope", "Global"),
                ("Group", "Category", "Security"),
                ("Governance", "Owner", "Finance Applications")),
            Entry(
                new("demo-group-mfa", DirectoryObjectType.Group, "GG-MFA-Enforced", "GG-MFA-Enforced", "CN=GG-MFA-Enforced,OU=Access Groups,OU=Groups,DC=corp,DC=example,DC=com", "Security", "Fictional strong-authentication policy group"),
                ("Group", "Scope", "Global"),
                ("Group", "Category", "Security"),
                ("Governance", "Owner", "Security Operations")),
            Entry(
                new("demo-group-standard", DirectoryObjectType.Group, "GG-Standard-Users", "GG-Standard-Users", "CN=GG-Standard-Users,OU=Role Groups,OU=Groups,DC=corp,DC=example,DC=com", "Security", "Fictional standard user baseline"),
                ("Group", "Scope", "Global"),
                ("Group", "Category", "Security"),
                ("Governance", "Owner", "Identity Operations")),
            Entry(
                new("demo-ou-helpdesk-users", DirectoryObjectType.OrganizationalUnit, "HelpDesk Users", string.Empty, "OU=HelpDesk,OU=Users,DC=corp,DC=example,DC=com", "Managed", "Help desk user account container"),
                ("Container", "Protected from deletion", "True"),
                ("Delegation", "Administrative role", "GG-HelpDesk-Account-Operators"),
                ("Policy", "Linked policy summary", "Help desk user baseline (demo)")),
            Entry(
                new("demo-ou-finance-users", DirectoryObjectType.OrganizationalUnit, "Finance Users", string.Empty, "OU=Finance,OU=Users,DC=corp,DC=example,DC=com", "Managed", "Finance user account container"),
                ("Container", "Protected from deletion", "True"),
                ("Policy", "Linked policy summary", "Finance user baseline (demo)")),
            Entry(
                new("demo-ou-staff-users", DirectoryObjectType.OrganizationalUnit, "Staff Users", string.Empty, "OU=Staff,OU=Users,DC=corp,DC=example,DC=com", "Managed", "Standard staff user account container"),
                ("Container", "Protected from deletion", "True"),
                ("Policy", "Linked policy summary", "Staff user baseline (demo)")),
            Entry(
                new("demo-ou-workstations", DirectoryObjectType.OrganizationalUnit, "Workstations", string.Empty, "OU=Workstations,DC=corp,DC=example,DC=com", "Managed", "Workstation parent container"),
                ("Container", "Protected from deletion", "True"),
                ("Policy", "Linked policy summary", "Workstation security baseline (demo)"))
        ];
    }

    private static DemoDirectoryEntry Entry(
        DirectoryObjectSummary summary,
        params (string Category, string Name, string Value)[] attributes)
    {
        return new DemoDirectoryEntry(
            summary,
            attributes
                .Select(attribute => new DirectoryAttributeValue(attribute.Category, attribute.Name, attribute.Value))
                .ToList());
    }

    private sealed record DemoDirectoryEntry(
        DirectoryObjectSummary Summary,
        IReadOnlyList<DirectoryAttributeValue> Attributes);
}
