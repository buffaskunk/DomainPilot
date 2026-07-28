using System.Diagnostics;
using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Performs local row validation and one batched, read-only directory-reference lookup.
/// It deliberately has no dependency capable of changing a directory.
/// </summary>
public sealed class BulkProvisioningPreflightService
{
    public const int MaximumRows = 5000;

    private readonly IReadOnlyProvisioningReferenceGateway _gateway;
    private readonly UserProvisioningValidator _validator;
    private readonly TimeSpan _timeout;

    public BulkProvisioningPreflightService(
        IReadOnlyProvisioningReferenceGateway gateway,
        UserProvisioningValidator validator,
        TimeSpan? timeout = null)
    {
        _gateway = gateway;
        _validator = validator;
        _timeout = timeout ?? TimeSpan.FromSeconds(15);
    }

    public async Task<ProvisioningBatchPreflightResult> RunAsync(
        IEnumerable<ProvisioningInputRow> inputRows,
        CancellationToken cancellationToken)
    {
        var rows = inputRows.Take(MaximumRows + 1).ToList();
        if (rows.Count > MaximumRows)
        {
            throw new ArgumentException($"Provisioning preflight supports at most {MaximumRows:N0} rows.");
        }

        if (rows.Count == 0)
        {
            throw new ArgumentException("Add or import at least one user before running preflight.");
        }

        var stopwatch = Stopwatch.StartNew();
        var issuesByRow = rows.ToDictionary(
            row => row.SourceLine,
            row => _validator.Validate(row.Request).Issues.ToList());

        AddDuplicateAccountIssues(rows, issuesByRow);

        var referenceRequest = new ProvisioningReferenceRequest(
            Distinct(rows.Select(row => row.Request.SamAccountName)),
            Distinct(rows.Select(row => row.Request.OrganizationalUnit)),
            Distinct(rows.SelectMany(row => MultiValueParser.Parse(row.Request.Groups))),
            Distinct(rows.SelectMany(row => MultiValueParser.Parse(row.Request.AllowedWorkstations))));

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_timeout);

        ProvisioningReferenceSnapshot snapshot;
        try
        {
            snapshot = await _gateway
                .ResolveAsync(referenceRequest, timeoutCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Provisioning preflight exceeded its {_timeout.TotalSeconds:0}-second read-only operation limit.");
        }

        var existingAccounts = snapshot.ExistingAccountNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingOus = snapshot.ExistingOrganizationalUnits.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingGroups = snapshot.ExistingGroups.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingWorkstations = snapshot.ExistingWorkstations.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var request = row.Request;
            var issues = issuesByRow[row.SourceLine];

            if (existingAccounts.Contains(request.SamAccountName))
            {
                issues.Add(new ValidationIssue(
                    nameof(request.SamAccountName),
                    $"Account '{request.SamAccountName}' already exists in the checked directory.",
                    ValidationSeverity.Error));
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationalUnit)
                && !existingOus.Contains(request.OrganizationalUnit))
            {
                issues.Add(new ValidationIssue(
                    nameof(request.OrganizationalUnit),
                    "The target OU was not found in the checked directory.",
                    ValidationSeverity.Error));
            }

            AddMissingReferences(
                nameof(request.Groups),
                "group",
                MultiValueParser.Parse(request.Groups),
                existingGroups,
                issues);
            AddMissingReferences(
                nameof(request.AllowedWorkstations),
                "workstation",
                MultiValueParser.Parse(request.AllowedWorkstations),
                existingWorkstations,
                issues);

            if (request.ProfilePath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                issues.Add(new ValidationIssue(
                    nameof(request.ProfilePath),
                    "Profile share availability and permissions were not contacted; verify them during approval.",
                    ValidationSeverity.Warning));
            }
        }

        stopwatch.Stop();
        var results = rows
            .Select(row => new ProvisioningPreflightRowResult(
                row.SourceLine,
                row.Request,
                issuesByRow[row.SourceLine]))
            .ToList();

        return new ProvisioningBatchPreflightResult(
            $"DP-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31],
            DateTimeOffset.Now,
            results,
            snapshot.Source,
            stopwatch.Elapsed);
    }

    private static void AddDuplicateAccountIssues(
        IReadOnlyList<ProvisioningInputRow> rows,
        IReadOnlyDictionary<long, List<ValidationIssue>> issuesByRow)
    {
        var duplicates = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Request.SamAccountName))
            .GroupBy(row => row.Request.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var duplicate in duplicates)
        {
            foreach (var row in duplicate)
            {
                issuesByRow[row.SourceLine].Add(new ValidationIssue(
                    nameof(row.Request.SamAccountName),
                    $"Username '{row.Request.SamAccountName}' appears more than once in this batch.",
                    ValidationSeverity.Error));
            }
        }
    }

    private static void AddMissingReferences(
        string field,
        string referenceType,
        IEnumerable<string> requested,
        IReadOnlySet<string> existing,
        ICollection<ValidationIssue> issues)
    {
        foreach (var value in requested.Where(value => !existing.Contains(value)))
        {
            issues.Add(new ValidationIssue(
                field,
                $"The {referenceType} '{value}' was not found in the checked directory.",
                ValidationSeverity.Error));
        }
    }

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
