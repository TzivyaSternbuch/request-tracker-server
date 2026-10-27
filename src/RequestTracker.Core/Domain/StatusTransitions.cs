namespace RequestTracker.Core.Domain;

// The only place the transition matrix is defined. Source of the rule: docs/PLAN.md -> Status transitions.
public static class StatusTransitions
{
    public static bool IsAllowed(RequestStatus from, RequestStatus to) => (from, to) switch
    {
        (RequestStatus.New, RequestStatus.InProgress) => true,
        (RequestStatus.InProgress, RequestStatus.Waiting) => true,
        (RequestStatus.InProgress, RequestStatus.Completed) => true,
        (RequestStatus.Waiting, RequestStatus.InProgress) => true,
        (RequestStatus.Waiting, RequestStatus.Completed) => true,
        (RequestStatus.Completed, RequestStatus.InProgress) => true,
        _ => false,
    };
}
