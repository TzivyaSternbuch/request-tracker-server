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

    [Theory]
    [InlineData(RequestStatus.New, new[] { RequestStatus.InProgress })]
    [InlineData(RequestStatus.InProgress, new[] { RequestStatus.Waiting, RequestStatus.Completed })]
    [InlineData(RequestStatus.Waiting, new[] { RequestStatus.InProgress, RequestStatus.Completed })]
    [InlineData(RequestStatus.Completed, new[] { RequestStatus.InProgress })]
    public void AllowedFrom_EveryStatus_ListsTheMatrixTargets(RequestStatus from, RequestStatus[] expected)
    {
        var result = StatusTransitions.AllowedFrom(from);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void AllowedFrom_EveryStatus_AgreesWithIsAllowed()
    {
        foreach (var from in Enum.GetValues<RequestStatus>())
        {
            var allowed = StatusTransitions.AllowedFrom(from);

            foreach (var to in Enum.GetValues<RequestStatus>())
                Assert.Equal(allowed.Contains(to), StatusTransitions.IsAllowed(from, to));
        }
    }
}
