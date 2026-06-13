using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using ProductService.Api;
using ProductService.Api.Endpoints;
using ProductService.Application.Commands;
using ProductService.Application.Queries;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Search;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.Services.AddProductServiceCore(cfg);

var dbConn = cfg.GetConnectionString("Postgres")
             ?? "Host=localhost;Port=5432;Database=products;Username=postgres;Password=postgres";
builder.Services.AddPostgresPersistence(dbConn);

if (builder.Environment.IsProduction())
{
    builder.Services
        .AddRedisCache(cfg["Redis:ConnectionString"] ?? "localhost:6379")
        .AddElasticSearch(cfg["Elasticsearch:Url"] ?? "http://localhost:9200")
        .AddMinioStorage(
            cfg["Minio:Endpoint"] ?? "localhost:9000",
            cfg["Minio:AccessKey"] ?? "minioadmin",
            cfg["Minio:SecretKey"] ?? "minioadmin");
    builder.Services.AddSingleton(new KafkaSettings(
        cfg["Kafka:BootstrapServers"] ?? "localhost:9092"));
    builder.Services.AddMessaging(MessagingProfile.Kafka);
}
else
{
    builder.Services
        .AddInMemoryCache()
        .AddInMemorySearch()
        .AddInMemoryStorage();
    builder.Services.AddMessaging(MessagingProfile.InMemory);
}

// JWT Bearer (Keycloak in prod; Stub in Development/Testing)
var jwtAuthority = cfg["Jwt:Authority"];
if (!string.IsNullOrWhiteSpace(jwtAuthority))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.Authority = jwtAuthority;
            opts.Audience = cfg["Jwt:Audience"] ?? "products";
            opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            opts.TokenValidationParameters.ValidateIssuer = true;
        });
}
else
{
    builder.Services.AddAuthentication("Stub")
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, StubAuthHandler>("Stub", _ => { });
}
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

OrderEndpoints.Map(app);
ProductEndpoints.Map(app);

// Auto-migrate DB on startup (production should use proper migrations)
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var db = sp.GetRequiredService<ProductDbContext>();
    await SchemaInitialiser.EnsureCreatedAsync(db);

    if (app.Environment.IsProduction() || app.Environment.EnvironmentName == "Testing")
    {
        var search = sp.GetRequiredService<ProductService.Domain.Ports.Outbound.ISearchPort>();
        await search.EnsureIndexAsync();
    }
}

app.Run();

namespace ProductService.Api { public partial class Program { } }
