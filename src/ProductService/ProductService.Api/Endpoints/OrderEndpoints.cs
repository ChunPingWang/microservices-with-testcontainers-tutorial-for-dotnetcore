using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Commands;
using ProductService.Application.Queries;

namespace ProductService.Api.Endpoints;

public static class OrderEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/orders").RequireAuthorization();

        grp.MapPost("/", async ([FromBody] PlaceOrderRequestDto body, IMediator m,
            CancellationToken ct) =>
        {
            var cmd = new PlaceOrderCommand(body.CustomerId,
                [.. body.Lines.Select(l => new OrderLineDto(l.ProductId, l.Quantity))]);
            var id = await m.Send(cmd, ct);
            return Results.Created($"/api/orders/{id}", new { id });
        });

        grp.MapGet("/{id:guid}", async (Guid id, IMediator m, CancellationToken ct) =>
        {
            var dto = await m.Send(new GetOrderQuery(id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
    }
}

public sealed record PlaceOrderRequestDto(Guid CustomerId, IReadOnlyList<PlaceOrderLineDto> Lines);
public sealed record PlaceOrderLineDto(Guid ProductId, int Quantity);
