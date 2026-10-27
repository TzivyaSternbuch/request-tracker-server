using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Abstractions;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.IntegrationTests.TestHost;
using static RequestTracker.IntegrationTests.Requests.TestRequests;

namespace RequestTracker.IntegrationTests.Requests;

// Calls the repository directly: through the API the service's early version check can answer 409 before the
// database is reached, so only these tests prove the database itself enforces the version.
[Collection(ApiCollection.Name)]
public sealed class RequestRepositoryUpdateTests(ApiFactory factory)
{
    [Fact]
    public async Task TryUpdateStatusAsync_StaleVersion_ReturnsConflictAndWritesNothing()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = (await GetByIdAsync(entity.Id))!;
        await TryUpdateAsync(entity.Id, original.Version, RequestStatus.New, RequestStatus.InProgress);

        var result = await TryUpdateAsync(
            entity.Id, original.Version, RequestStatus.InProgress, RequestStatus.Waiting);

        Assert.Equal(UpdateStatusOutcome.Conflict, result.Outcome);
        Assert.Null(result.Request);
        Assert.Single(await GetStoredHistoryAsync(factory, entity.Id));
        var current = (await GetByIdAsync(entity.Id))!;
        Assert.Equal(RequestStatus.InProgress, current.Status);
    }

    [Fact]
    public async Task TryUpdateStatusAsync_UnknownId_ReturnsNotFound()
    {
        var result = await TryUpdateAsync(int.MaxValue, new byte[8], RequestStatus.New, RequestStatus.InProgress);

        Assert.Equal(UpdateStatusOutcome.NotFound, result.Outcome);
        Assert.Null(result.Request);
    }

    [Fact]
    public async Task TryUpdateStatusAsync_CurrentVersion_ReturnsUpdatedWithNewVersion()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = (await GetByIdAsync(entity.Id))!;

        var result = await TryUpdateAsync(entity.Id, original.Version, RequestStatus.New, RequestStatus.InProgress);

        Assert.Equal(UpdateStatusOutcome.Updated, result.Outcome);
        Assert.NotNull(result.Request);
        Assert.NotEqual(original.Version, result.Request.Version);
        var current = (await GetByIdAsync(entity.Id))!;
        Assert.Equal(current.Version, result.Request.Version);
        Assert.Equal(RequestStatus.InProgress, current.Status);
    }

    [Fact]
    public async Task TryUpdateStatusAsync_ConcurrentSameVersion_ExactlyOneUpdated()
    {
        var entity = New(NewPrefix());
        await InsertAsync(factory, entity);
        var original = (await GetByIdAsync(entity.Id))!;

        // Both calls start before either is awaited, each with its own scope and DbContext, so they overlap in the
        // database. Asserts the outcomes, not which call wins.
        var first = TryUpdateAsync(entity.Id, original.Version, RequestStatus.New, RequestStatus.InProgress);
        var second = TryUpdateAsync(entity.Id, original.Version, RequestStatus.New, RequestStatus.InProgress);
        var results = await Task.WhenAll(first, second);

        Assert.Equal(
            new[] { UpdateStatusOutcome.Updated, UpdateStatusOutcome.Conflict },
            results.Select(r => r.Outcome).OrderBy(o => o));
        Assert.Single(await GetStoredHistoryAsync(factory, entity.Id));
        var current = (await GetByIdAsync(entity.Id))!;
        Assert.Equal(RequestStatus.InProgress, current.Status);
    }

    private async Task<UpdateStatusResult> TryUpdateAsync(
        int id, byte[] version, RequestStatus previousStatus, RequestStatus newStatus)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        return await repository.TryUpdateStatusAsync(
            id, version, previousStatus, newStatus, "alice", BaseTime.AddDays(1), CancellationToken.None);
    }

    private async Task<RequestDto?> GetByIdAsync(int id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        return await repository.GetByIdAsync(id, CancellationToken.None);
    }
}
