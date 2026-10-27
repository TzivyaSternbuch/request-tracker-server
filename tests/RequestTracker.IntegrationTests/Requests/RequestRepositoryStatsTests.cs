using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Entities;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

// Calls the repository with a fixed asOf, so the age boundaries do not depend on the clock. Statistics are global,
// so each test starts from an empty table.
[Collection(ApiCollection.Name)]
public sealed class RequestRepositoryStatsTests(ApiFactory factory)
{
    private static readonly DateTimeOffset AsOf = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task GetStatsAsync_EmptyTable_ReturnsZerosAndNoAssignees()
    {
        await ClearAsync(factory);

        var stats = await GetStatsAsync();

        Assert.Equal(AsOf, stats.AsOf);
        Assert.Equal(Enum.GetValues<RequestStatus>().Length, stats.CountByStatus.Count);
        Assert.All(stats.CountByStatus.Values, count => Assert.Equal(0, count));
        Assert.Equal(new OpenRequestAgeDto(0, 0, 0), stats.OpenRequestAge);
        Assert.Empty(stats.TopAssignees);
    }

    [Fact]
    public async Task GetStatsAsync_Statuses_IncludesZeroGroupsAndCompleted()
    {
        await ClearAsync(factory);
        var prefix = NewPrefix();
        await InsertAsync(factory,
            New(prefix, status: RequestStatus.New),
            New(prefix, status: RequestStatus.New),
            New(prefix, status: RequestStatus.Waiting),
            New(prefix, status: RequestStatus.Completed),
            New(prefix, status: RequestStatus.Completed),
            New(prefix, status: RequestStatus.Completed));

        var stats = await GetStatsAsync();

        Assert.Equal(2, stats.CountByStatus[RequestStatus.New]);
        Assert.Equal(0, stats.CountByStatus[RequestStatus.InProgress]);
        Assert.Equal(1, stats.CountByStatus[RequestStatus.Waiting]);
        Assert.Equal(3, stats.CountByStatus[RequestStatus.Completed]);
    }

    [Fact]
    public async Task GetStatsAsync_OpenAgeBoundaries_AreExact()
    {
        await ClearAsync(factory);
        var prefix = NewPrefix();
        var sevenDaysAgo = AsOf.AddDays(-7);
        var thirtyDaysAgo = AsOf.AddDays(-30);
        await InsertAsync(factory,
            New(prefix, createdAt: sevenDaysAgo + OneSecond),
            New(prefix, createdAt: sevenDaysAgo),
            New(prefix, createdAt: thirtyDaysAgo + OneSecond),
            New(prefix, createdAt: thirtyDaysAgo),
            New(prefix, status: RequestStatus.Completed, createdAt: AsOf.AddDays(-8)),
            New(prefix, status: RequestStatus.Completed, createdAt: AsOf.AddDays(-40)));

        var stats = await GetStatsAsync();

        Assert.Equal(new OpenRequestAgeDto(1, 2, 1), stats.OpenRequestAge);
    }

    [Fact]
    public async Task GetStatsAsync_TopAssignees_ExcludesUnassignedAndAssigneesWithOnlyCompleted()
    {
        await ClearAsync(factory);
        var prefix = NewPrefix();
        var requests = new List<Request>();
        AddOpen(requests, prefix, "a", 7);
        AddOpen(requests, prefix, "b", 6);
        AddOpen(requests, prefix, "c", 5);
        AddOpen(requests, prefix, "d", 4);
        AddOpen(requests, prefix, "f", 3);
        AddOpen(requests, prefix, "e", 3);
        AddOpen(requests, prefix, "g", 1);
        AddOpen(requests, prefix, "", 20);
        requests.AddRange(Enumerable.Range(0, 9)
            .Select(_ => New(prefix, status: RequestStatus.Completed, assignedTo: "h")));
        await InsertAsync(factory, [.. requests]);

        var stats = await GetStatsAsync();

        // e and f tie at 3 open requests; the name decides, and g, "" and h are not in the top five.
        Assert.Equal(
            [new AssigneeWorkloadDto("a", 7), new("b", 6), new("c", 5), new("d", 4), new("e", 3)],
            stats.TopAssignees);
    }

    [Fact]
    public async Task GetStatsAsync_TopAssignees_FewerThanFiveWithOpenWork_ReturnsOnlyThose()
    {
        await ClearAsync(factory);
        var prefix = NewPrefix();
        var requests = new List<Request>();
        AddOpen(requests, prefix, "x", 2);
        AddOpen(requests, prefix, "y", 1);
        await InsertAsync(factory, [.. requests]);

        var stats = await GetStatsAsync();

        Assert.Equal([new AssigneeWorkloadDto("x", 2), new("y", 1)], stats.TopAssignees);
    }

    private static void AddOpen(List<Request> requests, string prefix, string assignedTo, int count)
    {
        requests.AddRange(Enumerable.Range(0, count).Select(_ => New(prefix, assignedTo: assignedTo)));
    }

    private async Task<RequestStatsDto> GetStatsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        return await repository.GetStatsAsync(AsOf, CancellationToken.None);
    }
}
