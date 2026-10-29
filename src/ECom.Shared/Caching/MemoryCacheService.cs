namespace ECom.Shared.Caching;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

// Singleton-safe: IMemoryCache is registered as a singleton by AddMemoryCache().
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache) => _cache = cache;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default) where T : class
    {
        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration
        });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default) where T : class
    {
        if (_cache.TryGetValue(key, out T? existing) && existing is not null)
            return existing;

        var value = await factory(cancellationToken);

        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration
        });
        return value;
    }
}

public static class CachingServiceCollectionExtensions
{
    public static IServiceCollection AddEComMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();
        return services;
    }
}
