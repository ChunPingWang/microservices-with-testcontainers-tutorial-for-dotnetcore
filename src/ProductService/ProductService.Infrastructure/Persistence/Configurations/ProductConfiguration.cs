using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;

namespace ProductService.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.ToTable("products");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id)
            .HasConversion(v => v.Value, v => new ProductId(v))
            .HasColumnName("id");

        e.Property(p => p.Name).HasColumnName("name").HasMaxLength(256).IsRequired();
        e.Property(p => p.Description).HasColumnName("description").HasMaxLength(2048);
        e.Property(p => p.ImageStorageKey).HasColumnName("image_storage_key").HasMaxLength(512);
        e.Property(p => p.IsActive).HasColumnName("is_active");

        e.OwnsOne(p => p.Price, m =>
        {
            m.Property(x => x.Amount).HasColumnName("price_amount").HasColumnType("numeric(18,2)");
            m.Property(x => x.Currency).HasColumnName("price_currency").HasMaxLength(8);
        });
    }
}
