using System.Net;
using System.Text.Json;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

[Collection(ApiCollection.Name)]
public sealed class RequestsListTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetPaged_NoFilters_ReturnsDefaultPageNewestFirst()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, Enumerable.Range(0, 25)
            .Select(i => New(prefix, title: $"r{i}", createdAt: BaseTime.AddMinutes(i)))
            .ToArray());

        var page = await GetPageAsync(_client, $"organizationName={prefix}");

        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(25, page.TotalCount);
        Assert.Equal(Enumerable.Range(5, 20).Reverse().Select(i => $"r{i}"), page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_Item_HasBase64Version()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New(prefix));

        var response = await _client.GetAsync($"/api/requests?organizationName={prefix}");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var version = body.GetProperty("items")[0].GetProperty("version").GetString();
        Assert.Equal(8, Convert.FromBase64String(version!).Length);
    }

    [Fact]
    public async Task GetPaged_CombinedFiltersAndPaging_ReturnsMatchingPageAndTotalCount()
    {
        var prefix = NewPrefix();
        Request Match(int day) => New(prefix, title: $"match-{day}", status: RequestStatus.InProgress,
            priority: RequestPriority.High, assignedTo: "alice", createdAt: BaseTime.AddDays(day - 1));
        await InsertAsync(factory,
            Match(11), Match(12), Match(13), Match(14), Match(15),
            New(prefix, status: RequestStatus.New, priority: RequestPriority.High, assignedTo: "alice",
                createdAt: BaseTime.AddDays(12)),
            New(prefix, status: RequestStatus.InProgress, priority: RequestPriority.Low, assignedTo: "alice",
                createdAt: BaseTime.AddDays(12)),
            New(prefix, status: RequestStatus.InProgress, priority: RequestPriority.High, assignedTo: "bob",
                createdAt: BaseTime.AddDays(12)),
            New(prefix, status: RequestStatus.InProgress, priority: RequestPriority.High, assignedTo: "alice",
                createdAt: BaseTime.AddDays(8)),
            New(prefix, status: RequestStatus.InProgress, priority: RequestPriority.High, assignedTo: "alice",
                createdAt: BaseTime.AddDays(20)));

        var page = await GetPageAsync(_client,
            $"organizationName={prefix}&status=InProgress&priority=High&assignedTo=alice" +
            "&createdFrom=2026-01-10&createdTo=2026-01-20&page=2&pageSize=2");

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(new[] { "match-13", "match-12" }, page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_OrganizationName_MatchesPrefixOnly()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New($"{prefix}-a", title: "starts"), New($"x{prefix}", title: "contains"));

        var page = await GetPageAsync(_client, $"organizationName={prefix}");

        Assert.Equal(new[] { "starts" }, page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_Search_MatchesTitleOrOrganizationName()
    {
        var prefix = NewPrefix();
        var term = NewPrefix()[..12];
        await InsertAsync(factory,
            New($"{prefix}-a", title: $"Report {term}"),
            New($"{prefix}-{term}", title: "in organization"),
            New($"{prefix}-b", title: "other"));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&search={term}");

        Assert.Equivalent(new[] { $"Report {term}", "in organization" }, page.Items.Select(r => r.Title), strict: true);
    }

    [Fact]
    public async Task GetPaged_SearchWithWildcardCharacter_MatchesLiterally()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New(prefix, title: "50% done"), New(prefix, title: "500 done"));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&search=50%25");

        Assert.Equal(new[] { "50% done" }, page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_CreatedTo_IncludesWholeLastDay()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory,
            New(prefix, title: "last minute", createdAt: new DateTimeOffset(2026, 1, 31, 23, 59, 0, TimeSpan.Zero)),
            New(prefix, title: "next day", createdAt: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&createdTo=2026-01-31");

        Assert.Equal(new[] { "last minute" }, page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_BlankTextFilters_AreIgnored()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New(prefix), New(prefix), New(prefix));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&assignedTo=&assignedTo=%20&search=%20");

        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task GetPaged_PageBeyondEnd_ReturnsEmptyItemsWithTotalCount()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New(prefix), New(prefix), New(prefix));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&page=5");

        Assert.Empty(page.Items);
        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task GetPaged_SeveralValuesPerSelectionFilter_MatchesAnyOfThem()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory,
            New(prefix, title: "new-alice", status: RequestStatus.New, priority: RequestPriority.High, assignedTo: "alice"),
            New(prefix, title: "progress-bob", status: RequestStatus.InProgress, priority: RequestPriority.Low,
                assignedTo: "bob"),
            New(prefix, title: "waiting", status: RequestStatus.Waiting, priority: RequestPriority.High, assignedTo: "alice"),
            New(prefix, title: "medium", status: RequestStatus.New, priority: RequestPriority.Medium, assignedTo: "alice"),
            New(prefix, title: "carol", status: RequestStatus.New, priority: RequestPriority.High, assignedTo: "carol"));

        var page = await GetPageAsync(_client,
            $"organizationName={prefix}&status=New&status=InProgress&priority=High&priority=Low" +
            "&assignedTo=alice&assignedTo=bob");

        Assert.Equivalent(new[] { "new-alice", "progress-bob" }, page.Items.Select(r => r.Title), strict: true);
    }

    // Rows a, b, c, inserted in this order (so Id ascends a → b → c); expected is the ascending order.
    [Theory]
    [InlineData("id", new[] { "a", "b", "c" })]
    [InlineData("title", new[] { "b", "a", "c" })]
    [InlineData("organizationName", new[] { "a", "b", "c" })]
    [InlineData("status", new[] { "b", "c", "a" })]
    [InlineData("priority", new[] { "c", "a", "b" })]
    [InlineData("assignedTo", new[] { "c", "a", "b" })]
    [InlineData("createdAt", new[] { "a", "b", "c" })]
    [InlineData("updatedAt", new[] { "c", "a", "b" })]
    public async Task GetPaged_SortByEachField_OrdersByThatFieldInBothDirections(string field, string[] ascending)
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New($"{prefix}-a", title: "m", status: RequestStatus.Waiting,
            priority: RequestPriority.Medium, assignedTo: "bob",
            createdAt: BaseTime.AddDays(1), updatedAt: BaseTime.AddDays(5)));
        await InsertAsync(factory, New($"{prefix}-b", title: "k", status: RequestStatus.New,
            priority: RequestPriority.High, assignedTo: "carol",
            createdAt: BaseTime.AddDays(2), updatedAt: BaseTime.AddDays(6)));
        await InsertAsync(factory, New($"{prefix}-c", title: "z", status: RequestStatus.InProgress,
            priority: RequestPriority.Low, assignedTo: "alice",
            createdAt: BaseTime.AddDays(3), updatedAt: BaseTime.AddDays(4)));

        var asc = await GetPageAsync(_client, $"organizationName={prefix}&sort={field}:asc");
        var desc = await GetPageAsync(_client, $"organizationName={prefix}&sort={field}:desc");

        Assert.Equal(ascending.Select(s => $"{prefix}-{s}"), asc.Items.Select(r => r.OrganizationName));
        Assert.Equal(ascending.Reverse().Select(s => $"{prefix}-{s}"), desc.Items.Select(r => r.OrganizationName));
    }

    [Fact]
    public async Task GetPaged_SeveralSortFields_AppliesThemInOrder()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory,
            New(prefix, title: "high-old", priority: RequestPriority.High, createdAt: BaseTime.AddDays(1)),
            New(prefix, title: "high-new", priority: RequestPriority.High, createdAt: BaseTime.AddDays(3)),
            // The oldest row, so sorting by createdAt first would put it first.
            New(prefix, title: "low", priority: RequestPriority.Low, createdAt: BaseTime),
            New(prefix, title: "high-mid", priority: RequestPriority.High, createdAt: BaseTime.AddDays(2)));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&sort=priority:desc&sort=createdAt:asc");

        Assert.Equal(new[] { "high-old", "high-mid", "high-new", "low" }, page.Items.Select(r => r.Title));
    }

    [Fact]
    public async Task GetPaged_SortWithoutDirection_SortsAscending()
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, New(prefix, title: "b"), New(prefix, title: "c"), New(prefix, title: "a"));

        var page = await GetPageAsync(_client, $"organizationName={prefix}&sort=title");

        Assert.Equal(new[] { "a", "b", "c" }, page.Items.Select(r => r.Title));
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("title")]
    public async Task GetPaged_EqualSortValues_EveryRowAppearsOnExactlyOnePage(string field)
    {
        var prefix = NewPrefix();
        await InsertAsync(factory, Enumerable.Range(0, 7).Select(_ => New(prefix)).ToArray());

        var ids = new List<int>();
        foreach (var pageNumber in new[] { 1, 2, 3 })
        {
            var page = await GetPageAsync(_client,
                $"organizationName={prefix}&sort={field}&page={pageNumber}&pageSize=3");
            ids.AddRange(page.Items.Select(r => r.Id));
        }

        Assert.Equal(7, ids.Count);
        Assert.Equal(7, ids.Distinct().Count());
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=2147483647&pageSize=100", "page")]
    [InlineData("createdTo=9999-12-31", "createdTo")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("status=New&status=99", "status")]
    [InlineData("sort=banana", "sort[0]")]
    [InlineData("sort=title&sort=title:desc", "sort")]
    [InlineData("createdFrom=2026-02-01&createdTo=2026-01-31", "createdFrom")]
    public async Task GetPaged_InvalidParameter_Returns400WithFieldError(string query, string errorKey)
    {
        var response = await _client.GetAsync($"/api/requests?{query}");

        var body = await ProblemAssert.IsProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.True(body.GetProperty("errors").TryGetProperty(errorKey, out _),
            $"Expected key '{errorKey}' in {body.GetProperty("errors").GetRawText()}");
    }
}
