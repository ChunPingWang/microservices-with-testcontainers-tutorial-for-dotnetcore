using PaymentService.Domain.Model;

namespace PaymentService.Domain.Ports;

public interface IPaymentWriteRepository
{
    Task AddAsync(Payment payment, CancellationToken ct = default);
    Task UpdateAsync(Payment payment, CancellationToken ct = default);
    Task<Payment?> FindByIdempotencyAsync(IdempotencyKey key, CancellationToken ct = default);
    Task<Payment?> FindAsync(PaymentId id, CancellationToken ct = default);
    Task<Payment?> FindByOrderAsync(OrderId orderId, CancellationToken ct = default);
}

public interface IReceiptStorage
{
    Task<string> StoreAsync(Stream content, string contentType, CancellationToken ct = default);
}

public interface INotificationPort
{
    Task NotifyPaymentCompletedAsync(PaymentId paymentId, OrderId orderId,
        string recipient, CancellationToken ct = default);
}
