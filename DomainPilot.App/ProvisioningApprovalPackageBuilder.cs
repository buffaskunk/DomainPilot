using System.Text.Json;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Creates a portable review artifact. The package contains proposed actions and findings, but
/// never captures a password, token, or reusable credential value.
/// </summary>
public sealed class ProvisioningApprovalPackageBuilder
{
    private static readonly string[] RollbackGuidance =
    [
        "This package is a dry-run review artifact; no rollback is required unless an operator adapts and executes the plan.",
        "Record the executed batch ID and confirm which accounts were created before attempting remediation.",
        "Disable affected accounts first to stop sign-in while preserving evidence and user data.",
        "Remove only memberships and attributes added by the confirmed batch; do not remove unrelated access.",
        "Delete an account only after separate approval confirms ownership, mailbox, profile, and retention requirements."
    ];

    public string Build(ProvisioningBatchPreflightResult result, string dryRunScript)
    {
        var package = new
        {
            schemaVersion = 1,
            result.BatchId,
            result.CreatedAt,
            mode = result.Source.Mode.ToString(),
            dataSource = result.Source,
            summary = new
            {
                totalRows = result.Rows.Count,
                result.ReadyCount,
                result.ReviewCount,
                durationMilliseconds = Math.Round(result.Duration.TotalMilliseconds, 1)
            },
            rows = result.Rows.Select(row => new
            {
                row.SourceLine,
                row.Status,
                row.Request,
                issues = row.Issues.Select(issue => new
                {
                    issue.Field,
                    issue.Message,
                    severity = issue.Severity.ToString()
                })
            }),
            dryRunPowerShell = dryRunScript,
            rollbackGuidance = RollbackGuidance
        };

        return JsonSerializer.Serialize(package, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
