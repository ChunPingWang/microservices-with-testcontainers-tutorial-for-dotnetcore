using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Infrastructure.Persistence;

public sealed class EfOrderRepository(ProductDbContext db)
    : IOrderWriteRepository, IOrderReadRepository
{
    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await db.Orders.AddAsync(order, ct);
        WriteStatusShadow(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        db.Orders.Update(order);
        WriteStatusShadow(order);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Order?> FindAsync(OrderId id, CancellationToken ct = default)
    {
        var order = await db.Orders.FindAsync([id], ct);
        if (order is not null) await db.Entry(order).Collection("Lines").LoadAsync(ct);
        if (order is not null) RestoreStatusFromShadow(order);
        return order;
    }

    Task<Order?> IOrderReadRepository.GetAsync(OrderId id, CancellationToken ct)
        => FindAsync(id, ct);

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId,
        CancellationToken ct = default)
    {
        var orders = await db.Orders
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(ct);
        foreach (var o in orders) RestoreStatusFromShadow(o);
        return orders;
    }

    private void WriteStatusShadow(Order order)
    {
        var entry = db.Entry(order);
        entry.Property("status_name").CurrentValue = order.Status.Name;
        entry.Property("status_at_utc").CurrentValue = order.Status switch
        {
            OrderStatus.Created c => c.AtUtc,
            OrderStatus.Paid p => p.AtUtc,
            OrderStatus.Completed cm => cm.AtUtc,
            OrderStatus.Cancelled cn => cn.AtUtc,
            OrderStatus.Refunded r => r.AtUtc,
            _ => DateTime.UtcNow
        };
        entry.Property("status_payment_id").CurrentValue =
            order.Status is OrderStatus.Paid p2 ? (Guid?)p2.PaymentId.Value : null;
        entry.Property("status_reason").CurrentValue = order.Status switch
        {
            OrderStatus.Cancelled cn => cn.Reason,
            OrderStatus.Refunded r => r.Reason,
            _ => null
        };
    }

    private void RestoreStatusFromShadow(Order order)
    {
        var entry = db.Entry(order);
        var name = (string?)entry.Property("status_name").CurrentValue;
        var at = (DateTime)entry.Property("status_at_utc").CurrentValue!;
        var paymentId = (Guid?)entry.Property("status_payment_id").CurrentValue;
        var reason = (string?)entry.Property("status_reason").CurrentValue;

        var rebuilt = Order.Restore(order.Id, order.CustomerId, order.Lines, order.Total,
            name switch
            {
                nameof(OrderStatus.Created) => new OrderStatus.Created(at),
                nameof(OrderStatus.Paid) => new OrderStatus.Paid(at, new PaymentId(paymentId!.Value)),
                nameof(OrderStatus.Completed) => new OrderStatus.Completed(at),
                nameof(OrderStatus.Cancelled) => new OrderStatus.Cancelled(at, reason ?? ""),
                nameof(OrderStatus.Refunded) => new OrderStatus.Refunded(at, reason ?? ""),
                _ => new OrderStatus.Created(at)
            });
        // Apply the rebuilt status using internal mechanism — set via private field copy.
        typeof(Order).GetProperty(nameof(Order.Status))!
            .SetValue(order, rebuilt.Status);
    }
}
