using FluentAssertions;
using InventoryService.Infrastructure.Lock;
using StackExchange.Redis;
using TestInfrastructure.Containers;
using Xunit;

namespace InventoryService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class RedisDistributedLockTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentAcquire_SerializesAccess()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        var l = new RedisDistributedLock(redis);
        var key = "test-lock-" + Guid.NewGuid();

        var counter = 0;
        var winners = new List<int>();
        var t1 = Task.Run(async () =>
        {
            await using var h = await l.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            var v = Interlocked.Increment(ref counter);
            winners.Add(v);
            await Task.Delay(100);
        });
        var t2 = Task.Run(async () =>
        {
            await using var h = await l.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            var v = Interlocked.Increment(ref counter);
            winners.Add(v);
            await Task.Delay(100);
        });
        await Task.WhenAll(t1, t2);

        winners.Should().HaveCount(2).And.OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Acquire_BlockedThenReleased_Succeeds()
    {
        var redis = await ConnectionMultiplexer.ConnectAsync(containers.Redis.GetConnectionString());
        var l = new RedisDistributedLock(redis);
        var key = "test-lock-" + Guid.NewGuid();

        var first = await l.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(500));

        var acquiredBeforeRelease = false;
        var second = Task.Run(async () =>
        {
            await using var h = await l.AcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
            acquiredBeforeRelease = true;
        });

        await Task.Delay(200);
        acquiredBeforeRelease.Should().BeFalse();
        await first.DisposeAsync();
        await second;
        acquiredBeforeRelease.Should().BeTrue();
    }
}
