using FluentAssertions;
using ProductService.Domain.Ports.Outbound;
using Xunit;

namespace Contracts;

/// <summary>
/// Behaviour every <see cref="ICachePort"/> implementation must satisfy.
/// Verifies basic get/set/remove + TTL expiry.
/// </summary>
public abstract class CachePortContract
{
    protected abstract Task<ICachePort> CreateAsync();

    public sealed record Entry(string Name, int Value);

    [Fact]
    [Trait("Category", "Contract")]
    public async Task SetGet_Roundtrips()
    {
        var cache = await CreateAsync();
        var key = $"key-{Guid.NewGuid()}";

        await cache.SetAsync(key, new Entry("widget", 42));
        (await cache.GetAsync<Entry>(key)).Should().Be(new Entry("widget", 42));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var cache = await CreateAsync();
        (await cache.GetAsync<Entry>($"missing-{Guid.NewGuid()}")).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Remove_DeletesEntry()
    {
        var cache = await CreateAsync();
        var key = $"key-{Guid.NewGuid()}";
        await cache.SetAsync(key, new Entry("x", 1));
        await cache.RemoveAsync(key);
        (await cache.GetAsync<Entry>(key)).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task TTL_ExpiresEntry()
    {
        var cache = await CreateAsync();
        var key = $"key-{Guid.NewGuid()}";
        await cache.SetAsync(key, new Entry("x", 1), TimeSpan.FromMilliseconds(150));
        await Task.Delay(500);
        (await cache.GetAsync<Entry>(key)).Should().BeNull();
    }
}
