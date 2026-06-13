using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Application.Queries;

public sealed record ProductDto(Guid Id, string Name, string Description,
    decimal Price, string Currency);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record SearchProductQuery(string? Keyword, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ProductDto>>;

public sealed class SearchProductQueryHandler(
    ISearchPort search,
    ICachePort cache,
    ILogger<SearchProductQueryHandler> logger)
    : IRequestHandler<SearchProductQuery, PagedResult<ProductDto>>
{
    public async Task<PagedResult<ProductDto>> Handle(SearchProductQuery q, CancellationToken ct)
    {
        var cacheKey = $"product-search:{q.Keyword}:{q.Page}:{q.PageSize}";
        var cached = await cache.GetAsync<PagedResult<ProductDto>>(cacheKey, ct);
        if (cached is not null)
        {
            logger.LogDebug("Cache hit for {Key}", cacheKey);
            return cached;
        }

        var es = await search.SearchAsync(q.Keyword, q.Page, q.PageSize, ct);
        var items = es.Items.Select(d => new ProductDto(d.Id, d.Name, d.Description,
            d.Price, d.Currency)).ToList();
        var result = new PagedResult<ProductDto>(items, es.Total, es.Page, es.PageSize);
        await cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2), ct);
        return result;
    }
}
