using RequestTracker.Core.Caching;
using RequestTracker.Core.Domain;
using RequestTracker.Core.Dtos;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;
using RequestTracker.Core.Validation;
using RequestTracker.UnitTests.Fakes;

namespace RequestTracker.UnitTests.Services;

public sealed class RequestServiceStatsTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly List<string> _calls = [];
    private readonly FakeRequestRepository _repository;
    private readonly FakeHybridCache _cache;
    private readonly RequestService _service;

    public RequestServiceStatsTests()
    {
        _repository = new FakeRequestRepository(_calls);
        _cache = new FakeHybridCache(_calls);
        _service = new RequestService(
            _repository,
            new RequestQueryParametersValidator(),
            new UpdateStatusRequestValidator(),
            _cache,
            new FakeServiceScopeFactory(_repository),
            new FixedTimeProvider(Now),
            new CapturingLogger<RequestService>());
    }

    [Fact]
    public async Task GetStatsAsync_CalledTwice_QueriesRepositoryOnce()
    {
        await _service.GetStatsAsync(CancellationToken.None);

        var second = await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(1, _repository.StatsCalls);
        Assert.Same(_repository.Stats, second);
    }

    [Fact]
    public async Task GetStatsAsync_FirstCall_StoresEntryWithFiveMinuteExpirationAndStatsTag()
    {
        await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(CacheKeys.Stats, _cache.LastKey);
        Assert.Equal(TimeSpan.FromMinutes(5), _cache.LastOptions?.Expiration);
        Assert.Equal([CacheKeys.StatsTag], _cache.LastTags);
    }

    [Fact]
    public async Task GetStatsAsync_AfterTagRemoved_QueriesRepositoryAgain()
    {
        await _service.GetStatsAsync(CancellationToken.None);
        await _cache.RemoveByTagAsync(CacheKeys.StatsTag);

        await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, _repository.StatsCalls);
    }

    [Fact]
    public async Task GetStatsAsync_UpdateCommitsWhileComputing_NextCallRecomputes()
    {
        byte[] version = [1, 2, 3, 4, 5, 6, 7, 8];
        var row = new RequestDto(
            1, "Title", "Org", RequestStatus.New, RequestPriority.Medium, "alice", Now, Now, version);
        _repository.Current = row;
        _repository.NextResult = new UpdateStatusResult(UpdateStatusOutcome.Updated, row);
        _repository.DuringStats = () => _service.UpdateStatusAsync(
            row.Id,
            new UpdateStatusRequest { Status = RequestStatus.InProgress, Version = version, ChangedBy = "bob" },
            CancellationToken.None);

        await _service.GetStatsAsync(CancellationToken.None);
        _repository.DuringStats = null;
        await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(2, _repository.StatsCalls);
    }

    [Fact]
    public async Task GetStatsAsync_Always_PassesTimeProviderNowAsAsOf()
    {
        await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(Now, _repository.LastStatsAsOf);
    }
}
