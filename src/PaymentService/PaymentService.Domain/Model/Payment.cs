using PaymentService.Domain.Events;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace PaymentService.Domain.Model;

public sealed class Payment
{
    private readonly List<PaymentEvent> _domainEvents = [];

    public PaymentId Id { get; private set; }
    public OrderId OrderId { get; private set; }
    public Money Amount { get; private set; }
    public IdempotencyKey Idempotency { get; private set; }
    public PaymentStatus Status { get; private set; }
    public IReadOnlyList<PaymentEvent> DomainEvents => _domainEvents;

    // EF Core materialisation
    private Payment()
    {
        Amount = Money.Zero("TWD");
        Idempotency = new IdempotencyKey("");
        Status = new PaymentStatus.Pending(DateTime.UtcNow);
    }

    private Payment(PaymentId id, OrderId orderId, Money amount, IdempotencyKey idem,
        PaymentStatus status)
    {
        Id = id;
        OrderId = orderId;
        Amount = amount;
        Idempotency = idem;
        Status = status;
    }

    public static Payment Create(OrderId orderId, Money amount, IdempotencyKey idem,
        Func<DateTime>? clock = null)
    {
        if (amount.Amount <= 0)
            throw new DomainException("Payment amount must be positive.");
        var now = (clock ?? (() => DateTime.UtcNow))();
        var p = new Payment(PaymentId.New(), orderId, amount, idem, new PaymentStatus.Pending(now));
        p._domainEvents.Add(new PaymentEvent.PaymentInitiated(p.Id, orderId, amount, now));
        return p;
    }

    public static Payment Restore(PaymentId id, OrderId orderId, Money amount,
        IdempotencyKey idem, PaymentStatus status)
        => new(id, orderId, amount, idem, status);

    public void MarkCompleted(string receiptKey, Func<DateTime>? clock = null)
    {
        if (Status is not PaymentStatus.Pending)
            throw new DomainException(
                $"Only PENDING payments can be completed. Current: {Status.Name}");
        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new PaymentStatus.Completed(now, receiptKey);
        _domainEvents.Add(new PaymentEvent.PaymentCompleted(Id, OrderId, Amount, receiptKey, now));
    }

    public void MarkFailed(string reason, Func<DateTime>? clock = null)
    {
        if (Status is not PaymentStatus.Pending)
            throw new DomainException(
                $"Only PENDING payments can be failed. Current: {Status.Name}");
        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new PaymentStatus.Failed(now, reason);
        _domainEvents.Add(new PaymentEvent.PaymentFailed(Id, OrderId, reason, now));
    }

    public void Refund(string reason, Func<DateTime>? clock = null)
    {
        if (Status is not PaymentStatus.Completed)
            throw new DomainException(
                $"Only COMPLETED payments can be refunded. Current: {Status.Name}");
        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new PaymentStatus.Refunded(now, reason);
        _domainEvents.Add(new PaymentEvent.PaymentRefunded(Id, OrderId, reason, now));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
