using InventoryService.Domain.Model;
using InventoryService.Domain.Ports;
using InventoryService.Infrastructure;
using InventoryService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddInventoryServiceCore();

var dbConn = cfg.GetConnectionString("Postgres")
             ?? "Host=localhost;Port=5432;Database=inventory;Username=postgres;Password=postgres";
builder.Services.AddPostgresPersistence(dbConn);

if (builder.Environment.IsProduction())
{
    builder.Services.AddRedisLock(cfg["Redis:ConnectionString"] ?? "localhost:6379");
    builder.Services.AddSingleton(new KafkaSettings(
        cfg["Kafka:BootstrapServers"] ?? "localhost:9092"));
    builder.Services.AddMessaging(MessagingProfile.Kafka);
}
else
{
    builder.Services.AddInMemoryLock();
    builder.Services.AddMessaging(MessagingProfile.InMemory);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");

app.MapPost("/api/stocks", async (CreateStockDto body, IStockWriteRepository repo, CancellationToken ct) =>
{
    var s = Stock.Create(new ProductId(body.ProductId), body.Available);
    await repo.AddAsync(s, ct);
    return Results.Created($"/api/stocks/{s.Id.Value}", new { id = s.Id.Value });
});

app.MapGet("/api/stocks/{productId:guid}",
    async (Guid productId, IStockWriteRepository repo, CancellationToken ct) =>
{
    var s = await repo.GetAsync(new ProductId(productId), ct);
    return s is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            stockId = s.Id.Value,
            productId = s.ProductId.Value,
            available = s.Available.Value,
            reserved = s.Reserved.Value,
            version = s.Version
        });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StockDbContext>();
    await StockSchemaInitialiser.EnsureCreatedAsync(db);
}

app.Run();

public sealed record CreateStockDto(Guid ProductId, int Available);
namespace InventoryService.Api { public partial class Program { } }
