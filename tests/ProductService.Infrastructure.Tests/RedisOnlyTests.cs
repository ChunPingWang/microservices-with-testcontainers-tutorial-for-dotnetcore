using FluentAssertions;
using ProductService.Infrastructure.Cache;
using StackExchange.Redis;
using Testcontainers.Redis;
using Xunit;

namespace ProductService.Infrastructure.Tests;

/// <summary>
/// Self-contained smoke test for the Redis adapter. Boots only a Redis container,
/// avoiding the full SharedContainerFixture — useful for "does Testcontainers work
/// on this machine?" sanity checks and quick local iteration.
/// </summary>
public class RedisOnlyTests : IAsyncLifetime
{
    private RedisContainer _redis = null!;

    public async ValueTask InitializeAsync()
    {
        _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
        await _redis.StartAsync();
    }

    public async ValueTask DisposeAsync() => await _redis.DisposeAsync();

    public sealed record CacheEntry(string Name, int Value);

    [Fact]
    [Trait("Category", "Smoke")]
    public async Task SetAndGet_RoundtripsThroughRealRedis()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var cache = new RedisCacheAdapter(redis);

        await cache.SetAsync("k", new CacheEntry("widget", 42));
        var got = await cache.GetAsync<CacheEntry>("k");

        got.Should().Be(new CacheEntry("widget", 42));
    }
}
