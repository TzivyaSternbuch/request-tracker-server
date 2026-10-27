using Microsoft.Extensions.Caching.Hybrid;

namespace RequestTracker.UnitTests.Fakes;

// Keeps entries in a dictionary and records tag invalidations; SetAsync and RemoveAsync are not needed by the service.
public sealed class FakeHybridCache(List<string> calls) : HybridCache
{
    private readonly Dictionary<string, (object Value, string[] Tags)> _entries = [];

    public CancellationToken? LastRemoveByTagToken { get; private set; }

    public string? LastKey { get; private set; }

    public HybridCacheEntryOptions? LastOptions { get; private set; }

    public IReadOnlyList<string>? LastTags { get; private set; }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            return (T)entry.Value;
        }

        var value = await factory(state, cancellationToken);
        var tagArray = tags?.ToArray() ?? [];
        _entries[key] = (value!, tagArray);
        LastKey = key;
        LastOptions = options;
        LastTags = tagArray;
        return value;
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
        foreach (var key in _entries.Where(e => e.Value.Tags.Contains(tag)).Select(e => e.Key).ToList())
        {
            _entries.Remove(key);
        }

        return ValueTask.CompletedTask;
    }
}
