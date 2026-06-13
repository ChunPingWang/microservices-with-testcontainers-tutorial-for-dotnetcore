using MediatR;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Queries;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Api.Endpoints;

public static class ProductEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/products");

        grp.MapGet("/search", async (string? q, int page, int pageSize, IMediator m,
            CancellationToken ct) =>
        {
            var query = new SearchProductQuery(q, page == 0 ? 1 : page, pageSize == 0 ? 20 : pageSize);
            return Results.Ok(await m.Send(query, ct));
        });

        // seed for demo / E2E
        grp.MapPost("/", async ([FromBody] CreateProductDto body, IProductRepository repo,
            ISearchPort search, CancellationToken ct) =>
        {
            var id = ProductId.New();
            var product = new Product(id, body.Name, body.Description,
                new Money(body.Price, body.Currency));
            await repo.AddAsync(product, ct);
            await search.IndexAsync(new ProductSearchDoc(
                id.Value, body.Name, body.Description, body.Price, body.Currency, true), ct);
            return Results.Created($"/api/products/{id.Value}", new { id = id.Value });
        });
    }
}

public sealed record CreateProductDto(string Name, string Description, decimal Price, string Currency);
