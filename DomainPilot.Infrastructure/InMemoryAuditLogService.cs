using System.Text;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

public sealed class InMemoryAuditLogService(string actor) : IAuditLogService
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public void Add(string action, string severity, string message)
    {
        _entries.Insert(0, new AuditEntry(DateTime.Now, actor, action, severity, message));
    }

    public string ExportCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Timestamp,Actor,Action,Severity,Message");

        foreach (var entry in _entries)
        {
            builder.AppendLine($"{Csv(entry.Timestamp.ToString("O"))},{Csv(entry.Actor)},{Csv(entry.Action)},{Csv(entry.Severity)},{Csv(entry.Message)}");
        }

        return builder.ToString();
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
