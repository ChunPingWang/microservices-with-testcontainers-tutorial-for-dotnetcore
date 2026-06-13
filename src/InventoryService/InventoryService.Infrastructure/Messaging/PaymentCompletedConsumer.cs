using InventoryService.Application.Commands;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain.Events;

namespace InventoryService.Infrastructure.Messaging;

public sealed class PaymentCompletedConsumer(
    ILogger<PaymentCompletedConsumer> logger)
    : IConsumer<PaymentCompletedIntegrationEvent>
{
    public Task Consume(ConsumeContext<PaymentCompletedIntegrationEvent> ctx)
    {
        logger.LogInformation("PaymentCompleted received: order {OrderId}", ctx.Message.OrderId);
        return Task.CompletedTask;
    }
}

public sealed class OrderCreatedConsumer(
    IMediator mediator,
    ILogger<OrderCreatedConsumer> logger)
    : IConsumer<OrderCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> ctx)
    {
        var ev = ctx.Message;
        logger.LogInformation("OrderCreated received: {OrderId}, deducting stock", ev.OrderId);
        await mediator.Send(new DeductStockCommand(
            ev.OrderId,
            Guid.Empty,
            [.. ev.Lines.Select(l => new DeductStockLineDto(l.ProductId, l.Quantity))]),
            ctx.CancellationToken);
    }
}
