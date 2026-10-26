using System.Linq.Expressions;
using RequestTracker.Core.Models;

namespace RequestTracker.Infrastructure.Repositories;

// Picks OrderBy or OrderByDescending from the direction, so each sort column is written once.
internal static class QueryableSortExtensions
{
    public static IOrderedQueryable<T> OrderBy<T, TKey>(
        this IQueryable<T> query, Expression<Func<T, TKey>> key, SortDirection direction) =>
        direction == SortDirection.Asc ? query.OrderBy(key) : query.OrderByDescending(key);

    public static IOrderedQueryable<T> ThenBy<T, TKey>(
        this IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, SortDirection direction) =>
        direction == SortDirection.Asc ? query.ThenBy(key) : query.ThenByDescending(key);
}
