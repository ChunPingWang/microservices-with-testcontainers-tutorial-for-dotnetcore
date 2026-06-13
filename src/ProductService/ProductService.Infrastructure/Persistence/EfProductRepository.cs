using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Infrastructure.Persistence;

public sealed class EfProductRepository(ProductDbContext db) : IProductRepository
{
    public Task<Product?> GetAsync(ProductId id, CancellationToken ct = default)
        => db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetManyAsync(
        IReadOnlyCollection<ProductId> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        var rawIds = ids.Select(i => i.Value).ToArray();
        return await db.Products
            .Where(p => rawIds.Contains(p.Id.Value))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await db.Products.AddAsync(product, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        db.Products.Update(product);
        return db.SaveChangesAsync(ct);
    }
}
