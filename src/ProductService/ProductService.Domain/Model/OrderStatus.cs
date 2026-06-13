using ProductService.Domain.Model.ValueObjects;

namespace ProductService.Domain.Model;

public abstract record OrderStatus
{
    private OrderStatus() { }

    public sealed record Created(DateTime AtUtc) : OrderStatus;
    public sealed record Paid(DateTime AtUtc, PaymentId PaymentId) : OrderStatus;
    public sealed record Completed(DateTime AtUtc) : OrderStatus;
    public sealed record Cancelled(DateTime AtUtc, string Reason) : OrderStatus;
    public sealed record Refunded(DateTime AtUtc, string Reason) : OrderStatus;

    public string Name => GetType().Name;
}
