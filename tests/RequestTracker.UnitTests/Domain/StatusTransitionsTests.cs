using RequestTracker.Core.Domain;

namespace RequestTracker.UnitTests.Domain;

public sealed class StatusTransitionsTests
{
    [Theory]
    [InlineData(RequestStatus.New, RequestStatus.New, false)]
    [InlineData(RequestStatus.New, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.New, RequestStatus.Waiting, false)]
    [InlineData(RequestStatus.New, RequestStatus.Completed, false)]
    [InlineData(RequestStatus.InProgress, RequestStatus.New, false)]
    [InlineData(RequestStatus.InProgress, RequestStatus.InProgress, false)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Waiting, true)]
    [InlineData(RequestStatus.InProgress, RequestStatus.Completed, true)]
    [InlineData(RequestStatus.Waiting, RequestStatus.New, false)]
    [InlineData(RequestStatus.Waiting, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Waiting, false)]
    [InlineData(RequestStatus.Waiting, RequestStatus.Completed, true)]
    [InlineData(RequestStatus.Completed, RequestStatus.New, false)]
    [InlineData(RequestStatus.Completed, RequestStatus.InProgress, true)]
    [InlineData(RequestStatus.Completed, RequestStatus.Waiting, false)]
    [InlineData(RequestStatus.Completed, RequestStatus.Completed, false)]
    public void IsAllowed_EveryPair_MatchesMatrix(RequestStatus from, RequestStatus to, bool expected)
    {
        var result = StatusTransitions.IsAllowed(from, to);

        Assert.Equal(expected, result);
    }
}
