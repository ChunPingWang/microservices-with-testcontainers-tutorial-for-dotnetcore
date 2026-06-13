using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Domain.Events;

public abstract record OrderEvent(DateTime OccurredAtUtc)
{
    public sealed record OrderCreated(
        OrderId OrderId,
        CustomerId CustomerId,
        IReadOnlyList<OrderLine> Lines,
        Money Total,
        DateTime OccurredAtUtc) : OrderEvent(OccurredAtUtc);

    public sealed record OrderPaid(
        OrderId OrderId,
        PaymentId PaymentId,
        DateTime OccurredAtUtc) : OrderEvent(OccurredAtUtc);

    public sealed record OrderCompleted(
        OrderId OrderId,
        DateTime OccurredAtUtc) : OrderEvent(OccurredAtUtc);

    public sealed record OrderCancelled(
        OrderId OrderId,
        string Reason,
        DateTime OccurredAtUtc) : OrderEvent(OccurredAtUtc);

    public sealed record OrderRefunded(
        OrderId OrderId,
        string Reason,
        DateTime OccurredAtUtc) : OrderEvent(OccurredAtUtc);
}
