namespace DomainPilot.Core;

/// <summary>
/// Presents one directory attribute as display-safe text while preserving its technician-facing category.
/// </summary>
public sealed record DirectoryAttributeValue(
    string Category,
    string Name,
    string Value);
