using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Dtos;

public record RequestDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);
