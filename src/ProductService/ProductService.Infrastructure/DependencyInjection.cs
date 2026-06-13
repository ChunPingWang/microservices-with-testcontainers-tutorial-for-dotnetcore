using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minio;
using ProductService.Application.Behaviors;
using ProductService.Application.Commands;
using ProductService.Domain.Ports.Inbound;
using ProductService.Domain.Ports.Outbound;
using ProductService.Domain.Services;
using ProductService.Infrastructure.Cache;
using ProductService.Infrastructure.Messaging;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Search;
using ProductService.Infrastructure.Storage;
using SharedKernel.Domain.Ports;
using SharedKernel.Infrastructure.Messaging;
using StackExchange.Redis;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

namespace ProductService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProductServiceCore(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PricingService>();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<PlaceOrderCommand>();
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssemblyContaining<PlaceOrderCommandValidator>();

        services.AddScoped<IPlaceOrderUseCase, ProductService.Application.MediatRPlaceOrderUseCase>();
        services.AddScoped<ISearchProductUseCase, ProductService.Application.MediatRSearchProductUseCase>();

        return services;
    }

    public static IServiceCollection AddPostgresPersistence(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ProductDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IOrderWriteRepository, EfOrderRepository>();
        services.AddScoped<IOrderReadRepository>(sp =>
            (EfOrderRepository)sp.GetRequiredService<IOrderWriteRepository>());
        services.AddScoped<IProductRepository, EfProductRepository>();
        return services;
    }

    public static IServiceCollection AddRedisCache(
        this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<ICachePort, RedisCacheAdapter>();
        return services;
    }

    public static IServiceCollection AddInMemoryCache(this IServiceCollection services)
    {
        services.AddSingleton<ICachePort, InMemoryCachePort>();
        return services;
    }

    public static IServiceCollection AddElasticSearch(
        this IServiceCollection services, string url)
    {
        services.AddSingleton(_ =>
        {
            var settings = new ElasticsearchClientSettings(new Uri(url))
                .DisableAutomaticProxyDetection();
            return new ElasticsearchClient(settings);
        });
        services.AddSingleton<ISearchPort, ElasticSearchAdapter>();
        return services;
    }

    public static IServiceCollection AddInMemorySearch(this IServiceCollection services)
    {
        services.AddSingleton<ISearchPort, InMemorySearchAdapter>();
        return services;
    }

    public static IServiceCollection AddMinioStorage(
        this IServiceCollection services, string endpoint, string accessKey, string secretKey)
    {
        services.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .Build());
        services.AddSingleton<IObjectStorage, MinioStorageAdapter>();
        return services;
    }

    public static IServiceCollection AddInMemoryStorage(this IServiceCollection services)
    {
        services.AddSingleton<IObjectStorage, InMemoryObjectStorage>();
        return services;
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services, MessagingProfile profile, Action<IBusRegistrationConfigurator>? extraSetup = null)
    {
        services.AddMassTransit(x =>
        {
            x.AddConsumer<PaymentCompletedConsumer>();
            x.AddConsumer<InventoryDeductedConsumer>();
            x.AddConsumer<InventoryDeductionFailedConsumer>();

            switch (profile)
            {
                case MessagingProfile.Kafka:
                    x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                    x.AddRider(r =>
                    {
                        r.AddConsumer<PaymentCompletedConsumer>();
                        r.AddConsumer<InventoryDeductedConsumer>();
                        r.AddConsumer<InventoryDeductionFailedConsumer>();
                        r.UsingKafka((ctx, cfg) =>
                        {
                            cfg.Host(ctx.GetRequiredService<KafkaSettings>().BootstrapServers);
                        });
                    });
                    break;
                case MessagingProfile.InMemory:
                default:
                    x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
                    break;
            }

            extraSetup?.Invoke(x);
        });

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return services;
    }
}

public enum MessagingProfile { InMemory, Kafka }

public sealed record KafkaSettings(string BootstrapServers);
