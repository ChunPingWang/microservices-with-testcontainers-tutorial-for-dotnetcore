using MediatR;
using ProductService.Application.Commands;
using ProductService.Application.Queries;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Inbound;
using ProductService.Domain.Ports.Outbound;
using ProductService.Domain.Services;

namespace ProductService.Application;

public sealed class MediatRPlaceOrderUseCase(IMediator mediator) : IPlaceOrderUseCase
{
    public async Task<OrderId> ExecuteAsync(PlaceOrderRequest request, CancellationToken ct = default)
    {
        var cmd = new PlaceOrderCommand(
            request.CustomerId.Value,
            [.. request.Lines.Select(l => new OrderLineDto(l.ProductId.Value, l.Quantity))]);
        var id = await mediator.Send(cmd, ct);
        return new OrderId(id);
    }
}

public sealed class MediatRSearchProductUseCase(IMediator mediator) : ISearchProductUseCase
{
    public async Task<SearchResult<ProductSearchDoc>> ExecuteAsync(
        SearchProductRequest request, CancellationToken ct = default)
    {
        var paged = await mediator.Send(
            new SearchProductQuery(request.Keyword, request.Page, request.PageSize), ct);
        var docs = paged.Items.Select(p => new ProductSearchDoc(
            p.Id, p.Name, p.Description, p.Price, p.Currency, true)).ToList();
        return new SearchResult<ProductSearchDoc>(docs, paged.Total, paged.Page, paged.PageSize);
    }
}
