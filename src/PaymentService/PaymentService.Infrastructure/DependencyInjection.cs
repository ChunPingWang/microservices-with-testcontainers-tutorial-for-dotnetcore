using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using PaymentService.Application.Behaviors;
using PaymentService.Application.Commands;
using PaymentService.Domain.Ports;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.Notification;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Secret;
using PaymentService.Infrastructure.Storage;
using SharedKernel.Domain.Ports;
using SharedKernel.Infrastructure.Messaging;
using Google.Cloud.PubSub.V1;

namespace PaymentService.Infrastructure;

public enum MessagingProfile { InMemory, Kafka }
public sealed record KafkaSettings(string BootstrapServers);

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentServiceCore(this IServiceCollection s)
    {
        s.AddSingleton(TimeProvider.System);
        s.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<ProcessPaymentCommand>();
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });
        s.AddValidatorsFromAssemblyContaining<ProcessPaymentCommandValidator>();
        return s;
    }

    public static IServiceCollection AddPostgresPersistence(this IServiceCollection s, string conn)
    {
        s.AddDbContext<PaymentDbContext>(o => o.UseNpgsql(conn));
        s.AddScoped<IPaymentWriteRepository, EfPaymentRepository>();
        return s;
    }

    public static IServiceCollection AddMinioReceipts(
        this IServiceCollection s, string endpoint, string ak, string sk)
    {
        s.AddSingleton<IMinioClient>(_ => new MinioClient()
            .WithEndpoint(endpoint).WithCredentials(ak, sk).Build());
        s.AddSingleton<IReceiptStorage>(sp =>
            new MinioReceiptStorage(sp.GetRequiredService<IMinioClient>()));
        return s;
    }

    public static IServiceCollection AddInMemoryReceipts(this IServiceCollection s)
        => s.AddSingleton<IReceiptStorage, InMemoryReceiptStorage>();

    public static IServiceCollection AddVault(this IServiceCollection s, string addr, string token)
    {
        s.AddSingleton<ISecretProvider>(_ => new VaultSecretProvider(addr, token));
        return s;
    }

    public static IServiceCollection AddConfigSecrets(this IServiceCollection s)
    {
        s.AddSingleton<ISecretProvider, ConfigSecretProvider>();
        return s;
    }

    public static IServiceCollection AddPubSubNotification(
        this IServiceCollection s, string projectId, string topic, string? emulatorHost = null)
    {
        s.AddSingleton(_ =>
        {
            if (!string.IsNullOrEmpty(emulatorHost))
                Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", emulatorHost);
            var name = TopicName.FromProjectTopic(projectId, topic);
            return PublisherClient.Create(name);
        });
        s.AddSingleton<INotificationPort, PubSubNotificationAdapter>();
        return s;
    }

    public static IServiceCollection AddLogNotification(this IServiceCollection s)
        => s.AddSingleton<INotificationPort, LogNotificationAdapter>();

    public static IServiceCollection AddMessaging(
        this IServiceCollection s, MessagingProfile profile)
    {
        s.AddMassTransit(x =>
        {
            x.AddConsumer<OrderCreatedConsumer>();
            x.AddConsumer<InventoryDeductionFailedConsumer>();
            x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));

            if (profile == MessagingProfile.Kafka)
            {
                x.AddRider(r =>
                {
                    r.AddConsumer<OrderCreatedConsumer>();
                    r.AddConsumer<InventoryDeductionFailedConsumer>();
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
