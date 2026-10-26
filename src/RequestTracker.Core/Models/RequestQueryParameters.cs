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

    public IReadOnlyList<RequestSort> ParseSort() =>
        Sort.Length == 0
            ? [RequestSort.Default]
            : Sort.Select(value => RequestSort.TryParse(value, out var sort)
                ? sort
                : throw new InvalidOperationException("Sort values must be validated before parsing.")).ToList();
}
