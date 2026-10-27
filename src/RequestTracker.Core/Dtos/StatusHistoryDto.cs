using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Dtos;

public record StatusHistoryDto(
    int Id,
    int RequestId,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    DateTimeOffset ChangedAt,
    string ChangedBy);
