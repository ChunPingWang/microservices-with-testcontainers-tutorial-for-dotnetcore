using ProductService.Domain.Ports.Outbound;

namespace ProductService.Domain.Ports.Inbound;

public sealed record SearchProductRequest(string? Keyword, int Page = 1, int PageSize = 20);

public interface ISearchProductUseCase
{
    Task<SearchResult<ProductSearchDoc>> ExecuteAsync(
        SearchProductRequest request, CancellationToken ct = default);
}
