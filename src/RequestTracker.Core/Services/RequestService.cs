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

    public async Task<RequestDto> UpdateStatusAsync(
        int id, UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        await updateStatusValidator.ValidateAndThrowAsync(request, cancellationToken);
        var newStatus = request.Status!.Value;
        var expectedVersion = request.Version!;

        // This read only validates the transition and supplies PreviousStatus for the audit row. It does not prevent
        // lost updates: the row can change after it. The conditional update on the version below does that.
        var current = await repository.GetByIdAsync(id, cancellationToken) ?? throw NotFound(id);

        // A stale client gets 409 even when its transition is also invalid: it must reload before anything else.
        if (!current.Version.SequenceEqual(expectedVersion))
        {
            throw Conflict(id);
        }

        if (!StatusTransitions.IsAllowed(current.Status, newStatus))
        {
            throw new InvalidStatusTransitionException(
                $"Cannot change status from {current.Status} to {newStatus}.");
        }

        var result = await repository.TryUpdateStatusAsync(
            id,
            expectedVersion,
            current.Status,
            newStatus,
            request.ChangedBy!.Trim(),
            timeProvider.GetUtcNow(),
            cancellationToken);

        if (result.Outcome == UpdateStatusOutcome.NotFound)
        {
            throw NotFound(id);
        }

        if (result.Outcome == UpdateStatusOutcome.Conflict)
        {
            throw Conflict(id);
        }

        // Only after the committed write: invalidating earlier lets a concurrent read re-cache the old stats.
        await InvalidateStatsAsync();
        return result.Request!;
    }

    private static NotFoundException NotFound(int id)
    {
        return new NotFoundException($"Request {id} was not found.");
    }

    private ConcurrencyConflictException Conflict(int id)
    {
        logger.LogWarning("Status update conflict for {RequestId}", id);
        return new ConcurrencyConflictException($"Request {id} was modified by someone else.");
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
