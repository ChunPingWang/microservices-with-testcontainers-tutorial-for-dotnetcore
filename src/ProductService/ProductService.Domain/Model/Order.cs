using ProductService.Domain.Events;
using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Domain.Model;

public sealed class Order
{
    private List<OrderLine> _lines;
    private readonly List<OrderEvent> _domainEvents = [];

    public OrderId Id { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;
    public Money Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderEvent> DomainEvents => _domainEvents;

    // EF Core materialisation
    private Order()
    {
        _lines = [];
        Total = Money.Zero("TWD");
        Status = new OrderStatus.Created(DateTime.UtcNow);
    }

    private Order(OrderId id, CustomerId customerId, List<OrderLine> lines,
        Money total, OrderStatus status)
    {
        Id = id;
        CustomerId = customerId;
        _lines = lines;
        Total = total;
        Status = status;
    }

    public static Order Place(CustomerId customerId, IEnumerable<OrderLine> lines,
        Func<DateTime>? clock = null)
    {
        var now = (clock ?? (() => DateTime.UtcNow))();
        var lineList = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));
        if (lineList.Count == 0)
            throw new DomainException("Order must contain at least one line.");

        var currency = lineList[0].UnitPrice.Currency;
        var total = lineList.Aggregate(Money.Zero(currency), (acc, l) => acc + l.LineTotal);

        var id = OrderId.New();
        var order = new Order(id, customerId, lineList, total, new OrderStatus.Created(now));
        order._domainEvents.Add(new OrderEvent.OrderCreated(id, customerId, lineList, total, now));
        return order;
    }

    public static Order Restore(OrderId id, CustomerId customerId,
        IReadOnlyList<OrderLine> lines, Money total, OrderStatus status)
        => new(id, customerId, [.. lines], total, status);

    public void MarkPaid(PaymentId paymentId, Func<DateTime>? clock = null)
    {
        if (Status is not OrderStatus.Created)
            throw new DomainException(
                $"Only CREATED orders can be paid. Current status: {Status.Name}.");

        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new OrderStatus.Paid(now, paymentId);
        _domainEvents.Add(new OrderEvent.OrderPaid(Id, paymentId, now));
    }

    public void MarkCompleted(Func<DateTime>? clock = null)
    {
        if (Status is not OrderStatus.Paid)
            throw new DomainException(
                $"Only PAID orders can be completed. Current status: {Status.Name}.");

        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new OrderStatus.Completed(now);
        _domainEvents.Add(new OrderEvent.OrderCompleted(Id, now));
    }

    public void Cancel(string reason, Func<DateTime>? clock = null)
    {
        if (Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Refunded)
            throw new DomainException(
                $"Cannot cancel order in status {Status.Name}.");

        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new OrderStatus.Cancelled(now, reason);
        _domainEvents.Add(new OrderEvent.OrderCancelled(Id, reason, now));
    }

    public void Refund(string reason, Func<DateTime>? clock = null)
    {
        if (Status is not OrderStatus.Paid)
            throw new DomainException(
                $"Only PAID orders can be refunded. Current status: {Status.Name}.");

        var now = (clock ?? (() => DateTime.UtcNow))();
        Status = new OrderStatus.Refunded(now, reason);
        _domainEvents.Add(new OrderEvent.OrderRefunded(Id, reason, now));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
