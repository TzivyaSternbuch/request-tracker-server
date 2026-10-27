namespace RequestTracker.Core.Models;

// The paging inputs and limits shared by every paged endpoint.
public abstract record PagedQueryParameters
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public const int MaxPage = int.MaxValue / MaxPageSize;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
}
