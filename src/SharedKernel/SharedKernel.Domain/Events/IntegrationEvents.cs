namespace SharedKernel.Domain.Events;

public sealed record OrderCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid OrderId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderLinePayload> Lines) : IIntegrationEvent;

public sealed record OrderLinePayload(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record PaymentCompletedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string ReceiptStorageKey) : IIntegrationEvent;

public sealed record PaymentFailedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid OrderId,
    string Reason) : IIntegrationEvent;

public sealed record InventoryDeductedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid OrderId,
    IReadOnlyList<OrderLinePayload> Lines) : IIntegrationEvent;

public sealed record InventoryDeductionFailedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    Guid OrderId,
    Guid PaymentId,
    string Reason) : IIntegrationEvent;
