using System.Text;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Produces a portable review report without adding passwords, credentials, or execution data.
/// </summary>
public sealed class UserProvisioningValidationReportBuilder
{
    public string Build(IEnumerable<(long SourceLine, UserProvisioningValidationResult Result)> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SourceLine,Status,SamAccountName,FirstName,LastName,OrganizationalUnit,Groups,ProfilePath,AllowedWorkstations,ReviewNotes");

        foreach (var row in rows)
        {
            var request = row.Result.Request;
            builder.AppendLine(string.Join(",",
                CsvCellEncoder.Encode(row.SourceLine.ToString()),
                CsvCellEncoder.Encode(row.Result.Status),
                CsvCellEncoder.Encode(request.SamAccountName),
                CsvCellEncoder.Encode(request.FirstName),
                CsvCellEncoder.Encode(request.LastName),
                CsvCellEncoder.Encode(request.OrganizationalUnit),
                CsvCellEncoder.Encode(request.Groups),
                CsvCellEncoder.Encode(request.ProfilePath),
                CsvCellEncoder.Encode(request.AllowedWorkstations),
                CsvCellEncoder.Encode(row.Result.Message)));
        }

        return builder.ToString();
    }
}
