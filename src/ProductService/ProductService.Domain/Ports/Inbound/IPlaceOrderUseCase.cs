using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Services;

namespace ProductService.Domain.Ports.Inbound;

public sealed record PlaceOrderRequest(CustomerId CustomerId, IReadOnlyList<OrderLineDraft> Lines);

public interface IPlaceOrderUseCase
{
    Task<OrderId> ExecuteAsync(PlaceOrderRequest request, CancellationToken ct = default);
}
