using FluentAssertions;
using NetArchTest.Rules;
using System.Reflection;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Architecture guards — enforce the Hexagonal layering across all three services.
/// These run as Unit-category tests so CI fails fast if a dependency leak slips in.
/// </summary>
public class LayeringRules
{
    private static readonly Assembly ProductDomain = typeof(ProductService.Domain.Model.Order).Assembly;
    private static readonly Assembly ProductApplication = typeof(ProductService.Application.Commands.PlaceOrderCommand).Assembly;
    private static readonly Assembly ProductInfra = typeof(ProductService.Infrastructure.Persistence.ProductDbContext).Assembly;
    private static readonly Assembly PaymentDomain = typeof(PaymentService.Domain.Model.Payment).Assembly;
    private static readonly Assembly PaymentApplication = typeof(PaymentService.Application.Commands.ProcessPaymentCommand).Assembly;
    private static readonly Assembly PaymentInfra = typeof(PaymentService.Infrastructure.Persistence.PaymentDbContext).Assembly;
    private static readonly Assembly InventoryDomain = typeof(InventoryService.Domain.Model.Stock).Assembly;
    private static readonly Assembly InventoryApplication = typeof(InventoryService.Application.Commands.DeductStockCommand).Assembly;
    private static readonly Assembly InventoryInfra = typeof(InventoryService.Infrastructure.Persistence.StockDbContext).Assembly;

    [Theory]
    [Trait("Category", "Unit")]
    [MemberData(nameof(DomainAssemblies))]
    public void Domain_DoesNotDependOnInfrastructureLibraries(Assembly domain)
    {
        var bannedPrefixes = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "MassTransit",
            "Confluent.Kafka",
            "StackExchange.Redis",
            "Elastic.Clients",
            "Minio",
            "VaultSharp",
            "Google.Cloud.PubSub",
            "Microsoft.AspNetCore",
        };

        var result = Types.InAssembly(domain)
            .Should().NotHaveDependencyOnAny(bannedPrefixes)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain layer {domain.GetName().Name} must not reference infrastructure: {Names(result.FailingTypeNames)}");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [MemberData(nameof(ApplicationAssemblies))]
    public void Application_DoesNotDependOnEfOrAspNet(Assembly application)
    {
        var banned = new[]
        {
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Npgsql",
            "Confluent.Kafka",
        };

        var result = Types.InAssembly(application)
            .Should().NotHaveDependencyOnAny(banned)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application layer {application.GetName().Name} leaked infra deps: {Names(result.FailingTypeNames)}");
    }

    public static IEnumerable<object[]> DomainAssemblies()
    {
        yield return [ProductDomain];
        yield return [PaymentDomain];
        yield return [InventoryDomain];
    }

    public static IEnumerable<object[]> ApplicationAssemblies()
    {
        yield return [ProductApplication];
        yield return [PaymentApplication];
        yield return [InventoryApplication];
    }

    private static string Names(IEnumerable<string>? failing)
        => failing is null ? "" : string.Join(", ", failing);
}
