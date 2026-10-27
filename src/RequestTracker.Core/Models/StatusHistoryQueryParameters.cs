namespace RequestTracker.Core.Models;

// Paging limits are the list's; the default page is smaller because the history is read in the details view
// next to the request, not as a table of its own.
public record StatusHistoryQueryParameters
{
    public const int DefaultPageSize = 10;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
}
