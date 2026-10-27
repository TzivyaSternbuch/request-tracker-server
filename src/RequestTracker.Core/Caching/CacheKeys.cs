namespace RequestTracker.Core.Caching;

public static class CacheKeys
{
    // The cache entry, and the tag that invalidates it: every successful status change removes the tag.
    public const string Stats = "stats";
    public const string StatsTag = "stats";

    // Bounds the staleness if an invalidation is ever missed.
    public static readonly TimeSpan StatsLifetime = TimeSpan.FromMinutes(5);
}
