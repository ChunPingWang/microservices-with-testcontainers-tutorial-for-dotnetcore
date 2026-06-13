namespace ProductService.Domain.Ports.Outbound;

public interface ICachePort
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
