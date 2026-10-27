using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Caching;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Entities;
using RequestTracker.Infrastructure.Persistence;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests.Requests;

// All test classes share one database, so every test puts its rows under its own organization-name prefix
// and filters on it; counts then see only that test's rows.
internal static class TestRequests
{
    public static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string NewPrefix() => Guid.NewGuid().ToString("N");

    public static Request New(
        string organizationName,
        string title = "Title",
        RequestStatus status = RequestStatus.New,
        RequestPriority priority = RequestPriority.Medium,
        string assignedTo = "handler",
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null) =>
        new()
        {
            Title = title,
            OrganizationName = organizationName,
            Status = status,
            Priority = priority,
            AssignedTo = assignedTo,
            CreatedAt = createdAt ?? BaseTime,
            UpdatedAt = updatedAt ?? createdAt ?? BaseTime,
        };

    public static async Task InsertAsync(ApiFactory factory, params Request[] requests)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        db.Requests.AddRange(requests);
        await db.SaveChangesAsync();
    }

    // For tests of global figures (statistics): history first, its foreign key restricts deleting the requests.
    // Deletes every row in the shared database, so it is only safe because all integration test classes belong to
    // ApiCollection, whose tests run one at a time. A class outside that collection could lose its rows mid-test.
    public static async Task ClearAsync(ApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        await db.RequestStatusHistory.ExecuteDeleteAsync();
        await db.Requests.ExecuteDeleteAsync();
    }

    // Rows inserted straight into the database bypass the service, so the cached statistics must be dropped by hand.
    public static async Task InvalidateStatsAsync(ApiFactory factory)
    {
        var cache = factory.Services.GetRequiredService<HybridCache>();
        await cache.RemoveByTagAsync(CacheKeys.StatsTag);
    }

    public static async Task<PagedResult<RequestDto>> GetPageAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/requests?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<RequestDto>>(JsonOptions))!;
    }

    public static async Task<PagedResult<StatusHistoryDto>> GetHistoryPageAsync(HttpClient client, int id, string query)
    {
        var response = await client.GetAsync($"/api/requests/{id}/history?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<StatusHistoryDto>>(JsonOptions))!;
    }

    public static async Task<RequestDto> GetRequestAsync(HttpClient client, int id)
    {
        var response = await client.GetAsync($"/api/requests/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RequestDto>(JsonOptions))!;
    }

    public static async Task<HttpResponseMessage> PatchStatusAsync(HttpClient client, int id, object body)
    {
        return await client.PatchAsJsonAsync($"/api/requests/{id}/status", body, JsonOptions);
    }

    public static async Task<RequestDto> ReadRequestAsync(HttpResponseMessage response)
    {
        return (await response.Content.ReadFromJsonAsync<RequestDto>(JsonOptions))!;
    }

    // Reads the table directly, to check the audit rows themselves rather than what the history endpoint returns.
    public static async Task<List<RequestStatusHistory>> GetStoredHistoryAsync(ApiFactory factory, int requestId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        return await db.RequestStatusHistory.AsNoTracking()
            .Where(h => h.RequestId == requestId)
            .OrderBy(h => h.Id)
            .ToListAsync();
    }
}
