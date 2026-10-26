using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
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

    public static async Task<PagedResult<RequestDto>> GetPageAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/requests?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<RequestDto>>(JsonOptions))!;
    }
}
