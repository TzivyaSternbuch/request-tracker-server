using RequestTracker.Core.Domain;

namespace RequestTracker.UnitTests.Fakes;

public record TryUpdateStatusCall(
    int Id,
    byte[] ExpectedVersion,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);
