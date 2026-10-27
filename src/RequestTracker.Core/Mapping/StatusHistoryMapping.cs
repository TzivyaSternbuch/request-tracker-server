using System.Linq.Expressions;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Entities;

namespace RequestTracker.Core.Mapping;

public static class StatusHistoryMapping
{
    public static readonly Expression<Func<RequestStatusHistory, StatusHistoryDto>> ToDto = h => new StatusHistoryDto(
        h.Id,
        h.RequestId,
        h.PreviousStatus,
        h.NewStatus,
        h.ChangedAt,
        h.ChangedBy);
}
