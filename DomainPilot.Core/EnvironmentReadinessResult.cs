namespace DomainPilot.Core;

/// <summary>
/// Gives technicians an actionable readiness result instead of exposing a raw platform exception.
/// </summary>
public sealed record EnvironmentReadinessResult(
    string Area,
    string Check,
    EnvironmentReadinessStatus Status,
    string Evidence,
    string Guidance);

public enum EnvironmentReadinessStatus
{
    NotRun,
    Pass,
    Warning,
    Blocked
}
