using MediatR;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Application.Queries;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal Total,
    string Currency,
    IReadOnlyList<OrderLineReadDto> Lines);

public sealed record OrderLineReadDto(Guid ProductId, int Quantity, decimal UnitPrice);

public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;

public sealed class GetOrderQueryHandler(IOrderReadRepository repo)
    : IRequestHandler<GetOrderQuery, OrderDto?>
{
    public async Task<OrderDto?> Handle(GetOrderQuery q, CancellationToken ct)
    {
        var order = await repo.GetAsync(new OrderId(q.OrderId), ct);
        return order is null ? null : Map(order);
    }

    private static OrderDto Map(Order o) => new(
        o.Id.Value, o.CustomerId.Value, o.Status.Name,
        o.Total.Amount, o.Total.Currency,
        [.. o.Lines.Select(l => new OrderLineReadDto(l.ProductId.Value, l.Quantity.Value, l.UnitPrice.Amount))]);
}
