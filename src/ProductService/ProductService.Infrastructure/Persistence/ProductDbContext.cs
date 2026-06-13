using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Infrastructure.Persistence.Configurations;

namespace ProductService.Infrastructure.Persistence;

public sealed class ProductDbContext(DbContextOptions<ProductDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfiguration(new OrderConfiguration());
        b.ApplyConfiguration(new ProductConfiguration());
    }
}
