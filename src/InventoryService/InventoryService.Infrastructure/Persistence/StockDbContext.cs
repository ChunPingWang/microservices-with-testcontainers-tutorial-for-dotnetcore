using InventoryService.Domain.Model;
using InventoryService.Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharedKernel.Domain.ValueObjects;

namespace InventoryService.Infrastructure.Persistence;

public sealed class StockDbContext(DbContextOptions<StockDbContext> options) : DbContext(options)
{
    public DbSet<Stock> Stocks => Set<Stock>();

    protected override void OnModelCreating(ModelBuilder b)
        => b.ApplyConfiguration(new StockConfiguration());
}

public sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> e)
    {
        e.ToTable("stocks");
        e.HasKey(s => s.Id);
        e.Property(s => s.Id)
            .HasConversion(v => v.Value, v => new StockId(v))
            .HasColumnName("id");
        e.Property(s => s.ProductId)
            .HasConversion(v => v.Value, v => new ProductId(v))
            .HasColumnName("product_id");
        e.HasIndex(s => s.ProductId).IsUnique();

        e.Property(s => s.Available)
            .HasConversion(v => v.Value, v => Quantity.Of(v))
            .HasColumnName("available");
        e.Property(s => s.Reserved)
            .HasConversion(v => v.Value, v => Quantity.Of(v))
            .HasColumnName("reserved");

        e.Property(s => s.Version)
            .HasColumnName("version")
            .IsConcurrencyToken();
    }
}

public sealed class EfStockRepository(StockDbContext db) : IStockWriteRepository
{
    public Task<Stock?> GetAsync(ProductId productId, CancellationToken ct = default)
        => db.Stocks.FirstOrDefaultAsync(s => s.ProductId == productId, ct);

    public async Task<IReadOnlyList<Stock>> GetManyAsync(
        IReadOnlyCollection<ProductId> productIds, CancellationToken ct = default)
    {
        if (productIds.Count == 0) return [];
        var raw = productIds.Select(p => p.Value).ToArray();
        return await db.Stocks.Where(s => raw.Contains(s.ProductId.Value)).ToListAsync(ct);
    }

    public async Task AddAsync(Stock stock, CancellationToken ct = default)
    {
        await db.Stocks.AddAsync(stock, ct);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Stock stock, CancellationToken ct = default)
    {
        db.Stocks.Update(stock);
        // increment optimistic concurrency token
        var entry = db.Entry(stock);
        entry.Property(s => s.Version).CurrentValue =
            (uint)(entry.Property(s => s.Version).OriginalValue + 1);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

public static class StockSchemaInitialiser
{
    public static Task EnsureCreatedAsync(StockDbContext db, CancellationToken ct = default)
        => db.Database.EnsureCreatedAsync(ct);
}
