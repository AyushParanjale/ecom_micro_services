namespace ECom.Shared.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default) where T : class;
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan absoluteExpiration, CancellationToken cancellationToken = default) where T : class;
}
