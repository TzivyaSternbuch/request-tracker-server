namespace RequestTracker.Core.Dtos;

public enum BulkFailureReason
{
    NotFound = 0,
    Conflict = 1,
    InvalidTransition = 2,
}
