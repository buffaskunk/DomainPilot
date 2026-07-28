namespace DomainPilot.Core;

/// <summary>
/// Identifies the supported directory object categories without exposing provider-specific types to the UI.
/// </summary>
public enum DirectoryObjectType
{
    All,
    User,
    Computer,
    Group,
    OrganizationalUnit
}
