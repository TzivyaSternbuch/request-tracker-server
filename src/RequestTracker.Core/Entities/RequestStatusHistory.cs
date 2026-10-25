using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Entities;

public class RequestStatusHistory
{
    public const int ChangedByMaxLength = 100;

    public int Id { get; private set; }
    public int RequestId { get; set; }
    public RequestStatus PreviousStatus { get; set; }
    public RequestStatus NewStatus { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public required string ChangedBy { get; set; }
}
