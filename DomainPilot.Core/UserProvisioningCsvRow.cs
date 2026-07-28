namespace DomainPilot.Core;

/// <summary>
/// Preserves the source line for traceable feedback when a technician imports a provisioning file.
/// </summary>
public sealed record UserProvisioningCsvRow(
    long SourceLine,
    UserProvisioningRequest Request);
