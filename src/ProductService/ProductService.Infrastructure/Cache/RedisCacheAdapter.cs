using System.Text.Json;
using ProductService.Domain.Ports.Outbound;
using StackExchange.Redis;

namespace ProductService.Infrastructure.Cache;

public sealed class RedisCacheAdapter(IConnectionMultiplexer redis) : ICachePort
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var raw = await Db.StringGetAsync(key);
        if (!raw.HasValue) return null;
        return JsonSerializer.Deserialize<T>(raw.ToString(), Json);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null,
        CancellationToken ct = default) where T : class
    {
        var json = JsonSerializer.Serialize(value, Json);
        return Db.StringSetAsync(key, json, ttl);
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
        => Db.KeyDeleteAsync(key);
}

public sealed class InMemoryCachePort : ICachePort
{
    private readonly Dictionary<string, (object value, DateTime? expiry)> _store = new();
    private readonly System.Threading.Lock _gate = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        lock (_gate)
        {
            if (_store.TryGetValue(key, out var entry))
            {
                if (entry.expiry is null || entry.expiry > DateTime.UtcNow)
                    return Task.FromResult((T?)entry.value);
                _store.Remove(key);
            }
        }
        return Task.FromResult<T?>(null);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null,
        CancellationToken ct = default) where T : class
    {
        lock (_gate)
        {
            var expiry = ttl is null ? (DateTime?)null : DateTime.UtcNow.Add(ttl.Value);
            _store[key] = (value, expiry);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        lock (_gate) _store.Remove(key);
        return Task.CompletedTask;
    }
}
