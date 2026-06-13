using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Model;
using PaymentService.Domain.Ports;

namespace PaymentService.Infrastructure.Notification;

public sealed class PubSubNotificationAdapter(
    PublisherClient publisher,
    ILogger<PubSubNotificationAdapter> logger) : INotificationPort
{
    public async Task NotifyPaymentCompletedAsync(PaymentId paymentId, OrderId orderId,
        string recipient, CancellationToken ct = default)
    {
        var payload = $$"""
        {"type":"payment.completed","paymentId":"{{paymentId}}","orderId":"{{orderId}}","to":"{{recipient}}"}
        """;
        var msg = new PubsubMessage
        {
            Data = ByteString.CopyFromUtf8(payload),
            Attributes = { ["type"] = "payment.completed" }
        };
        var messageId = await publisher.PublishAsync(msg);
        logger.LogInformation("Published Pub/Sub message {Id} for payment {PaymentId}",
            messageId, paymentId);
    }
}

public sealed class LogNotificationAdapter(ILogger<LogNotificationAdapter> logger) : INotificationPort
{
    public Task NotifyPaymentCompletedAsync(PaymentId paymentId, OrderId orderId,
        string recipient, CancellationToken ct = default)
    {
        logger.LogInformation("[Notify] payment {PaymentId} order {OrderId} → {Recipient}",
            paymentId, orderId, recipient);
        return Task.CompletedTask;
    }
}
