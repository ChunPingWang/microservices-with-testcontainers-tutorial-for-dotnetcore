using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("orders");

        e.HasKey(o => o.Id);
        e.Property(o => o.Id)
            .HasConversion(v => v.Value, v => new OrderId(v))
            .HasColumnName("id");

        e.Property(o => o.CustomerId)
            .HasConversion(v => v.Value, v => new CustomerId(v))
            .HasColumnName("customer_id");

        e.OwnsOne(o => o.Total, m =>
        {
            m.Property(p => p.Amount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");
            m.Property(p => p.Currency).HasColumnName("total_currency").HasMaxLength(8);
        });

        e.Property<string>("status_name").HasMaxLength(32);
        e.Property<DateTime>("status_at_utc");
        e.Property<Guid?>("status_payment_id");
        e.Property<string?>("status_reason").HasMaxLength(512);

        e.Ignore(o => o.Status);
        e.Ignore(o => o.DomainEvents);

        e.OwnsMany(o => o.Lines, l =>
        {
            l.ToTable("order_lines");
            l.WithOwner().HasForeignKey("order_id");
            l.Property<int>("line_seq").ValueGeneratedOnAdd();
            l.HasKey("line_seq");
            l.Property(x => x.ProductId)
                .HasConversion(v => v.Value, v => new ProductId(v))
                .HasColumnName("product_id");
            l.Property(x => x.Quantity)
                .HasConversion(v => v.Value, v => Quantity.Of(v))
                .HasColumnName("quantity");
            l.OwnsOne(x => x.UnitPrice, m =>
            {
                m.Property(p => p.Amount).HasColumnName("unit_price_amount").HasColumnType("numeric(18,2)");
                m.Property(p => p.Currency).HasColumnName("unit_price_currency").HasMaxLength(8);
            });
        });

        e.Navigation(o => o.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_lines");
    }
}
