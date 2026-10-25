namespace RequestTracker.Core.Domain;

// Stored as int: explicit values keep existing rows meaningful if members are ever reordered.
public enum RequestStatus
{
    New = 0,
    InProgress = 1,
    Waiting = 2,
    Completed = 3,
}
