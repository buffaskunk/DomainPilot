namespace DomainPilot.Core;

/// <summary>
/// Tells technicians where directory information came from and whether it is synthetic.
/// </summary>
public sealed record DirectoryDataSource(
    string Provider,
    string Environment,
    string Server,
    DateTimeOffset RetrievedAt,
    AdministrationMode Mode,
    bool IsSynthetic);
