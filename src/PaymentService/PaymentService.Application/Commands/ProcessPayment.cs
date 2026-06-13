using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Events;
using PaymentService.Domain.Model;
using PaymentService.Domain.Ports;
using SharedKernel.Domain.Events;
using SharedKernel.Domain.Ports;
using SharedKernel.Domain.ValueObjects;

namespace PaymentService.Application.Commands;

public sealed record ProcessPaymentCommand(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string IdempotencyKey) : IRequest<Guid>;

public sealed class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEqual(Guid.Empty);
        RuleFor(c => c.Amount).GreaterThan(0);
        RuleFor(c => c.Currency).NotEmpty().Length(3);
        RuleFor(c => c.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

public sealed class ProcessPaymentCommandHandler(
    IPaymentWriteRepository payments,
    IReceiptStorage receipts,
    IEventPublisher publisher,
    INotificationPort notification,
    ISecretProvider secrets,
    TimeProvider clock,
    ILogger<ProcessPaymentCommandHandler> logger)
    : IRequestHandler<ProcessPaymentCommand, Guid>
{
    public async Task<Guid> Handle(ProcessPaymentCommand cmd, CancellationToken ct)
    {
        var idem = IdempotencyKey.Of(cmd.IdempotencyKey);
        var existing = await payments.FindByIdempotencyAsync(idem, ct);
        if (existing is not null)
        {
            logger.LogInformation("Idempotent replay for {Idem} → payment {Id}",
                cmd.IdempotencyKey, existing.Id);
            return existing.Id.Value;
        }

        // Demonstrate Vault: fetch external PSP key (not actually used here, but verified accessible)
        try { await secrets.GetSecretAsync("kv/payment", "psp_api_key", ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Secret unavailable, continuing in demo mode"); }

        var payment = Payment.Create(
            new OrderId(cmd.OrderId),
            new Money(cmd.Amount, cmd.Currency),
            idem,
            () => clock.GetUtcNow().UtcDateTime);

        await payments.AddAsync(payment, ct);

        // Simulate PSP call success and persist receipt
        var receiptBytes = Encoding.UTF8.GetBytes(
            $"RECEIPT\norder={cmd.OrderId}\namount={cmd.Amount} {cmd.Currency}\nat={clock.GetUtcNow():O}");
        using var ms = new MemoryStream(receiptBytes);
        var receiptKey = await receipts.StoreAsync(ms, "text/plain", ct);

        payment.MarkCompleted(receiptKey, () => clock.GetUtcNow().UtcDateTime);
        await payments.UpdateAsync(payment, ct);

        // Publish integration event
        var integration = new PaymentCompletedIntegrationEvent(
            EventId: Guid.NewGuid(),
            OccurredAtUtc: clock.GetUtcNow().UtcDateTime,
            PaymentId: payment.Id.Value,
            OrderId: cmd.OrderId,
            Amount: cmd.Amount,
            Currency: cmd.Currency,
            ReceiptStorageKey: receiptKey);
        await publisher.PublishAsync(integration, ct);

        await notification.NotifyPaymentCompletedAsync(
            payment.Id, payment.OrderId, $"customer-{cmd.OrderId}@example.com", ct);

        foreach (var ev in payment.DomainEvents) logger.LogDebug("Domain event: {Event}", ev);
        payment.ClearDomainEvents();

        return payment.Id.Value;
    }
}

public sealed record RefundPaymentByOrderCommand(Guid OrderId, string Reason) : IRequest;

public sealed class RefundPaymentByOrderCommandHandler(
    IPaymentWriteRepository payments,
    IEventPublisher publisher,
    TimeProvider clock,
    ILogger<RefundPaymentByOrderCommandHandler> logger)
    : IRequestHandler<RefundPaymentByOrderCommand>
{
    public async Task Handle(RefundPaymentByOrderCommand cmd, CancellationToken ct)
    {
        var p = await payments.FindByOrderAsync(new OrderId(cmd.OrderId), ct);
        if (p is null)
        {
            logger.LogWarning("Refund requested for unknown order {OrderId}", cmd.OrderId);
            return;
        }
        if (p.Status is not PaymentStatus.Completed)
        {
            logger.LogWarning("Cannot refund payment {Id} in status {Status}", p.Id, p.Status.Name);
            return;
        }
        p.Refund(cmd.Reason, () => clock.GetUtcNow().UtcDateTime);
        await payments.UpdateAsync(p, ct);

        await publisher.PublishAsync(new PaymentFailedIntegrationEvent(
            Guid.NewGuid(), clock.GetUtcNow().UtcDateTime, cmd.OrderId, cmd.Reason), ct);
    }
}
