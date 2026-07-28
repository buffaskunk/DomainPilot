using DomainPilot.Core;

namespace DomainPilot.App;

/// <summary>
/// Applies shared search limits and timeouts before a directory provider receives a request.
/// </summary>
public sealed class DirectoryExplorerService(
    IReadOnlyDirectoryGateway gateway,
    TimeSpan? operationTimeout = null)
{
    public const int MaximumQueryLength = 128;
    public const int MaximumResultLimit = 200;

    private readonly TimeSpan _operationTimeout = operationTimeout ?? TimeSpan.FromSeconds(10);

    public AdministrationMode Mode => gateway.Mode;

    public async Task<DirectorySearchResponse> SearchAsync(
        DirectorySearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedQuery = request.Query.Trim();
        if (normalizedQuery.Length < 2)
        {
            throw new ArgumentException("Enter at least two characters to avoid an unbounded directory search.", nameof(request));
        }

        if (normalizedQuery.Length > MaximumQueryLength)
        {
            throw new ArgumentException($"Search text cannot exceed {MaximumQueryLength} characters.", nameof(request));
        }

        var boundedLimit = Math.Clamp(request.MaximumResults, 1, MaximumResultLimit);
        var boundedRequest = request with
        {
            Query = normalizedQuery,
            MaximumResults = boundedLimit
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_operationTimeout);

        try
        {
            return await gateway.SearchAsync(boundedRequest, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The directory search exceeded the {_operationTimeout.TotalSeconds:0}-second limit.");
        }
    }

    public async Task<DirectoryObjectDetailsResponse> GetDetailsAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectId) || objectId.Length > 2_048)
        {
            throw new ArgumentException("The selected directory object identifier is invalid.", nameof(objectId));
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_operationTimeout);

        try
        {
            return await gateway.GetDetailsAsync(objectId, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The directory detail request exceeded the {_operationTimeout.TotalSeconds:0}-second limit.");
        }
    }
}
