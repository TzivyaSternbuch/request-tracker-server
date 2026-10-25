namespace RequestTracker.Core.Domain;

// Stored as int, so sorting by priority follows its meaning (Low < Medium < High).
public enum RequestPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}
