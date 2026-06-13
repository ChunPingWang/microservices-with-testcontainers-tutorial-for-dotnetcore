using InventoryService.Domain.Model;

namespace InventoryService.Domain.Ports;

public interface IStockWriteRepository
{
    Task<Stock?> GetAsync(ProductId productId, CancellationToken ct = default);
    Task<IReadOnlyList<Stock>> GetManyAsync(IReadOnlyCollection<ProductId> productIds,
        CancellationToken ct = default);
    Task AddAsync(Stock stock, CancellationToken ct = default);
    Task UpdateAsync(Stock stock, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IDistributedLock
{
    Task<IAsyncDisposable> AcquireAsync(string resource, TimeSpan expiry,
        TimeSpan acquireTimeout, CancellationToken ct = default);
}
