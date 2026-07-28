namespace DomainPilot.App;

/// <summary>
/// Applies one delimiter and normalization rule to group and workstation lists throughout the app.
/// </summary>
public static class MultiValueParser
{
    public static IReadOnlyList<string> Parse(string value)
    {
        return value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
