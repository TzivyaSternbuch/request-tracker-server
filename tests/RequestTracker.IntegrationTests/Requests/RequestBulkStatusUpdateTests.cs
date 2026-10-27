using System.Net;
using System.Net.Http.Json;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class RequestBulkStatusUpdateTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task BulkUpdateStatus_ValidMissingAndStaleItems_ReportsEachAndAuditsOnlyTheValidOne()
    {
        var prefix = NewPrefix();
        var valid = New(prefix);
        var stale = New(prefix);
        await InsertAsync(factory, valid, stale);
        var validBefore = await GetRequestAsync(_client, valid.Id);
        var staleBefore = await GetRequestAsync(_client, stale.Id);
        await PatchStatusAsync(_client, stale.Id, new
        {
            status = "InProgress",
            version = Convert.ToBase64String(staleBefore.Version),
            changedBy = "bob",
        });
        const int missingId = int.MaxValue;

        var response = await PatchBulkAsync(
            _client,
            Items((valid.Id, validBefore.Version), (missingId, new byte[8]), (stale.Id, staleBefore.Version)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BulkUpdateStatusResultDto>(JsonOptions))!;
        Assert.Equal([valid.Id], result.Succeeded);
        Assert.Equal(
            [
                new BulkFailureDto(missingId, BulkFailureReason.NotFound),
                new BulkFailureDto(stale.Id, BulkFailureReason.Conflict),
            ],
            result.Failed);
        Assert.Equal(RequestStatus.InProgress, (await GetRequestAsync(_client, valid.Id)).Status);
        var audit = Assert.Single(await GetStoredHistoryAsync(factory, valid.Id));
        Assert.Equal(RequestStatus.New, audit.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, audit.NewStatus);
        Assert.Equal("alice", audit.ChangedBy);
        // The stale item keeps only the audit row of the earlier single update, none from the bulk call.
        var staleAudit = Assert.Single(await GetStoredHistoryAsync(factory, stale.Id));
        Assert.Equal("bob", staleAudit.ChangedBy);
    }

    [Fact]
    public async Task BulkUpdateStatus_DisallowedTransition_ReportsInvalidTransitionAndChangesNothing()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var before = await GetRequestAsync(_client, entity.Id);

        var response = await PatchBulkAsync(_client, Items((entity.Id, before.Version)), status: "Completed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<BulkUpdateStatusResultDto>(JsonOptions))!;
        Assert.Empty(result.Succeeded);
        Assert.Equal([new BulkFailureDto(entity.Id, BulkFailureReason.InvalidTransition)], result.Failed);
        Assert.Empty(await GetStoredHistoryAsync(factory, entity.Id));
        Assert.Equal(before.Version, (await GetRequestAsync(_client, entity.Id)).Version);
    }

    [Fact]
    public async Task BulkUpdateStatus_Success_InvalidatesStats()
    {
        await ClearAsync(factory);
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        await InvalidateStatsAsync(factory);
        var before = await GetRequestAsync(_client, entity.Id);
        var statsBefore = await _client.GetFromJsonAsync<RequestStatsDto>("/api/requests/stats", JsonOptions);
        Assert.Equal(1, statsBefore!.CountByStatus[RequestStatus.New]);

        await PatchBulkAsync(_client, Items((entity.Id, before.Version)));

        var statsAfter = await _client.GetFromJsonAsync<RequestStatsDto>("/api/requests/stats", JsonOptions);
        Assert.Equal(0, statsAfter!.CountByStatus[RequestStatus.New]);
        Assert.Equal(1, statsAfter.CountByStatus[RequestStatus.InProgress]);
    }

    [Fact]
    public async Task BulkUpdateStatus_MoreThanMaxItems_Returns400()
    {
        var items = Enumerable.Range(1, 101).Select(i => (i, new byte[8])).ToArray();

        var response = await PatchBulkAsync(_client, Items(items));

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkUpdateStatus_NoItems_Returns400()
    {
        var response = await PatchBulkAsync(_client, Items());

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkUpdateStatus_MissingVersion_Returns400AndChangesNothing()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var body = new
        {
            items = new object[] { new { id = entity.Id } },
            status = "InProgress",
            changedBy = "alice",
        };

        var response = await _client.PatchAsJsonAsync("/api/requests/bulk-status", body, JsonOptions);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Empty(await GetStoredHistoryAsync(factory, entity.Id));
    }

    [Fact]
    public async Task BulkUpdateStatus_MalformedVersion_Returns400()
    {
        var body = new
        {
            items = new object[] { new { id = 1, version = "not-base64!" } },
            status = "InProgress",
            changedBy = "alice",
        };

        var response = await _client.PatchAsJsonAsync("/api/requests/bulk-status", body, JsonOptions);

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BulkUpdateStatus_DuplicateId_Returns400()
    {
        var response = await PatchBulkAsync(_client, Items((1, new byte[8]), (1, new byte[8])));

        await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
    }

    private static object[] Items(params (int Id, byte[] Version)[] items) =>
        items.Select(i => (object)new { id = i.Id, version = Convert.ToBase64String(i.Version) }).ToArray();

    private static Task<HttpResponseMessage> PatchBulkAsync(
        HttpClient client, object[] items, string status = "InProgress", string changedBy = "alice") =>
        client.PatchAsJsonAsync("/api/requests/bulk-status", new { items, status, changedBy }, JsonOptions);
}
