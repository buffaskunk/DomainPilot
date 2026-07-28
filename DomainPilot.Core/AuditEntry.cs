namespace DomainPilot.Core;

/// <summary>
/// Records who did what, when, and with what outcome.
/// </summary>
public sealed record AuditEntry(DateTime Timestamp, string Actor, string Action, string Severity, string Message);
