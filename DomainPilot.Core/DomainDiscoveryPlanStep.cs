namespace DomainPilot.Core;

/// <summary>
/// Describes one proposed discovery operation before the application is allowed to perform it.
/// </summary>
public sealed record DomainDiscoveryPlanStep(
    int Order,
    string Source,
    string Operation,
    string ExpectedActivity,
    bool IsNetworkActivity,
    bool CanModifyEnvironment);
