using PaymentService.Domain.Model;
using SharedKernel.Domain.ValueObjects;

namespace PaymentService.Domain.Events;

public abstract record PaymentEvent(DateTime OccurredAtUtc)
{
    public sealed record PaymentInitiated(
        PaymentId PaymentId, OrderId OrderId, Money Amount, DateTime OccurredAtUtc)
        : PaymentEvent(OccurredAtUtc);

    public sealed record PaymentCompleted(
        PaymentId PaymentId, OrderId OrderId, Money Amount, string ReceiptKey, DateTime OccurredAtUtc)
        : PaymentEvent(OccurredAtUtc);

    public sealed record PaymentFailed(
        PaymentId PaymentId, OrderId OrderId, string Reason, DateTime OccurredAtUtc)
        : PaymentEvent(OccurredAtUtc);

    public sealed record PaymentRefunded(
        PaymentId PaymentId, OrderId OrderId, string Reason, DateTime OccurredAtUtc)
        : PaymentEvent(OccurredAtUtc);
}
