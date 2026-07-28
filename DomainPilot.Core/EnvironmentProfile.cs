namespace DomainPilot.Core;

/// <summary>
/// Identifies the environment and safety mode that future directory operations must use.
/// </summary>
public sealed record EnvironmentProfile(
    string Name,
    AdministrationMode Mode,
    string DomainName,
    string PreferredDomainController,
    bool PreferLocalSite);
