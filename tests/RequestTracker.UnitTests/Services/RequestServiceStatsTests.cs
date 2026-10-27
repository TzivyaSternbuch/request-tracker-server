using RequestTracker.Core.Caching;
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
    public async Task GetStatsAsync_Always_PassesTimeProviderNowAsAsOf()
    {
        await _service.GetStatsAsync(CancellationToken.None);

        Assert.Equal(Now, _repository.LastStatsAsOf);
    }
}
