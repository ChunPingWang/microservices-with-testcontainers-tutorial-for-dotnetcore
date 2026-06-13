using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentService.Domain.Model;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
        => b.ApplyConfiguration(new PaymentConfiguration());
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.ToTable("payments");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id)
            .HasConversion(v => v.Value, v => new PaymentId(v))
            .HasColumnName("id");

        e.Property(p => p.OrderId)
            .HasConversion(v => v.Value, v => new OrderId(v))
            .HasColumnName("order_id");

        e.Property(p => p.Idempotency)
            .HasConversion(v => v.Value, v => IdempotencyKey.Of(v))
            .HasColumnName("idempotency_key")
            .HasMaxLength(128);
        e.HasIndex(p => p.Idempotency).IsUnique();
        e.HasIndex(p => p.OrderId);

        e.OwnsOne(p => p.Amount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
            m.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(8);
        });

        e.Ignore(p => p.Status);
        e.Ignore(p => p.DomainEvents);
        e.Property<string>("status_name").HasMaxLength(32);
        e.Property<DateTime>("status_at_utc");
        e.Property<string?>("status_receipt_key").HasMaxLength(512);
        e.Property<string?>("status_reason").HasMaxLength(512);
    }
}
