using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductDI = ProductService.Infrastructure.DependencyInjection;
using PaymentDI = PaymentService.Infrastructure.DependencyInjection;
using InventoryDI = InventoryService.Infrastructure.DependencyInjection;
using TestInfrastructure.Containers;

namespace EndToEnd.Tests;

/// <summary>
/// All three Web hosts wired against the SharedContainerFixture stack and
/// connected through a single MassTransit InMemory bus instance so events
/// flow end-to-end without needing Kafka. (Testing-Lite profile.)
/// </summary>
public sealed class ProductWebFactory(SharedContainerFixture fx) : WebApplicationFactory<ProductService.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fx.ProductsDb.GetConnectionString(),
                ["Redis:ConnectionString"] = fx.Redis.GetConnectionString(),
                ["Elasticsearch:Url"] = fx.Elasticsearch.GetConnectionString(),
                ["Jwt:Authority"] = "",
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ProductService.Domain.Ports.Outbound.ICachePort>();
            ProductDI.AddRedisCache(services, fx.Redis.GetConnectionString());
            services.RemoveAll<ProductService.Domain.Ports.Outbound.ISearchPort>();
            ProductDI.AddElasticSearch(services, fx.Elasticsearch.GetConnectionString());
        });
    }
}

public sealed class PaymentWebFactory(SharedContainerFixture fx)
    : WebApplicationFactory<PaymentService.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fx.PaymentsDb.GetConnectionString(),
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<SharedKernel.Domain.Ports.ISecretProvider>();
            PaymentDI.AddVault(services, fx.VaultAddress, SharedContainerFixture.VaultRootToken);
        });
    }
}

public sealed class InventoryWebFactory(SharedContainerFixture fx)
    : WebApplicationFactory<InventoryService.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = fx.InventoryDb.GetConnectionString(),
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<InventoryService.Domain.Ports.IDistributedLock>();
            InventoryDI.AddRedisLock(services, fx.Redis.GetConnectionString());
        });
    }
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
            services.Remove(d);
    }
}
