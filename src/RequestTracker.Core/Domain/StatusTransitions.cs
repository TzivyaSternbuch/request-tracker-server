namespace RequestTracker.Core.Domain;

// The only place the transition matrix is defined. Source of the rule: docs/PLAN.md -> Status transitions.
public static class StatusTransitions
{
    private static readonly RequestStatus[] None = [];

    public static IReadOnlyList<RequestStatus> AllowedFrom(RequestStatus from) => from switch
    {
        RequestStatus.New => [RequestStatus.InProgress],
        RequestStatus.InProgress => [RequestStatus.Waiting, RequestStatus.Completed],
        RequestStatus.Waiting => [RequestStatus.InProgress, RequestStatus.Completed],
        RequestStatus.Completed => [RequestStatus.InProgress],
        _ => None,
    };

    public static bool IsAllowed(RequestStatus from, RequestStatus to) => AllowedFrom(from).Contains(to);
}
