using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;

namespace ProductService.Domain.Ports.Outbound;

public interface IOrderWriteRepository
{
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task<Order?> FindAsync(OrderId id, CancellationToken ct = default);
}

public interface IOrderReadRepository
{
    Task<Order?> GetAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId,
        CancellationToken ct = default);
}

public interface IProductRepository
{
    Task<Product?> GetAsync(ProductId id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetManyAsync(IReadOnlyCollection<ProductId> ids,
        CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
