using DomainPilot.Core;

namespace DomainPilot.App;

public interface IAuditLogService
{
    IReadOnlyList<AuditEntry> Entries { get; }

    void Add(string action, string severity, string message);

    string ExportCsv();
}
