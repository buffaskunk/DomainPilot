namespace DomainPilot.App;

/// <summary>
/// Encodes untrusted text for CSV and neutralizes leading characters that spreadsheet software may execute as formulas.
/// </summary>
public static class CsvCellEncoder
{
    public static string Encode(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (safeValue.Length > 0 && safeValue[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            safeValue = $"'{safeValue}";
        }

        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
