using System.Text;
using DomainPilot.Core;
using Microsoft.VisualBasic.FileIO;

namespace DomainPilot.App;

/// <summary>
/// Parses bulk-user CSV files with quoted-field support and a fixed, reviewable schema.
/// </summary>
public sealed class UserProvisioningCsvImporter
{
    public const int MaximumRows = 5_000;
    public const int MaximumFileBytes = 5 * 1024 * 1024;

    public static readonly IReadOnlyList<string> RequiredHeaders =
    [
        "SamAccountName",
        "FirstName",
        "LastName",
        "OrganizationalUnit",
        "Groups",
        "ProfilePath",
        "AllowedWorkstations"
    ];

    public UserProvisioningCsvImportResult Import(Stream csvStream)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var rows = new List<UserProvisioningCsvRow>();
        var issues = new List<UserProvisioningCsvIssue>();

        if (csvStream.CanSeek && csvStream.Length > MaximumFileBytes)
        {
            issues.Add(new UserProvisioningCsvIssue(
                null,
                "File",
                $"The CSV exceeds the {MaximumFileBytes / 1024 / 1024} MB import limit.",
                ValidationSeverity.Error));
            return new UserProvisioningCsvImportResult(rows, issues);
        }

        using var parser = new TextFieldParser(csvStream, Encoding.UTF8, true, true)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(",");

        var headerLine = parser.LineNumber;
        var headers = ReadFields(parser, issues);
        if (headers is null)
        {
            issues.Add(new UserProvisioningCsvIssue(
                headerLine,
                "Header",
                "The file is empty or its header row could not be read.",
                ValidationSeverity.Error));
            return new UserProvisioningCsvImportResult(rows, issues);
        }

        var headerMap = BuildHeaderMap(headers, headerLine, issues);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return new UserProvisioningCsvImportResult(rows, issues);
        }

        foreach (var header in headers.Where(header => !RequiredHeaders.Contains(header, StringComparer.OrdinalIgnoreCase)))
        {
            issues.Add(new UserProvisioningCsvIssue(
                headerLine,
                header,
                $"Unknown column '{header}' was ignored.",
                ValidationSeverity.Warning));
        }

        while (!parser.EndOfData)
        {
            var sourceLine = parser.LineNumber;
            var fields = ReadFields(parser, issues);
            if (fields is null || fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (rows.Count >= MaximumRows)
            {
                issues.Add(new UserProvisioningCsvIssue(
                    sourceLine,
                    "File",
                    $"The import limit is {MaximumRows:N0} users per batch. Split larger changes into reviewed batches.",
                    ValidationSeverity.Error));
                break;
            }

            if (fields.Length != headers.Length)
            {
                issues.Add(new UserProvisioningCsvIssue(
                    sourceLine,
                    "Row",
                    $"Expected {headers.Length} column(s) but found {fields.Length}. The row was skipped.",
                    ValidationSeverity.Error));
                continue;
            }

            rows.Add(new UserProvisioningCsvRow(
                sourceLine,
                new UserProvisioningRequest(
                    GetField("SamAccountName"),
                    GetField("FirstName"),
                    GetField("LastName"),
                    GetField("OrganizationalUnit"),
                    GetField("Groups"),
                    GetField("ProfilePath"),
                    GetField("AllowedWorkstations"))));

            string GetField(string header) => fields[headerMap[header]].Trim();
        }

        return new UserProvisioningCsvImportResult(rows, issues);
    }

    private static string[]? ReadFields(
        TextFieldParser parser,
        ICollection<UserProvisioningCsvIssue> issues)
    {
        try
        {
            return parser.ReadFields();
        }
        catch (MalformedLineException exception)
        {
            issues.Add(new UserProvisioningCsvIssue(
                exception.LineNumber,
                "Row",
                "The row contains malformed CSV quoting and was skipped.",
                ValidationSeverity.Error));
            return null;
        }
    }

    private static Dictionary<string, int> BuildHeaderMap(
        IReadOnlyList<string> headers,
        long headerLine,
        ICollection<UserProvisioningCsvIssue> issues)
    {
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index].Trim();
            if (!headerMap.TryAdd(header, index))
            {
                issues.Add(new UserProvisioningCsvIssue(
                    headerLine,
                    header,
                    $"Column '{header}' appears more than once.",
                    ValidationSeverity.Error));
            }
        }

        foreach (var requiredHeader in RequiredHeaders.Where(required => !headerMap.ContainsKey(required)))
        {
            issues.Add(new UserProvisioningCsvIssue(
                headerLine,
                requiredHeader,
                $"Required column '{requiredHeader}' is missing.",
                ValidationSeverity.Error));
        }

        return headerMap;
    }
}
