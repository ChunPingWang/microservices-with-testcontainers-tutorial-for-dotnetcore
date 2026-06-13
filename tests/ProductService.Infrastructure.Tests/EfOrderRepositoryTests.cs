using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Infrastructure.Persistence;
using SharedKernel.Domain.ValueObjects;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class EfOrderRepositoryTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RoundTrip_OrderWithLines_PreservesAllState()
    {
        await using var db = NewContext(containers);
        await db.Database.EnsureCreatedAsync();
        var repo = new EfOrderRepository(db);

        var customer = CustomerId.New();
        var order = Order.Place(customer, [
            OrderLine.Create(ProductId.New(), 2, new Money(100m, "TWD")),
            OrderLine.Create(ProductId.New(), 1, new Money(50m, "TWD"))]);
        await repo.AddAsync(order);
        order.ClearDomainEvents();

        await using var db2 = NewContext(containers);
        var repo2 = new EfOrderRepository(db2);
        var loaded = await repo2.FindAsync(order.Id);

        loaded.Should().NotBeNull();
        loaded!.CustomerId.Should().Be(customer);
        loaded.Total.Should().Be(new Money(250m, "TWD"));
        loaded.Lines.Should().HaveCount(2);
        loaded.Status.Should().BeOfType<OrderStatus.Created>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MarkPaid_PersistsPaymentInfo()
    {
        await using var db = NewContext(containers);
        await db.Database.EnsureCreatedAsync();
        var repo = new EfOrderRepository(db);

        var order = Order.Place(CustomerId.New(), [
            OrderLine.Create(ProductId.New(), 1, new Money(99m, "TWD"))]);
        await repo.AddAsync(order);

        var paymentId = new PaymentId(Guid.NewGuid());
        order.MarkPaid(paymentId);
        await repo.UpdateAsync(order);

        await using var db2 = NewContext(containers);
        var loaded = await new EfOrderRepository(db2).FindAsync(order.Id);
        loaded!.Status.Should().BeOfType<OrderStatus.Paid>()
            .Which.PaymentId.Should().Be(paymentId);
    }

    private static ProductDbContext NewContext(SharedContainerFixture fx)
    {
        var opts = new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql(fx.ProductsDb.GetConnectionString())
            .Options;
        return new ProductDbContext(opts);
    }
}
