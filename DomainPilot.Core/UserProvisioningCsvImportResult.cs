namespace DomainPilot.Core;

/// <summary>
/// Contains structurally valid CSV rows plus file-level issues that should be shown to the operator.
/// </summary>
public sealed record UserProvisioningCsvImportResult(
    IReadOnlyList<UserProvisioningCsvRow> Rows,
    IReadOnlyList<UserProvisioningCsvIssue> Issues)
{
    public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);

    public string Summary
    {
        get
        {
            var errors = Issues.Count(issue => issue.Severity == ValidationSeverity.Error);
            var warnings = Issues.Count(issue => issue.Severity == ValidationSeverity.Warning);

            return $"{Rows.Count} row(s) imported, {errors} error(s), {warnings} warning(s).";
        }
    }
}
