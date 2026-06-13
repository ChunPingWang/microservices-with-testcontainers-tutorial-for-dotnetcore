using FluentValidation;
using InventoryService.Application.Behaviors;
using InventoryService.Application.Commands;
using InventoryService.Domain.Ports;
using InventoryService.Domain.Services;
using InventoryService.Infrastructure.Lock;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel.Domain.Ports;
using SharedKernel.Infrastructure.Messaging;
using StackExchange.Redis;

namespace InventoryService.Infrastructure;

public enum MessagingProfile { InMemory, Kafka }
public sealed record KafkaSettings(string BootstrapServers);

public static class DependencyInjection
{
    public static IServiceCollection AddInventoryServiceCore(this IServiceCollection s)
    {
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton<StockAllocationService>();
        s.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<DeductStockCommand>();
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        s.AddValidatorsFromAssemblyContaining<DeductStockCommandValidator>();
        return s;
    }

    public static IServiceCollection AddPostgresPersistence(this IServiceCollection s, string conn)
    {
        s.AddDbContext<StockDbContext>(o => o.UseNpgsql(conn));
        s.AddScoped<IStockWriteRepository, EfStockRepository>();
        return s;
    }

    public static IServiceCollection AddRedisLock(this IServiceCollection s, string conn)
    {
        s.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(conn));
        s.AddSingleton<IDistributedLock, RedisDistributedLock>();
        return s;
    }

    public static IServiceCollection AddInMemoryLock(this IServiceCollection s)
    {
        s.AddSingleton<IDistributedLock, SemaphoreDistributedLock>();
        return s;
    }

    public static IServiceCollection AddMessaging(this IServiceCollection s, MessagingProfile profile)
    {
        s.AddMassTransit(x =>
        {
            x.AddConsumer<OrderCreatedConsumer>();
            x.AddConsumer<PaymentCompletedConsumer>();
            x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));

            if (profile == MessagingProfile.Kafka)
            {
                x.AddRider(r =>
                {
                    r.AddConsumer<OrderCreatedConsumer>();
                    r.AddConsumer<PaymentCompletedConsumer>();
                    r.UsingKafka((ctx, cfg) =>
                    {
                        cfg.Host(ctx.GetRequiredService<KafkaSettings>().BootstrapServers);
                    });
                });
            }
        });
        s.AddScoped<IEventPublisher, MassTransitEventPublisher>();
        return s;
    }
}
