using System.Net;
using RequestTracker.Core.Domain;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class RequestStatusUpdateTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task UpdateStatus_ValidTransition_UpdatesRowAndWritesOneAuditRow()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var before = await GetRequestAsync(_client, entity.Id);

        var response = await PatchStatusAsync(_client, entity.Id, Body("InProgress", before.Version, "alice"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadRequestAsync(response);
        Assert.Equal(RequestStatus.InProgress, body.Status);
        Assert.True(body.UpdatedAt > entity.UpdatedAt);
        Assert.NotEqual(before.Version, body.Version);
        var after = await GetRequestAsync(_client, entity.Id);
        Assert.Equal(after.Version, body.Version);
        var history = Assert.Single(await GetStoredHistoryAsync(factory, entity.Id));
        Assert.Equal(entity.Id, history.RequestId);
        Assert.Equal(RequestStatus.New, history.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, history.NewStatus);
        Assert.Equal("alice", history.ChangedBy);
        Assert.Equal(body.UpdatedAt, history.ChangedAt);
    }

    [Fact]
    public async Task UpdateStatus_StaleVersion_Returns409AndWritesNoSecondAuditRow()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = await GetRequestAsync(_client, entity.Id);
        var first = await PatchStatusAsync(_client, entity.Id, Body("InProgress", original.Version, "alice"));
        var firstBody = await ReadRequestAsync(first);

        var response = await PatchStatusAsync(_client, entity.Id, Body("Waiting", original.Version, "bob"));

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Single(await GetStoredHistoryAsync(factory, entity.Id));
        var current = await GetRequestAsync(_client, entity.Id);
        Assert.Equal(RequestStatus.InProgress, current.Status);
        Assert.Equal(firstBody.Version, current.Version);
    }

    [Fact]
    public async Task UpdateStatus_OverlappingUpdates_ExactlyOneSucceeds()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = await GetRequestAsync(_client, entity.Id);
        var otherClient = factory.CreateClient();

        // Asserts the outcomes, not which request wins, so the test is deterministic.
        var responses = await Task.WhenAll(
            PatchStatusAsync(_client, entity.Id, Body("InProgress", original.Version, "alice")),
            PatchStatusAsync(otherClient, entity.Id, Body("InProgress", original.Version, "bob")));

        Assert.Equal(
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            responses.Select(r => r.StatusCode).OrderBy(c => (int)c));
        Assert.Single(await GetStoredHistoryAsync(factory, entity.Id));
    }

    [Fact]
    public async Task UpdateStatus_UnknownId_Returns404()
    {
        var response = await PatchStatusAsync(_client, int.MaxValue, Body("InProgress", new byte[8], "alice"));

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("version")]
    [InlineData("changedBy")]
    public async Task UpdateStatus_MissingField_Returns400WithFieldError(string missingField)
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = await GetRequestAsync(_client, entity.Id);
        var body = new Dictionary<string, object>
        {
            ["status"] = "InProgress",
            ["version"] = Convert.ToBase64String(original.Version),
            ["changedBy"] = "alice",
        };
        body.Remove(missingField);

        var response = await PatchStatusAsync(_client, entity.Id, body);

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty(missingField, out _));
    }

    [Theory]
    [InlineData("not-base64!")]
    [InlineData("AAAA")]
    public async Task UpdateStatus_MalformedVersion_Returns400WithVersionError(string version)
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);

        var response = await PatchStatusAsync(_client, entity.Id,
            new { status = "InProgress", version, changedBy = "alice" });

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty("version", out _));
    }

    [Fact]
    public async Task UpdateStatus_DisallowedTransition_Returns422AndChangesNothing()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = await GetRequestAsync(_client, entity.Id);

        var response = await PatchStatusAsync(_client, entity.Id, Body("Completed", original.Version, "alice"));

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        var detail = problem.GetProperty("detail").GetString();
        Assert.Contains("New", detail);
        Assert.Contains("Completed", detail);
        Assert.Empty(await GetStoredHistoryAsync(factory, entity.Id));
        var current = await GetRequestAsync(_client, entity.Id);
        Assert.Equal(original.Version, current.Version);
    }

    private static object Body(string status, byte[] version, string changedBy)
    {
        return new { status, version = Convert.ToBase64String(version), changedBy };
    }
}
