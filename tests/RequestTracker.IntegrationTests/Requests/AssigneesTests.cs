using System.Net.Http.Json;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class AssigneesTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetAssignees_RepeatedAssignees_ReturnsEachOnceInAlphabeticalOrder()
    {
        var prefix = NewPrefix();
        string[] assignees = [$"{prefix}-b", $"{prefix}-a", $"{prefix}-c"];
        await InsertAsync(factory,
            New(prefix, assignedTo: assignees[0]),
            New(prefix, assignedTo: assignees[1]),
            New(prefix, assignedTo: assignees[0]),
            New(prefix, assignedTo: assignees[2]));

        var result = await _client.GetFromJsonAsync<string[]>("/api/requests/assignees");

        Assert.NotNull(result);
        Assert.Equal(new[] { $"{prefix}-a", $"{prefix}-b", $"{prefix}-c" }, result.Where(a => a.StartsWith(prefix)));
    }
}
