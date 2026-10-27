using System.Net.Http.Json;
using System.Text.Json;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

// Statistics are global and cached, so each test empties the table and the cache first.
[Collection(ApiCollection.Name)]
public sealed class RequestStatsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetStats_Json_HasContractShape()
    {
        await ResetAsync();
        await InsertAsync(factory, New(NewPrefix(), assignedTo: "dana"));

        using var document = JsonDocument.Parse(await _client.GetStringAsync("/api/requests/stats"));

        var root = document.RootElement;
        Assert.Equal(
            ["asOf", "countByStatus", "openRequestAge", "topAssignees"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["New", "InProgress", "Waiting", "Completed"],
            root.GetProperty("countByStatus").EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["underSevenDays", "sevenToUnderThirtyDays", "thirtyDaysOrMore"],
            root.GetProperty("openRequestAge").EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            ["assignedTo", "openCount"],
            root.GetProperty("topAssignees")[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task GetStats_CalledTwice_SecondResponseIsCached()
    {
        await ResetAsync();
        await InsertAsync(factory, New(NewPrefix()));
        var first = await GetStatsAsync();
        await InsertAsync(factory, New(NewPrefix()));

        var second = await GetStatsAsync();

        Assert.Equal(first.AsOf, second.AsOf);
        Assert.Equal(1, second.CountByStatus[RequestStatus.New]);

        await InvalidateStatsAsync(factory);
        var afterInvalidation = await GetStatsAsync();
        Assert.Equal(2, afterInvalidation.CountByStatus[RequestStatus.New]);
    }

    [Fact]
    public async Task GetStats_AfterStatusUpdate_ReflectsTheChange()
    {
        await ResetAsync();
        var prefix = NewPrefix();
        var entity = New(prefix);
        await InsertAsync(factory, entity, New(prefix));
        var before = await GetStatsAsync();
        var current = await GetRequestAsync(_client, entity.Id);

        var response = await PatchStatusAsync(_client, entity.Id, new
        {
            status = "InProgress",
            version = Convert.ToBase64String(current.Version),
            changedBy = "alice",
        });
        response.EnsureSuccessStatusCode();
        var after = await GetStatsAsync();

        Assert.Equal(2, before.CountByStatus[RequestStatus.New]);
        Assert.Equal(1, after.CountByStatus[RequestStatus.New]);
        Assert.Equal(1, after.CountByStatus[RequestStatus.InProgress]);
        Assert.True(after.AsOf > before.AsOf);
    }

    private async Task ResetAsync()
    {
        await ClearAsync(factory);
        await InvalidateStatsAsync(factory);
    }

    private async Task<RequestStatsDto> GetStatsAsync()
    {
        var response = await _client.GetAsync("/api/requests/stats");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RequestStatsDto>(JsonOptions))!;
    }
}
