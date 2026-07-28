namespace DomainPilot.Core;

/// <summary>
/// Documents an environmental prerequisite that must be understood before production administration.
/// </summary>
public sealed record SetupCheck(string Area, string Requirement, string Status, string HelpText);
