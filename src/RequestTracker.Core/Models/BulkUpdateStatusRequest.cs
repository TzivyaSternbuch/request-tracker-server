using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Models;

public record BulkUpdateStatusRequest
{
    public const int MaxItems = 100;

    public List<BulkStatusItem>? Items { get; init; }
    public RequestStatus? Status { get; init; }
    public string? ChangedBy { get; init; }
}
