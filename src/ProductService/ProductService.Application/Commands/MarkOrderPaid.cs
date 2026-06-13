using MediatR;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using SharedKernel.Domain;

namespace ProductService.Application.Commands;

public sealed record MarkOrderPaidCommand(Guid OrderId, Guid PaymentId) : IRequest;

public sealed class MarkOrderPaidCommandHandler(IOrderWriteRepository orders, TimeProvider clock)
    : IRequestHandler<MarkOrderPaidCommand>
{
    public async Task Handle(MarkOrderPaidCommand cmd, CancellationToken ct)
    {
        var order = await orders.FindAsync(new OrderId(cmd.OrderId), ct)
                    ?? throw new DomainException($"Order {cmd.OrderId} not found.");
        order.MarkPaid(new PaymentId(cmd.PaymentId), () => clock.GetUtcNow().UtcDateTime);
        await orders.UpdateAsync(order, ct);
    }
}

public sealed record CompleteOrderCommand(Guid OrderId) : IRequest;

public sealed class CompleteOrderCommandHandler(
    IOrderWriteRepository orders,
    ICachePort cache,
    TimeProvider clock)
    : IRequestHandler<CompleteOrderCommand>
{
    public async Task Handle(CompleteOrderCommand cmd, CancellationToken ct)
    {
        var order = await orders.FindAsync(new OrderId(cmd.OrderId), ct)
                    ?? throw new DomainException($"Order {cmd.OrderId} not found.");
        order.MarkCompleted(() => clock.GetUtcNow().UtcDateTime);
        await orders.UpdateAsync(order, ct);
        await cache.RemoveAsync($"order:{order.Id.Value}", ct);
    }
}

public sealed record RefundOrderCommand(Guid OrderId, string Reason) : IRequest;

public sealed class RefundOrderCommandHandler(IOrderWriteRepository orders, TimeProvider clock)
    : IRequestHandler<RefundOrderCommand>
{
    public async Task Handle(RefundOrderCommand cmd, CancellationToken ct)
    {
        var order = await orders.FindAsync(new OrderId(cmd.OrderId), ct)
                    ?? throw new DomainException($"Order {cmd.OrderId} not found.");
        order.Refund(cmd.Reason, () => clock.GetUtcNow().UtcDateTime);
        await orders.UpdateAsync(order, ct);
    }
}
