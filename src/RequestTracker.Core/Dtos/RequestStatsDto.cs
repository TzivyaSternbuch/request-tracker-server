using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Dtos;

// CountByStatus has every status as a key, zeros included. AsOf is when the figures were computed, so a cached
// response reports its own age.
public record RequestStatsDto(
    DateTimeOffset AsOf,
    IReadOnlyDictionary<RequestStatus, int> CountByStatus,
    OpenRequestAgeDto OpenRequestAge,
    IReadOnlyList<AssigneeWorkloadDto> TopAssignees);
