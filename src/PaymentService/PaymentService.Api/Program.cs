using MediatR;
using PaymentService.Application.Commands;
using PaymentService.Infrastructure;
using PaymentService.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddPaymentServiceCore();

var dbConn = cfg.GetConnectionString("Postgres")
             ?? "Host=localhost;Port=5432;Database=payments;Username=postgres;Password=postgres";
builder.Services.AddPostgresPersistence(dbConn);

if (builder.Environment.IsProduction())
{
    builder.Services
        .AddMinioReceipts(
            cfg["Minio:Endpoint"] ?? "localhost:9000",
            cfg["Minio:AccessKey"] ?? "minioadmin",
            cfg["Minio:SecretKey"] ?? "minioadmin")
        .AddVault(cfg["Vault:Address"] ?? "http://localhost:8200",
                  cfg["Vault:Token"] ?? "root")
        .AddPubSubNotification(
            projectId: cfg["PubSub:ProjectId"] ?? "demo-project",
            topic: cfg["PubSub:Topic"] ?? "payment-events",
            emulatorHost: cfg["PubSub:EmulatorHost"]);

    builder.Services.AddSingleton(new KafkaSettings(
        cfg["Kafka:BootstrapServers"] ?? "localhost:9092"));
    builder.Services.AddMessaging(MessagingProfile.Kafka);
}
else
{
    builder.Services
        .AddInMemoryReceipts()
        .AddConfigSecrets()
        .AddLogNotification()
        .AddMessaging(MessagingProfile.InMemory);
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");

app.MapPost("/api/payments", async (PaymentRequest body, IMediator m, CancellationToken ct) =>
{
    var id = await m.Send(new ProcessPaymentCommand(body.OrderId, body.Amount,
        body.Currency, body.IdempotencyKey), ct);
    return Results.Created($"/api/payments/{id}", new { id });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await PaymentSchemaInitialiser.EnsureCreatedAsync(db);
}

app.Run();

public sealed record PaymentRequest(Guid OrderId, decimal Amount, string Currency, string IdempotencyKey);
namespace PaymentService.Api { public partial class Program { } }
