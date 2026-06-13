using FluentAssertions;
using ProductService.Infrastructure.Cache;
using StackExchange.Redis;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class RedisCacheAdapterTests(SharedContainerFixture containers)
{
    public sealed record CacheEntry(string Name, int Value);

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SetGet_RoundtripsThroughRedis()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        var cache = new RedisCacheAdapter(redis);

        await cache.SetAsync("k1", new CacheEntry("widget", 42));
        var got = await cache.GetAsync<CacheEntry>("k1");

        got.Should().Be(new CacheEntry("widget", 42));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        var cache = new RedisCacheAdapter(redis);
        (await cache.GetAsync<CacheEntry>("missing-" + Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task TTL_ExpiresEntry()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        var cache = new RedisCacheAdapter(redis);

        var key = "ttl-" + Guid.NewGuid();
        await cache.SetAsync(key, new CacheEntry("x", 1), TimeSpan.FromMilliseconds(150));
        await Task.Delay(400);
        (await cache.GetAsync<CacheEntry>(key)).Should().BeNull();
    }
}
