namespace DomainPilot.Core;

/// <summary>
/// Captures an explicitly approved request for one cache-friendly Windows DC Locator operation.
/// </summary>
public sealed record DomainControllerDiscoveryRequest(
    string DomainName,
    bool PreferLocalSite,
    bool OperatorApproved);
