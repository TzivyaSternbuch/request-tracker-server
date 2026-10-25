using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Entities;
using RequestTracker.Infrastructure.Persistence;
using RequestTracker.IntegrationTests.TestHost;

namespace RequestTracker.IntegrationTests;

// The host migrates the empty container database on startup, so these tests check the schema that migration produced.
[Collection(ApiCollection.Name)]
public sealed class SchemaTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesDocumentedIndexes()
    {
        var indexes = await QueryNamesAsync("""
            SELECT name AS Value FROM sys.indexes
            WHERE OBJECT_NAME(object_id) IN ('Requests', 'RequestStatusHistory') AND name LIKE 'IX[_]%'
            """);

        Assert.Equivalent(
            new[]
            {
                "IX_Requests_AssignedTo_CreatedAt",
                "IX_Requests_CreatedAt",
                "IX_Requests_OrganizationName",
                "IX_Requests_Priority_CreatedAt",
                "IX_Requests_Status_CreatedAt",
                "IX_RequestStatusHistory_RequestId_ChangedAt",
            },
            indexes,
            strict: true);
    }

    [Fact]
    public async Task Migrate_EmptyDatabase_HistoryIndexSortsChangedAtDescending()
    {
        var descendingColumns = await QueryNamesAsync("""
            SELECT c.name AS Value
            FROM sys.index_columns ic
            JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.name = 'IX_RequestStatusHistory_RequestId_ChangedAt' AND ic.is_descending_key = 1
            """);

        Assert.Equal(new[] { "ChangedAt" }, descendingColumns);
    }

    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesEnumCheckConstraints()
    {
        var constraints = await QueryNamesAsync("SELECT name AS Value FROM sys.check_constraints");

        Assert.Equivalent(
            new[]
            {
                "CK_Requests_Priority",
                "CK_Requests_Status",
                "CK_RequestStatusHistory_NewStatus",
                "CK_RequestStatusHistory_PreviousStatus",
            },
            constraints,
            strict: true);
    }

    [Fact]
    public async Task Migrate_EmptyDatabase_VersionIsRowversion()
    {
        var types = await QueryNamesAsync("""
            SELECT DATA_TYPE AS Value FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = 'Requests' AND COLUMN_NAME = 'Version'
            """);

        // INFORMATION_SCHEMA still reports rowversion under its old name.
        Assert.Equal(new[] { "timestamp" }, types);
    }

    // The next two tests prove the database enforces the schema, not only that it is declared:
    // EF Core does not check enum values or max lengths before saving.
    [Fact]
    public async Task SaveChanges_UndefinedStatus_ThrowsAndWritesNothing()
    {
        var marker = Guid.NewGuid().ToString();
        var request = NewRequest(marker, status: (RequestStatus)7);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(request));

        Assert.Equal(CheckConstraintViolation, Assert.IsType<SqlException>(exception.InnerException).Number);
        Assert.False(await RequestExistsAsync(marker));
    }

    [Fact]
    public async Task SaveChanges_TitleOverMaxLength_ThrowsAndWritesNothing()
    {
        var marker = Guid.NewGuid().ToString();
        var request = NewRequest(marker, title: new string('x', Request.TitleMaxLength + 1));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(request));

        Assert.Equal(StringTruncation, Assert.IsType<SqlException>(exception.InnerException).Number);
        Assert.False(await RequestExistsAsync(marker));
    }

    private const int CheckConstraintViolation = 547;
    private const int StringTruncation = 2628;

    // The marker goes in OrganizationName, so the existence check finds only this test's row.
    private static Request NewRequest(string marker, RequestStatus status = RequestStatus.New, string title = "Title") =>
        new()
        {
            Title = title,
            OrganizationName = marker,
            Status = status,
            Priority = RequestPriority.Medium,
            AssignedTo = "handler",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

    private async Task SaveAsync(Request request)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        db.Requests.Add(request);
        await db.SaveChangesAsync();
    }

    private async Task<bool> RequestExistsAsync(string marker)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        return await db.Requests.AnyAsync(r => r.OrganizationName == marker);
    }

    private async Task<string[]> QueryNamesAsync(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequestTrackerDbContext>();
        return (await db.Database.SqlQueryRaw<string>(sql).ToListAsync()).ToArray();
    }
}
