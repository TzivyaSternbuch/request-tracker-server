using System.Net;
using System.Text.Json;
using RequestTracker.Core.Domain;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class RequestDetailsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetById_Existing_ReturnsRequest()
    {
        var entity = New(NewPrefix(), title: "Broken door", status: RequestStatus.InProgress,
            priority: RequestPriority.High, assignedTo: "alice", createdAt: BaseTime.AddDays(3),
            updatedAt: BaseTime.AddDays(4));
        await InsertAsync(factory, entity);

        var response = await _client.GetAsync($"/api/requests/{entity.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal(entity.Id, body.GetProperty("id").GetInt32());
        Assert.Equal(entity.Title, body.GetProperty("title").GetString());
        Assert.Equal(entity.OrganizationName, body.GetProperty("organizationName").GetString());
        Assert.Equal("InProgress", body.GetProperty("status").GetString());
        Assert.Equal("High", body.GetProperty("priority").GetString());
        Assert.Equal("alice", body.GetProperty("assignedTo").GetString());
        Assert.Equal(entity.CreatedAt, body.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(entity.UpdatedAt, body.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.Equal(8, Convert.FromBase64String(body.GetProperty("version").GetString()!).Length);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404Problem()
    {
        var response = await _client.GetAsync($"/api/requests/{int.MaxValue}");

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_NonInteger_Returns404()
    {
        var response = await _client.GetAsync("/api/requests/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
