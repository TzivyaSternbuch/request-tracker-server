using RequestTracker.Core.Domain;

namespace RequestTracker.Core.Entities;

public class Request
{
    // Shared by the EF configuration and input validation, so the two limits cannot drift apart.
    public const int TitleMaxLength = 200;
    public const int OrganizationNameMaxLength = 200;
    public const int AssignedToMaxLength = 100;

    public int Id { get; private set; }
    public required string Title { get; set; }
    public required string OrganizationName { get; set; }
    public RequestStatus Status { get; set; }
    public RequestPriority Priority { get; set; }
    public required string AssignedTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // rowversion: the database sets a new value on every update.
    public byte[] Version { get; private set; } = [];
}
