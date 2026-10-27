using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Entities;
using RequestTracker.Core.Mapping;
using RequestTracker.Core.Models;
using RequestTracker.Infrastructure.Persistence;

namespace RequestTracker.Infrastructure.Repositories;

public sealed class RequestRepository(RequestTrackerDbContext db) : IRequestRepository
{
    private static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);
    private static readonly TimeSpan ThirtyDays = TimeSpan.FromDays(30);
    private const int TopAssigneeCount = 5;

    public async Task<PagedResult<RequestDto>> GetPagedAsync(
        RequestQueryParameters parameters, CancellationToken cancellationToken)
    {
        var query = Filter(db.Requests.AsNoTracking(), parameters);

        // A separate statement from the page query: an update committed between the two can make totalCount
        // differ from the rows by one. Known limitation; snapshot isolation would remove it (task 17).
        var totalCount = await query.CountAsync(cancellationToken);

        var sorted = Sort(query, parameters.ParseSort());

        var items = await sorted
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(RequestMapping.ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestDto>(items, parameters.Page, parameters.PageSize, totalCount);
    }

    public async Task<RequestDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await db.Requests.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(RequestMapping.ToDto)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UpdateStatusResult> TryUpdateStatusAsync(
        int id,
        byte[] expectedVersion,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string changedBy,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The version check is in the UPDATE's WHERE clause, so the database decides who wins: once a concurrent
        // writer has changed the rowversion, this statement matches no row.
        var affected = await db.Requests
            .Where(r => r.Id == id && r.Version == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, newStatus)
                    .SetProperty(r => r.UpdatedAt, changedAt),
                cancellationToken);

        if (affected == 0)
        {
            // Nothing was written; disposing the transaction without a commit rolls it back.
            var exists = await db.Requests.AnyAsync(r => r.Id == id, cancellationToken);
            return new UpdateStatusResult(exists ? UpdateStatusOutcome.Conflict : UpdateStatusOutcome.NotFound, null);
        }

        db.RequestStatusHistory.Add(new RequestStatusHistory
        {
            RequestId = id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            ChangedAt = changedAt,
            ChangedBy = changedBy,
        });
        await db.SaveChangesAsync(cancellationToken);

        // Read inside the transaction, so the returned version is the one this update wrote, not a later writer's.
        var updated = await GetByIdAsync(id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new UpdateStatusResult(UpdateStatusOutcome.Updated, updated);
    }

    public async Task<IReadOnlyList<string>> GetAssigneesAsync(CancellationToken cancellationToken)
    {
        return await db.Requests
            .Select(r => r.AssignedTo)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(cancellationToken);
    }

    public async Task<RequestStatsDto> GetStatsAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        // One after another: a DbContext does not allow parallel queries.
        var countByStatus = await GroupCountByStatusAsync(cancellationToken);
        var openRequestAge = await BucketOpenRequestsByAgeAsync(asOf, cancellationToken);
        var topAssignees = await RankAssigneesByOpenCountAsync(cancellationToken);
        return new RequestStatsDto(asOf, countByStatus, openRequestAge, topAssignees);
    }

    private async Task<Dictionary<RequestStatus, int>> GroupCountByStatusAsync(CancellationToken cancellationToken)
    {
        var countsFromDb = await db.Requests.AsNoTracking()
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
        return Enum.GetValues<RequestStatus>()
            .ToDictionary(status => status, status => countsFromDb.GetValueOrDefault(status));
    }

    private async Task<OpenRequestAgeDto> BucketOpenRequestsByAgeAsync(DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        // Age is measured from CreatedAt. A request exactly 7 (or 30) days old belongs to the older bucket.
        var sevenDaysAgo = asOf - SevenDays;
        var thirtyDaysAgo = asOf - ThirtyDays;
        var age = await OpenRequests()
            .GroupBy(_ => 1)
            .Select(g => new OpenRequestAgeDto(
                g.Count(r => r.CreatedAt > sevenDaysAgo),
                g.Count(r => r.CreatedAt <= sevenDaysAgo && r.CreatedAt > thirtyDaysAgo),
                g.Count(r => r.CreatedAt <= thirtyDaysAgo)))
            .SingleOrDefaultAsync(cancellationToken);

        return age ?? new OpenRequestAgeDto(0, 0, 0);
    }

    private async Task<List<AssigneeWorkloadDto>> RankAssigneesByOpenCountAsync(CancellationToken cancellationToken)
    {
        var rows = await OpenRequests()
            .Where(r => r.AssignedTo != "")
            .GroupBy(r => r.AssignedTo)
            .Select(g => new { AssignedTo = g.Key, OpenCount = g.Count() })
            .OrderByDescending(a => a.OpenCount)
            .ThenBy(a => a.AssignedTo)
            .Take(TopAssigneeCount)
            .ToListAsync(cancellationToken);
        return rows.Select(a => new AssigneeWorkloadDto(a.AssignedTo, a.OpenCount)).ToList();
    }

    private IQueryable<Request> OpenRequests() =>
        db.Requests.AsNoTracking().Where(r => r.Status != RequestStatus.Completed);

    private static IQueryable<Request> Filter(IQueryable<Request> query, RequestQueryParameters parameters)
    {
        if (parameters.Status.Length > 0)
        {
            query = query.Where(r => parameters.Status.Contains(r.Status));
        }

        if (parameters.Priority.Length > 0)
        {
            query = query.Where(r => parameters.Priority.Contains(r.Priority));
        }

        // Text values are trimmed: stray spaces from a text box would otherwise make the match fail silently.
        var organizationName = parameters.OrganizationName?.Trim();
        if (!string.IsNullOrEmpty(organizationName))
        {
            query = query.Where(r => r.OrganizationName.StartsWith(organizationName));
        }

        var assignees = parameters.AssignedTo
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .ToArray();
        if (assignees.Length > 0)
        {
            query = query.Where(r => assignees.Contains(r.AssignedTo));
        }

        if (parameters.CreatedFrom is { } createdFrom)
        {
            var from = StartOfUtcDay(createdFrom);
            query = query.Where(r => r.CreatedAt >= from);
        }

        if (parameters.CreatedTo is { } createdTo)
        {
            var toExclusive = StartOfUtcDay(createdTo.AddDays(1));
            query = query.Where(r => r.CreatedAt < toExclusive);
        }

        var search = parameters.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // LIKE '%x%' can't use an index; this scan is the documented bottleneck (docs/PERFORMANCE.md).
            query = query.Where(r => r.Title.Contains(search) || r.OrganizationName.Contains(search));
        }

        return query;
    }

    // Fields whose index is (Field, CreatedAt) in RequestConfiguration: ending ties on CreatedAt lets the sort read
    // that index in order instead of sorting the whole filtered set. Change both together.
    private static readonly RequestSortField[] IndexedWithCreatedAt =
        [RequestSortField.Status, RequestSortField.Priority, RequestSortField.AssignedTo];

    private static IOrderedQueryable<Request> Sort(IQueryable<Request> query, IReadOnlyList<RequestSort> sorts)
    {
        IOrderedQueryable<Request>? ordered = null;
        foreach (var sort in sorts)
        {
            ordered = AddSortKey(query, ordered, sort);
        }

        var last = sorts[^1];
        if (IndexedWithCreatedAt.Contains(last.Field) && sorts.All(s => s.Field != RequestSortField.CreatedAt))
        {
            ordered = ordered!.ThenBy(r => r.CreatedAt, last.Direction);
        }

        // Id last, so rows with equal values keep one order across pages.
        return sorts.Any(s => s.Field == RequestSortField.Id)
            ? ordered!
            : ordered!.ThenBy(r => r.Id, last.Direction);
    }

    private static IOrderedQueryable<Request> AddSortKey(
        IQueryable<Request> query, IOrderedQueryable<Request>? ordered, RequestSort sort)
    {
        return sort.Field switch
        {
            RequestSortField.Id => By(r => r.Id),
            RequestSortField.Title => By(r => r.Title),
            RequestSortField.OrganizationName => By(r => r.OrganizationName),
            RequestSortField.Status => By(r => r.Status),
            RequestSortField.Priority => By(r => r.Priority),
            RequestSortField.AssignedTo => By(r => r.AssignedTo),
            RequestSortField.UpdatedAt => By(r => r.UpdatedAt),
            _ => By(r => r.CreatedAt),
        };

        IOrderedQueryable<Request> By<TKey>(Expression<Func<Request, TKey>> key) =>
            ordered is null ? query.OrderBy(key, sort.Direction) : ordered.ThenBy(key, sort.Direction);
    }

    private static DateTimeOffset StartOfUtcDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
