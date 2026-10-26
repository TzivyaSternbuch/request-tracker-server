using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Models;

public record RequestQueryParameters
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public const int MaxPage = int.MaxValue / MaxPageSize;

    public const int MaxFilterValues = 50;

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;

    public RequestStatus[] Status { get; init; } = [];
    public RequestPriority[] Priority { get; init; } = [];

    // Exact match on each value.
    public string[] AssignedTo { get; init; } = [];

    // Prefix match.
    public string? OrganizationName { get; init; }

    public DateOnly? CreatedFrom { get; init; }
    public DateOnly? CreatedTo { get; init; }

    public string? Search { get; init; }

    public string[] Sort { get; init; } = [];

    // Blank values are ignored, like blank filters, so an emptied sort falls back to the default order.
    public IReadOnlyList<RequestSort> ParseSort()
    {
        var sorts = Sort
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => RequestSort.TryParse(value, out var sort)
                ? sort
                : throw new InvalidOperationException("Sort values must be validated before parsing."))
            .ToList();
        return sorts.Count == 0 ? [RequestSort.Default] : sorts;
    }
}
