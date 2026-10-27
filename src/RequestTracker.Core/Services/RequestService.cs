using FluentValidation;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Caching;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Exceptions;
using RequestTracker.Core.Models;

namespace RequestTracker.Core.Services;

public sealed class RequestService(
    IRequestRepository repository,
    IValidator<RequestQueryParameters> queryValidator,
    IValidator<UpdateStatusRequest> updateStatusValidator,
    IValidator<BulkUpdateStatusRequest> bulkUpdateStatusValidator,
    IValidator<StatusHistoryQueryParameters> historyValidator,
    HybridCache cache,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RequestService> logger) : IRequestService
{
    public async Task<PagedResult<RequestDto>> GetPagedAsync(
        RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        await queryValidator.ValidateAndThrowAsync(parameters, cancellationToken);
        return await repository.GetPagedAsync(parameters, cancellationToken);
    }

    public async Task<RequestDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var request = await repository.GetByIdAsync(id, cancellationToken);
        return request ?? throw NotFound(id);
    }

    public async Task<PagedResult<StatusHistoryDto>> GetHistoryAsync(
        int requestId, StatusHistoryQueryParameters parameters, CancellationToken cancellationToken)
    {
        await historyValidator.ValidateAndThrowAsync(parameters, cancellationToken);
        return await repository.GetHistoryAsync(requestId, parameters, cancellationToken);
    }

    public async Task<RequestDto> UpdateStatusAsync(
        int id, UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        await updateStatusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var newStatus = request.Status!.Value;

        // This read only validates the transition and supplies PreviousStatus for the audit row. It does not prevent
        // lost updates: the row can change after it. The conditional update on the version does that.
        var current = await repository.GetByIdAsync(id, cancellationToken);

        var result = await ChangeStatusAsync(
            id, current, request.Version!, newStatus, request.ChangedBy!.Trim(), cancellationToken);

        switch (result.Outcome)
        {
            case StatusChangeOutcome.NotFound:
                throw NotFound(id);
            case StatusChangeOutcome.Conflict:
                logger.LogWarning("Status update conflict for {RequestId}", id);
                throw new ConcurrencyConflictException($"Request {id} was modified by someone else.");
            case StatusChangeOutcome.InvalidTransition:
                throw new InvalidStatusTransitionException(
                    $"Cannot change status from {current!.Status} to {newStatus}.");
        }

        // Only after the committed write: invalidating earlier lets a concurrent read re-cache the old stats.
        await InvalidateStatsAsync();
        return result.Request!;
    }

    public async Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(
        BulkUpdateStatusRequest request, CancellationToken cancellationToken)
    {
        await bulkUpdateStatusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var newStatus = request.Status!.Value;
        var changedBy = request.ChangedBy!.Trim();
        var items = request.Items!;

        var current = await repository.GetByIdsAsync(items.Select(i => i.Id!.Value).ToList(), cancellationToken);

        var succeeded = new List<int>();
        var failed = new List<BulkFailureDto>();
        try
        {
            foreach (var item in items)
            {
                var id = item.Id!.Value;
                var result = await ChangeStatusAsync(
                    id, current.GetValueOrDefault(id), item.Version!, newStatus, changedBy, cancellationToken);

                if (result.Outcome == StatusChangeOutcome.Updated)
                {
                    succeeded.Add(id);
                }
                else
                {
                    failed.Add(new BulkFailureDto(id, ToFailureReason(result.Outcome)));
                }
            }
        }
        finally
        {
            if (succeeded.Count > 0)
            {
                await InvalidateStatsAsync();
            }
        }

        // Counts only: ids and changedBy stay out of the log.
        logger.LogInformation(
            "Bulk status update: {Succeeded} succeeded, {NotFound} not found, {Conflict} conflicts, {InvalidTransition} invalid transitions",
            succeeded.Count,
            failed.Count(f => f.Reason == BulkFailureReason.NotFound),
            failed.Count(f => f.Reason == BulkFailureReason.Conflict),
            failed.Count(f => f.Reason == BulkFailureReason.InvalidTransition));

        return new BulkUpdateStatusResultDto(succeeded, failed);
    }

    private async Task<StatusChangeResult> ChangeStatusAsync(
        int id,
        RequestDto? current,
        byte[] expectedVersion,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken cancellationToken)
    {
        if (current is null)
        {
            return new StatusChangeResult(StatusChangeOutcome.NotFound, null);
        }

        // A stale client gets a conflict even when its transition is also invalid: it must reload before anything else.
        if (!current.Version.SequenceEqual(expectedVersion))
        {
            return new StatusChangeResult(StatusChangeOutcome.Conflict, null);
        }

        if (!StatusTransitions.IsAllowed(current.Status, newStatus))
        {
            return new StatusChangeResult(StatusChangeOutcome.InvalidTransition, null);
        }

        var result = await repository.TryUpdateStatusAsync(
            id,
            expectedVersion,
            current.Status,
            newStatus,
            changedBy,
            timeProvider.GetUtcNow(),
            cancellationToken);

        return result.Outcome switch
        {
            UpdateStatusOutcome.NotFound => new StatusChangeResult(StatusChangeOutcome.NotFound, null),
            UpdateStatusOutcome.Conflict => new StatusChangeResult(StatusChangeOutcome.Conflict, null),
            _ => new StatusChangeResult(StatusChangeOutcome.Updated, result.Request),
        };
    }

    private static BulkFailureReason ToFailureReason(StatusChangeOutcome outcome) => outcome switch
    {
        StatusChangeOutcome.NotFound => BulkFailureReason.NotFound,
        StatusChangeOutcome.Conflict => BulkFailureReason.Conflict,
        _ => BulkFailureReason.InvalidTransition,
    };

    private enum StatusChangeOutcome
    {
        Updated,
        NotFound,
        Conflict,
        InvalidTransition,
    }

    // Request is set only when Outcome is Updated.
    private sealed record StatusChangeResult(StatusChangeOutcome Outcome, RequestDto? Request);

    private static NotFoundException NotFound(int id)
    {
        return new NotFoundException($"Request {id} was not found.");
    }

    public Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken) =>
        repository.GetAssigneesAsync(cancellationToken);

    public async Task<RequestStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var invalidationsBefore = Volatile.Read(ref s_statsInvalidations);

        // One global entry. HybridCache runs the factory once for concurrent misses, which matters because every
        // status update empties the entry. That one run can outlive the request that started it, so it gets its own
        // scope (and DbContext) instead of this request's repository, which is disposed when the request ends.
        var stats = await cache.GetOrCreateAsync(
            CacheKeys.Stats,
            (scopeFactory, timeProvider),
            static async (state, ct) =>
            {
                await using var scope = state.scopeFactory.CreateAsyncScope();
                var statsRepository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
                return await statsRepository.GetStatsAsync(state.timeProvider.GetUtcNow(), ct);
            },
            new HybridCacheEntryOptions { Expiration = CacheKeys.StatsLifetime },
            [CacheKeys.StatsTag],
            cancellationToken);

        // An update that committed while these figures were computed has already removed the tag, before this entry
        // existed, and HybridCache would serve the entry as fresh. Removing the tag again makes the next read recompute.
        if (Volatile.Read(ref s_statsInvalidations) != invalidationsBefore)
        {
            await cache.RemoveByTagAsync(CacheKeys.StatsTag, CancellationToken.None);
        }

        return stats;
    }

    // Counts invalidations in this process, like the in-memory cache it guards.
    private static long s_statsInvalidations;

    private async Task InvalidateStatsAsync()
    {
        Interlocked.Increment(ref s_statsInvalidations);

        // Not cancellable: the update is committed, so a client disconnect here would leave stale stats.
        await cache.RemoveByTagAsync(CacheKeys.StatsTag, CancellationToken.None);
    }
}
