using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Infrastructure;
using RequestTracker.Infrastructure.Persistence;
using RequestTracker.Infrastructure.Seeding;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

// A small count keeps these tests fast; it is not a multiple of the batch size, so the last, partial batch is covered.
[Collection(ApiCollection.Name)]
public sealed class DataSeederTests(ApiFactory factory)
{
    private const int RequestCount = 5_500;

    [Fact]
    public async Task SeedDatabase_RunTwice_ProducesIdenticalRows()
    {
        await factory.Services.SeedDatabaseAsync(RequestCount);
        var first = await ReadRowsAsync();

        await factory.Services.SeedDatabaseAsync(RequestCount);
        var second = await ReadRowsAsync();

        Assert.Equal(RequestCount, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SeedDatabase_ExistingData_ReplacesIt()
    {
        var marker = Guid.NewGuid().ToString();
        await AddRequestAsync(marker);

        await factory.Services.SeedDatabaseAsync(RequestCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        Assert.Equal(RequestCount, await db.Requests.CountAsync());
        Assert.False(await db.Requests.AnyAsync(r => r.OrganizationName == marker));
    }

    [Fact]
    public async Task SeedDatabase_EmptyDatabase_CreatesValidRows()
    {
        await factory.Services.SeedDatabaseAsync(RequestCount);
        var rows = await ReadRowsAsync();

        Assert.Equal(Enumerable.Range(1, RequestCount), rows.Select(r => r.Id));
        Assert.All(rows, r =>
        {
            Assert.InRange(r.CreatedAt, DataSeeder.EndDate.AddYears(-2), DataSeeder.EndDate);
            Assert.InRange(r.UpdatedAt, r.CreatedAt, DataSeeder.EndDate);
            if (r.Status == RequestStatus.New)
            {
                Assert.Equal(r.CreatedAt, r.UpdatedAt);
            }
        });
        Assert.Equivalent(Enum.GetValues<RequestStatus>(), rows.Select(r => r.Status).Distinct(), strict: true);
        Assert.Equivalent(Enum.GetValues<RequestPriority>(), rows.Select(r => r.Priority).Distinct(), strict: true);
        Assert.True(
            rows.Count(r => r.Status == RequestStatus.Completed) > rows.Length / 2,
            "Most seeded requests should be Completed.");
    }

    [Fact]
    public async Task SeedDatabaseIfEmpty_EmptyDatabase_Seeds()
    {
        await factory.Services.SeedDatabaseAsync(requestCount: 0);

        await factory.Services.SeedDatabaseIfEmptyAsync(RequestCount);

        Assert.Equal(RequestCount, (await ReadRowsAsync()).Length);
    }

    [Fact]
    public async Task SeedDatabaseIfEmpty_ExistingData_LeavesItUnchanged()
    {
        await factory.Services.SeedDatabaseAsync(requestCount: 0);
        var marker = Guid.NewGuid().ToString();
        await AddRequestAsync(marker);

        await factory.Services.SeedDatabaseIfEmptyAsync(RequestCount);

        var rows = await ReadRowsAsync();
        Assert.Equal(marker, Assert.Single(rows).OrganizationName);
    }

    // Version is left out: it is a database-wide counter, so it differs between runs by design.
    private sealed record SeededRow(
        int Id,
        string Title,
        string OrganizationName,
        RequestStatus Status,
        RequestPriority Priority,
        string AssignedTo,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private async Task<SeededRow[]> ReadRowsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        return await db.Requests
            .OrderBy(r => r.Id)
            .Select(r => new SeededRow(
                r.Id, r.Title, r.OrganizationName, r.Status, r.Priority, r.AssignedTo, r.CreatedAt, r.UpdatedAt))
            .ToArrayAsync();
    }

    private async Task AddRequestAsync(string organizationName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        db.Requests.Add(new Request
        {
            Title = "Title",
            OrganizationName = organizationName,
            Status = RequestStatus.New,
            Priority = RequestPriority.Medium,
            AssignedTo = "handler",
            CreatedAt = DataSeeder.EndDate,
            UpdatedAt = DataSeeder.EndDate,
        });
        await db.SaveChangesAsync();
    }
}
