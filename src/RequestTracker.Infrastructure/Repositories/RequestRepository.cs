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

    public Task<UpdateStatusResult> TryUpdateStatusAsync(
        int id,
        byte[] expectedVersion,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string changedBy,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

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
