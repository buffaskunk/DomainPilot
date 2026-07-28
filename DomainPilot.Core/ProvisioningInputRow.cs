namespace DomainPilot.Core;

/// <summary>
/// Preserves the source line alongside a provisioning request so review findings can point
/// technicians back to the exact CSV row that needs attention.
/// </summary>
public sealed record ProvisioningInputRow(
    long SourceLine,
    UserProvisioningRequest Request);
