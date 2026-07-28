namespace DomainPilot.Core;

/// <summary>
/// Reports the site-aware domain controller selected by Windows without enumerating directory objects.
/// </summary>
public sealed record DomainControllerDiscoveryResult(
    string DomainName,
    string ForestName,
    string ControllerName,
    string ControllerAddress,
    string ControllerSite,
    string ClientSite,
    bool IsGlobalCatalog,
    bool IsKeyDistributionCenter,
    bool IsWritable,
    DateTimeOffset RetrievedAt,
    string Method);
