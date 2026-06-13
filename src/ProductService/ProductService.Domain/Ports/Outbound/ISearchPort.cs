namespace ProductService.Domain.Ports.Outbound;

public sealed record ProductSearchDoc(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    bool IsActive);

public sealed record SearchResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

public interface ISearchPort
{
    Task IndexAsync(ProductSearchDoc doc, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<SearchResult<ProductSearchDoc>> SearchAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default);
    Task EnsureIndexAsync(CancellationToken ct = default);
}
