namespace DomainPilot.Core;

/// <summary>
/// Describes a validation problem in language a technician can act on.
/// </summary>
public sealed record ValidationIssue(string Field, string Message, ValidationSeverity Severity);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
