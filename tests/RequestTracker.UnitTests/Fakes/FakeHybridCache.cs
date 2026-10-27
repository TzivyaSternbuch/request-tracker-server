using Microsoft.Extensions.Caching.Hybrid;

namespace RequestTracker.UnitTests.Fakes;

// Records tag invalidations only; the service under test never reads or writes entries.
public sealed class FakeHybridCache(List<string> calls) : HybridCache
{
    public CancellationToken? LastRemoveByTagToken { get; private set; }

    public override ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        calls.Add($"RemoveByTag:{tag}");
        LastRemoveByTagToken = cancellationToken;
        return ValueTask.CompletedTask;
    }
}
