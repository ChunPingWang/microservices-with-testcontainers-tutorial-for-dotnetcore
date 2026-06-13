using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Model;
using PaymentService.Domain.Ports;

namespace PaymentService.Infrastructure.Persistence;

public sealed class EfPaymentRepository(PaymentDbContext db) : IPaymentWriteRepository
{
    public async Task AddAsync(Payment payment, CancellationToken ct = default)
    {
        await db.Payments.AddAsync(payment, ct);
        WriteShadow(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        db.Payments.Update(payment);
        WriteShadow(payment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Payment?> FindByIdempotencyAsync(IdempotencyKey key, CancellationToken ct = default)
    {
        var p = await db.Payments.FirstOrDefaultAsync(x => x.Idempotency == key, ct);
        if (p is not null) RestoreShadow(p);
        return p;
    }

    public async Task<Payment?> FindAsync(PaymentId id, CancellationToken ct = default)
    {
        var p = await db.Payments.FindAsync([id], ct);
        if (p is not null) RestoreShadow(p);
        return p;
    }

    public async Task<Payment?> FindByOrderAsync(OrderId orderId, CancellationToken ct = default)
    {
        var p = await db.Payments.FirstOrDefaultAsync(x => x.OrderId == orderId, ct);
        if (p is not null) RestoreShadow(p);
        return p;
    }

    private void WriteShadow(Payment p)
    {
        var entry = db.Entry(p);
        entry.Property("status_name").CurrentValue = p.Status.Name;
        entry.Property("status_at_utc").CurrentValue = p.Status switch
        {
            PaymentStatus.Pending s => s.AtUtc,
            PaymentStatus.Completed s => s.AtUtc,
            PaymentStatus.Failed s => s.AtUtc,
            PaymentStatus.Refunded s => s.AtUtc,
            _ => DateTime.UtcNow
        };
        entry.Property("status_receipt_key").CurrentValue =
            p.Status is PaymentStatus.Completed c ? c.ReceiptKey : null;
        entry.Property("status_reason").CurrentValue = p.Status switch
        {
            PaymentStatus.Failed f => f.Reason,
            PaymentStatus.Refunded r => r.Reason,
            _ => null
        };
    }

    private void RestoreShadow(Payment p)
    {
        var entry = db.Entry(p);
        var name = (string?)entry.Property("status_name").CurrentValue;
        var at = (DateTime)entry.Property("status_at_utc").CurrentValue!;
        var receipt = (string?)entry.Property("status_receipt_key").CurrentValue;
        var reason = (string?)entry.Property("status_reason").CurrentValue;

        var rebuilt = Payment.Restore(p.Id, p.OrderId, p.Amount, p.Idempotency,
            name switch
            {
                nameof(PaymentStatus.Pending) => new PaymentStatus.Pending(at),
                nameof(PaymentStatus.Completed) => new PaymentStatus.Completed(at, receipt ?? ""),
                nameof(PaymentStatus.Failed) => new PaymentStatus.Failed(at, reason ?? ""),
                nameof(PaymentStatus.Refunded) => new PaymentStatus.Refunded(at, reason ?? ""),
                _ => new PaymentStatus.Pending(at)
            });
        typeof(Payment).GetProperty(nameof(Payment.Status))!.SetValue(p, rebuilt.Status);
    }
}

public static class PaymentSchemaInitialiser
{
    public static Task EnsureCreatedAsync(PaymentDbContext db, CancellationToken ct = default)
        => db.Database.EnsureCreatedAsync(ct);
}
