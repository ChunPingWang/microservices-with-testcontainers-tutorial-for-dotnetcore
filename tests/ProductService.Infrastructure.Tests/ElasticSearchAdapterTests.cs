using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using FluentAssertions;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Search;
using TestInfrastructure;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class ElasticSearchAdapterTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Index_AndSearch_MatchesByKeyword()
    {
        var url = $"http://{containers.Elasticsearch.Hostname}:{containers.Elasticsearch.GetMappedPublicPort(9200)}";
        var settings = new ElasticsearchClientSettings(new Uri(url))
            .DisableAutomaticProxyDetection();
        var es = new ElasticsearchClient(settings);
        var adapter = new ElasticSearchAdapter(es);

        await adapter.EnsureIndexAsync();

        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await adapter.IndexAsync(new ProductSearchDoc(ids[0], "iPhone 16", "Apple flagship", 35000m, "TWD", true));
        await adapter.IndexAsync(new ProductSearchDoc(ids[1], "Galaxy S25", "Samsung flagship", 32000m, "TWD", true));

        SearchResult<ProductSearchDoc>? result = null;
        await AsyncWaiter.WaitUntilAsync(async _ =>
        {
            result = await adapter.SearchAsync("iPhone", 1, 10);
            return result.Items.Any(d => d.Id == ids[0]);
        }, TimeSpan.FromSeconds(30));

        result!.Items.Should().ContainSingle(d => d.Id == ids[0]);
    }
}
