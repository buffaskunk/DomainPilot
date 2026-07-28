using System.Text;
using DomainPilot.App;
using DomainPilot.Core;

namespace DomainPilot.Infrastructure;

/// <summary>
/// Keeps the demo audit trail in memory while providing a spreadsheet-safe export for technician review.
/// </summary>
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
            builder.AppendLine(string.Join(",",
                CsvCellEncoder.Encode(entry.Timestamp.ToString("O")),
                CsvCellEncoder.Encode(entry.Actor),
                CsvCellEncoder.Encode(entry.Action),
                CsvCellEncoder.Encode(entry.Severity),
                CsvCellEncoder.Encode(entry.Message)));
        }

        return builder.ToString();
    }
}
