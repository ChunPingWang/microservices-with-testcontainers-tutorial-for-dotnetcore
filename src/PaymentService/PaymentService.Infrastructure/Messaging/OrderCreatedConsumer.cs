using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.Commands;
using SharedKernel.Domain.Events;

namespace PaymentService.Infrastructure.Messaging;

public sealed class OrderCreatedConsumer(
    IMediator mediator,
    ILogger<OrderCreatedConsumer> logger)
    : IConsumer<OrderCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> ctx)
    {
        var ev = ctx.Message;
        logger.LogInformation("OrderCreated received: {OrderId}, processing payment", ev.OrderId);
        await mediator.Send(new ProcessPaymentCommand(
            ev.OrderId, ev.TotalAmount, ev.Currency,
            IdempotencyKey: $"order-{ev.OrderId}"), ctx.CancellationToken);
    }
}

public sealed class InventoryDeductionFailedConsumer(
    IMediator mediator,
    ILogger<InventoryDeductionFailedConsumer> logger)
    : IConsumer<InventoryDeductionFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryDeductionFailedIntegrationEvent> ctx)
    {
        var ev = ctx.Message;
        logger.LogWarning("InventoryDeductionFailed → refunding order {OrderId}", ev.OrderId);
        await mediator.Send(new RefundPaymentByOrderCommand(ev.OrderId, ev.Reason),
            ctx.CancellationToken);
    }
}
