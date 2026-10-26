using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RequestTracker.Core.Abstractions;
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

        var totalCount = await query.CountAsync(cancellationToken);

        var sorted = Sort(query, parameters.ParseSort());

        var items = await sorted
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(RequestMapping.ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestDto>(items, parameters.Page, parameters.PageSize, totalCount);
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

        if (!string.IsNullOrWhiteSpace(parameters.OrganizationName))
        {
            query = query.Where(r => r.OrganizationName.StartsWith(parameters.OrganizationName));
        }

        var assignees = parameters.AssignedTo.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray();
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

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            // LIKE '%x%' can't use an index; this scan is the documented bottleneck (docs/PERFORMANCE.md).
            query = query.Where(r => r.Title.Contains(parameters.Search) || r.OrganizationName.Contains(parameters.Search));
        }

        return query;
    }

    private static IOrderedQueryable<Request> Sort(IQueryable<Request> query, IReadOnlyList<RequestSort> sorts)
    {
        IOrderedQueryable<Request>? ordered = null;
        foreach (var sort in sorts)
        {
            ordered = AddSortKey(query, ordered, sort);
        }

        return sorts.Any(s => s.Field == RequestSortField.Id)
            ? ordered!
            : ordered!.ThenBy(r => r.Id, sorts[^1].Direction);
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
