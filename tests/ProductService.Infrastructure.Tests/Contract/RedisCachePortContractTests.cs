using Contracts;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Cache;
using StackExchange.Redis;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests.Contract;

/// <summary>
/// Same <see cref="CachePortContract"/> the in-memory tests run, now against
/// a real Redis container. Proves the production Redis adapter and the
/// development InMemoryCachePort don't diverge on TTL semantics, deletion, etc.
/// </summary>
[Collection("SharedContainers")]
public class RedisCachePortContractTests(SharedContainerFixture containers)
    : CachePortContract
{
    protected override async Task<ICachePort> CreateAsync()
    {
        // No FLUSHALL — that requires admin mode on the connection. The shared
        // CachePortContract uses GUID-suffixed keys per test so collisions are
        // already impossible.
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        return new RedisCacheAdapter(redis);
    }
}
