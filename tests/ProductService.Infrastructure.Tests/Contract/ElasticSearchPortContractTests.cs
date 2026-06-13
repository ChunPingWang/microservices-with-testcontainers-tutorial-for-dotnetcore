using Contracts;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Search;
using TestInfrastructure;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests.Contract;

/// <summary>
/// Same <see cref="SearchPortContract"/> the in-memory tests run, now against
/// a real Elasticsearch container. We override <see cref="WaitForIndexAsync"/>
/// because ES needs a couple hundred milliseconds for refresh to make a freshly
/// indexed document visible to search.
/// </summary>
[Collection("SharedContainers")]
public class ElasticSearchPortContractTests(SharedContainerFixture containers)
    : SearchPortContract
{
    protected override Task<ISearchPort> CreateAsync()
    {
        // ES 8 builder defaults to https; bypass via raw http to the mapped port
        var url = $"http://{containers.Elasticsearch.Hostname}:{containers.Elasticsearch.GetMappedPublicPort(9200)}";
        var settings = new ElasticsearchClientSettings(new Uri(url))
            .DisableAutomaticProxyDetection();
        var es = new ElasticsearchClient(settings);
        return Task.FromResult<ISearchPort>(new ElasticSearchAdapter(es));
    }

    protected override Task WaitForIndexAsync(ISearchPort _, Func<Task<bool>> condition)
        => AsyncWaiter.WaitUntilAsync(async _ => await condition(),
            timeout: TimeSpan.FromSeconds(15),
            interval: TimeSpan.FromMilliseconds(200));
}
