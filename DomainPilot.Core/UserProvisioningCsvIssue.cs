namespace DomainPilot.Core;

/// <summary>
/// Describes a CSV structure or schema problem separately from Active Directory policy validation.
/// </summary>
public sealed record UserProvisioningCsvIssue(
    long? SourceLine,
    string Field,
    string Message,
    ValidationSeverity Severity);
