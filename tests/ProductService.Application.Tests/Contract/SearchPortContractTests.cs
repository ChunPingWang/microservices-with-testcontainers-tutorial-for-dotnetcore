using Contracts;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Search;

namespace ProductService.Application.Tests.Contract;

/// <summary>
/// Run the shared <see cref="SearchPortContract"/> against the in-memory search
/// implementation. The Elasticsearch counterpart lives in
/// <c>tests/ProductService.Infrastructure.Tests/Contract/ElasticSearchPortContractTests.cs</c>.
/// </summary>
public class InMemorySearchPortContractTests : SearchPortContract
{
    protected override Task<ISearchPort> CreateAsync()
        => Task.FromResult<ISearchPort>(new InMemorySearchAdapter());
}
