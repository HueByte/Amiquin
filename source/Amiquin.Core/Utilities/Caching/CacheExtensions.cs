using Microsoft.Extensions.Caching.Memory;

namespace Amiquin.Core.Utilities.Caching;

public static class CacheExtensions
{
    public static bool TryGetTypedValue<T>(this IMemoryCache cache, object key, out T? value)
    {
        if (cache.TryGetValue(key, out var boxed) && boxed is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public static void ConfigureAbsolute(this ICacheEntry entry, TimeSpan absoluteExpirationRelativeToNow, CacheItemPriority priority = CacheItemPriority.Normal)
    {
        entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
        entry.Priority = priority;
    }

    public static void ConfigureAbsoluteSliding(this ICacheEntry entry, TimeSpan absoluteExpirationRelativeToNow, TimeSpan slidingExpiration, CacheItemPriority priority = CacheItemPriority.Normal)
    {
        entry.AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow;
        entry.SlidingExpiration = slidingExpiration;
        entry.Priority = priority;
    }

    public static void SetAbsolute<T>(this IMemoryCache cache, object key, T value, TimeSpan absoluteExpirationRelativeToNow)
    {
        cache.Set(key, value, absoluteExpirationRelativeToNow);
    }
}
