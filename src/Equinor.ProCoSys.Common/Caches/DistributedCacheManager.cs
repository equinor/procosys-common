using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Equinor.ProCoSys.Common.Caches;

public sealed class DistributedCacheManager(
    IDistributedCache distributedCache,
    ILogger<DistributedCacheManager> logger) : ICacheManager
{
    public Task<T> GetAsync<T>(string key, CancellationToken cancellationToken)
        => GetObjectFromCacheAsync<T>(key, cancellationToken);

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
        => distributedCache.RemoveAsync(key, cancellationToken);

    public async Task<T> GetOrCreateAsync<T>(
        string key, 
        Func<CancellationToken, Task<T>> fetch, 
        CacheDuration duration, 
        long expiration,
        CancellationToken cancellationToken)
    {
        var sw = new Stopwatch();
        sw.Start();
        var item = await GetObjectFromCacheAsync<T>(key, cancellationToken);
        if (item != null && !item.Equals(default(T)))
        {
            sw.Stop();
            logger.LogInformation("Fetching {Key} from cache ({Elapsed}ms)", key, sw.ElapsedMilliseconds);
            return item;
        }

        item = await fetch(cancellationToken);
        await AddToCache(item, key, duration, expiration, cancellationToken);

        sw.Stop();
        logger.LogInformation("Added {Key} to cache ({Elapsed}ms)", key, sw.ElapsedMilliseconds);
        return item;
    }

    private async Task AddToCache<T>(T item, string key, CacheDuration duration, long expiration,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(item);
        await distributedCache.SetStringAsync(key, serialized,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = GetEntryCacheDuration(duration, expiration)
            }, cancellationToken);
    }

    public async Task<List<T>> GetManyAsync<T>(List<string> keys, CancellationToken cancellationToken)
    {
        var result = new List<T>();
        foreach (var key in keys)
        {
            var item = await GetObjectFromCacheAsync<T>(key, cancellationToken);
            if (item != null && !item.Equals(default(T)))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public async Task CreateAsync<T>(string key, T item, CacheDuration duration, long expiration, CancellationToken cancellationToken)
    {
        await AddToCache(item, key, duration, expiration, cancellationToken);

        logger.LogInformation("Added {Key} to cache", key);
    }

    private async Task<T> GetObjectFromCacheAsync<T>(string key, CancellationToken cancellationToken)
    {
        var entry = await distributedCache.GetStringAsync(key, cancellationToken);
        if (entry is null)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(entry);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize cached value for key {key} as {typeof(T)}", ex);
        }
    }

    private static TimeSpan GetEntryCacheDuration(CacheDuration duration, long expiration) =>
        duration switch
        {
            CacheDuration.Seconds => TimeSpan.FromSeconds(expiration),
            CacheDuration.Minutes => TimeSpan.FromMinutes(expiration),
            CacheDuration.Hours => TimeSpan.FromHours(expiration),
            _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null)
        };
}
