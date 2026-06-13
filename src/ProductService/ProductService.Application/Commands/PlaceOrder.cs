using FluentValidation;
using MediatR;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using ProductService.Domain.Services;
using SharedKernel.Domain;
using SharedKernel.Domain.Events;
using SharedKernel.Domain.Ports;

namespace ProductService.Application.Commands;

public sealed record OrderLineDto(Guid ProductId, int Quantity);

public sealed record PlaceOrderCommand(Guid CustomerId, IReadOnlyList<OrderLineDto> Lines)
    : IRequest<Guid>;

public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEqual(Guid.Empty);
        RuleFor(c => c.Lines).NotEmpty();
        RuleForEach(c => c.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEqual(Guid.Empty);
            line.RuleFor(l => l.Quantity).GreaterThan(0);
        });
    }
}

public sealed class PlaceOrderCommandHandler(
    IProductRepository products,
    IOrderWriteRepository orders,
    IEventPublisher eventPublisher,
    PricingService pricing,
    TimeProvider clock)
    : IRequestHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var productIds = cmd.Lines.Select(l => new ProductId(l.ProductId)).ToHashSet();
        var loaded = await products.GetManyAsync(productIds, ct);
        if (loaded.Count != productIds.Count)
        {
            var missing = productIds.Except(loaded.Select(p => p.Id));
            throw new DomainException(
                $"Unknown product ids: {string.Join(",", missing)}.");
        }
        var catalog = loaded.ToDictionary(p => p.Id);

        var drafts = cmd.Lines
            .Select(l => new OrderLineDraft(new ProductId(l.ProductId), l.Quantity))
            .ToList();

        var orderLines = pricing.BuildLines(drafts, catalog);

        var order = Order.Place(new CustomerId(cmd.CustomerId), orderLines,
            () => clock.GetUtcNow().UtcDateTime);
        await orders.AddAsync(order, ct);

        foreach (var ev in order.DomainEvents)
        {
            if (ev is Domain.Events.OrderEvent.OrderCreated created)
            {
                var integration = new OrderCreatedIntegrationEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: created.OccurredAtUtc,
                    OrderId: created.OrderId.Value,
                    CustomerId: created.CustomerId.Value,
                    TotalAmount: created.Total.Amount,
                    Currency: created.Total.Currency,
                    Lines: [.. created.Lines.Select(l =>
                        new OrderLinePayload(l.ProductId.Value, l.Quantity.Value, l.UnitPrice.Amount))]);
                await eventPublisher.PublishAsync(integration, ct);
            }
        }
        order.ClearDomainEvents();

        return order.Id.Value;
    }
}
