using Contracts;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Cache;

namespace ProductService.Application.Tests.Contract;

/// <summary>
/// Run the shared <see cref="CachePortContract"/> against the in-memory
/// implementation that production code uses in Development environments.
/// The Redis counterpart lives in
/// <c>tests/ProductService.Infrastructure.Tests/Contract/RedisCachePortContractTests.cs</c>.
/// </summary>
public class InMemoryCachePortContractTests : CachePortContract
{
    protected override Task<ICachePort> CreateAsync()
        => Task.FromResult<ICachePort>(new InMemoryCachePort());
}
