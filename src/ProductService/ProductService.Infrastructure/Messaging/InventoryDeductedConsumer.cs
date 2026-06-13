using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using ProductService.Application.Commands;
using SharedKernel.Domain.Events;

namespace ProductService.Infrastructure.Messaging;

public sealed class InventoryDeductedConsumer(
    IMediator mediator,
    ILogger<InventoryDeductedConsumer> logger)
    : IConsumer<InventoryDeductedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<InventoryDeductedIntegrationEvent> ctx)
    {
        var ev = ctx.Message;
        logger.LogInformation("InventoryDeducted received for order {OrderId}", ev.OrderId);
        await mediator.Send(new CompleteOrderCommand(ev.OrderId), ctx.CancellationToken);
    }
}

public sealed class PaymentCompletedConsumer(
    IMediator mediator,
    ILogger<PaymentCompletedConsumer> logger)
    : IConsumer<PaymentCompletedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> ctx)
    {
        var ev = ctx.Message;
        logger.LogInformation("PaymentCompleted received: order {OrderId}, payment {PaymentId}",
            ev.OrderId, ev.PaymentId);
        await mediator.Send(new MarkOrderPaidCommand(ev.OrderId, ev.PaymentId),
            ctx.CancellationToken);
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
        logger.LogWarning("InventoryDeductionFailed: order {OrderId}, reason {Reason}",
            ev.OrderId, ev.Reason);
        await mediator.Send(new RefundOrderCommand(ev.OrderId, ev.Reason),
            ctx.CancellationToken);
    }
}
