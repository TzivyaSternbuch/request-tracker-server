namespace RequestTracker.Core.Models;

public record StatusHistoryQueryParameters : PagedQueryParameters
{
    // Smaller than the list's page: the history is read in the details view next to the request, not as a table.
    public const int HistoryDefaultPageSize = 10;

    public StatusHistoryQueryParameters()
    {
        PageSize = HistoryDefaultPageSize;
    }
}
