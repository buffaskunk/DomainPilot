namespace DomainPilot.Core;

/// <summary>
/// Controls how close DomainPilot is allowed to get to a real environment.
/// New installations must start in Demo mode so simply launching the app cannot query or modify a domain.
/// </summary>
public enum AdministrationMode
{
    Demo,
    DryRun,
    Live
}
