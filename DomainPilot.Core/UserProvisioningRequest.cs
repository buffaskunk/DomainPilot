namespace DomainPilot.Core;

/// <summary>
/// Represents one requested Active Directory user creation row before it has been approved or executed.
/// </summary>
public sealed record UserProvisioningRequest(
    string SamAccountName,
    string FirstName,
    string LastName,
    string OrganizationalUnit,
    string Groups,
    string ProfilePath,
    string AllowedWorkstations);
