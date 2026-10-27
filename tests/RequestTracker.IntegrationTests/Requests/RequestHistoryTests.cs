using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class RequestHistoryTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHistory_AfterTwoUpdates_ReturnsBothNewestFirstWithAuditFields()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var first = await ChangeStatusAsync(entity.Id, "InProgress", "alice");
        var second = await ChangeStatusAsync(entity.Id, "Waiting", "bob");

        var page = await GetHistoryPageAsync(_client, entity.Id, "");

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        var newest = page.Items[0];
        Assert.Equal(entity.Id, newest.RequestId);
        Assert.Equal(RequestStatus.InProgress, newest.PreviousStatus);
        Assert.Equal(RequestStatus.Waiting, newest.NewStatus);
        Assert.Equal("bob", newest.ChangedBy);
        Assert.Equal(second.UpdatedAt, newest.ChangedAt);
        var oldest = page.Items[1];
        Assert.Equal(entity.Id, oldest.RequestId);
        Assert.Equal(RequestStatus.New, oldest.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, oldest.NewStatus);
        Assert.Equal("alice", oldest.ChangedBy);
        Assert.Equal(first.UpdatedAt, oldest.ChangedAt);
    }

    [Fact]
    public async Task GetHistory_MoreEntriesThanPageSize_SecondPageHoldsOlderEntries()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        string[] statuses = ["InProgress", "Waiting", "InProgress", "Waiting", "InProgress"];
        var changes = new List<RequestDto>();
        foreach (var status in statuses)
        {
            changes.Add(await ChangeStatusAsync(entity.Id, status, "alice"));
        }

        var firstPage = await GetHistoryPageAsync(_client, entity.Id, "page=1&pageSize=2");
        var secondPage = await GetHistoryPageAsync(_client, entity.Id, "page=2&pageSize=2");
        var lastPage = await GetHistoryPageAsync(_client, entity.Id, "page=3&pageSize=2");

        Assert.Equal(5, firstPage.TotalCount);
        Assert.Equal(5, secondPage.TotalCount);
        Assert.Equal(2, secondPage.Page);
        Assert.Equal(2, secondPage.PageSize);
        Assert.Equal(
            new[] { changes[4].UpdatedAt, changes[3].UpdatedAt },
            firstPage.Items.Select(h => h.ChangedAt));
        Assert.Equal(
            new[] { changes[2].UpdatedAt, changes[1].UpdatedAt },
            secondPage.Items.Select(h => h.ChangedAt));
        Assert.Equal(new[] { changes[0].UpdatedAt }, lastPage.Items.Select(h => h.ChangedAt));
    }

    [Fact]
    public async Task GetHistory_RequestWithoutChanges_ReturnsEmptyPage()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);

        var page = await GetHistoryPageAsync(_client, entity.Id, "");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task GetHistory_UnknownId_ReturnsEmptyPage()
    {
        var page = await GetHistoryPageAsync(_client, int.MaxValue, "");

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(StatusHistoryQueryParameters.HistoryDefaultPageSize, page.PageSize);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task GetHistory_InvalidPaging_Returns400WithFieldError(string query, string field)
    {
        var response = await _client.GetAsync($"/api/requests/1/history?{query}");

        var problem = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _));
    }

    private async Task<RequestDto> ChangeStatusAsync(int id, string status, string changedBy)
    {
        var current = await GetRequestAsync(_client, id);
        var response = await PatchStatusAsync(
            _client, id, new { status, version = Convert.ToBase64String(current.Version), changedBy });
        response.EnsureSuccessStatusCode();
        return await ReadRequestAsync(response);
    }
}
