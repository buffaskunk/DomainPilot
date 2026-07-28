namespace DomainPilot.Core;

/// <summary>
/// Defines an approved administrative action and the role/risk context a technician should see before use.
/// </summary>
public sealed record AdminActionTemplate(
    string Name,
    string RiskLevel,
    string RequiredRole,
    string Description,
    string ScriptPreview);
