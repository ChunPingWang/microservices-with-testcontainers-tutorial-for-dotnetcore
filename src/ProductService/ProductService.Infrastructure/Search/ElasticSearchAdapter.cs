using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Infrastructure.Search;

public sealed class ElasticSearchAdapter(ElasticsearchClient es) : ISearchPort
{
    public const string IndexName = "products";

    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        var exists = await es.Indices.ExistsAsync(IndexName, ct);
        if (!exists.Exists)
            await es.Indices.CreateAsync(IndexName, ct);
    }

    public async Task IndexAsync(ProductSearchDoc doc, CancellationToken ct = default)
    {
        var resp = await es.IndexAsync(doc, idx => idx
            .Index(IndexName)
            .Id(doc.Id.ToString())
            .Refresh(Elastic.Clients.Elasticsearch.Refresh.WaitFor), ct);
        if (!resp.IsValidResponse)
            throw new InvalidOperationException(
                $"Elasticsearch index failed: {resp.DebugInformation}");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await es.DeleteAsync<ProductSearchDoc>(IndexName, id.ToString(), ct);
    }

    public async Task<SearchResult<ProductSearchDoc>> SearchAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        var from = Math.Max(0, (page - 1) * pageSize);

        var resp = await es.SearchAsync<ProductSearchDoc>(s => s
            .Indices(IndexName)
            .From(from)
            .Size(pageSize)
            .Query(q =>
            {
                if (string.IsNullOrWhiteSpace(keyword))
                    q.MatchAll(_ => { });
                else
                    q.MultiMatch(m => m
                        .Query(keyword)
                        .Fields(new[] { "name^2", "description" }));
            }), ct);

        var docs = resp.Documents?.ToList() ?? [];
        var total = (int?)resp.Total ?? docs.Count;
        return new SearchResult<ProductSearchDoc>(docs, total, page, pageSize);
    }
}

public sealed class InMemorySearchAdapter : ISearchPort
{
    private readonly Dictionary<Guid, ProductSearchDoc> _store = new();

    public Task EnsureIndexAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task IndexAsync(ProductSearchDoc doc, CancellationToken ct = default)
    {
        _store[doc.Id] = doc;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task<SearchResult<ProductSearchDoc>> SearchAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        IEnumerable<ProductSearchDoc> q = _store.Values;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.ToLowerInvariant();
            q = q.Where(d =>
                d.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                d.Description.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }
        var arr = q.ToList();
        var paged = arr.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new SearchResult<ProductSearchDoc>(paged, arr.Count, page, pageSize));
    }
}
