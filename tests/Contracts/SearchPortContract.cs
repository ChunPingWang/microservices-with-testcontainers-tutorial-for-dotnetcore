using FluentAssertions;
using ProductService.Domain.Ports.Outbound;
using Xunit;

namespace Contracts;

/// <summary>
/// Behaviour every <see cref="ISearchPort"/> implementation must satisfy.
/// Both in-memory and Elasticsearch implementations should index documents,
/// search by keyword, paginate, and delete by id.
/// </summary>
public abstract class SearchPortContract
{
    protected abstract Task<ISearchPort> CreateAsync();

    /// <summary>
    /// Optional hook for slow back-ends (Elasticsearch) that need to wait for
    /// the index to refresh before search results are visible.
    /// </summary>
    protected virtual Task WaitForIndexAsync(ISearchPort port, Func<Task<bool>> condition)
        => condition().ContinueWith(t => { if (!t.Result) throw new InvalidOperationException("Index not visible."); });

    [Fact]
    [Trait("Category", "Contract")]
    public async Task IndexThenSearch_FindsByKeyword()
    {
        var port = await CreateAsync();
        await port.EnsureIndexAsync();

        var iphone = new ProductSearchDoc(Guid.NewGuid(), "iPhone 16", "Apple flagship", 35000m, "TWD", true);
        var galaxy = new ProductSearchDoc(Guid.NewGuid(), "Galaxy S25", "Samsung flagship", 32000m, "TWD", true);
        await port.IndexAsync(iphone);
        await port.IndexAsync(galaxy);

        SearchResult<ProductSearchDoc>? result = null;
        await WaitForIndexAsync(port, async () =>
        {
            result = await port.SearchAsync("iPhone", 1, 10);
            return result.Items.Any(d => d.Id == iphone.Id);
        });
        result!.Items.Should().Contain(d => d.Id == iphone.Id);
        result.Items.Should().NotContain(d => d.Id == galaxy.Id);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Delete_RemovesFromSearch()
    {
        var port = await CreateAsync();
        await port.EnsureIndexAsync();

        var doc = new ProductSearchDoc(Guid.NewGuid(), "DeleteMe", "to be removed", 1m, "TWD", true);
        await port.IndexAsync(doc);

        await WaitForIndexAsync(port, async () =>
        {
            var r = await port.SearchAsync("DeleteMe", 1, 10);
            return r.Items.Any(d => d.Id == doc.Id);
        });

        await port.DeleteAsync(doc.Id);

        await WaitForIndexAsync(port, async () =>
        {
            var r = await port.SearchAsync("DeleteMe", 1, 10);
            return !r.Items.Any(d => d.Id == doc.Id);
        });
    }
}
