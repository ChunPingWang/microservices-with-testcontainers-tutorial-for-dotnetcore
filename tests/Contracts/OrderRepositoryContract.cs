using FluentAssertions;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace Contracts;

/// <summary>
/// Behaviour every <see cref="IOrderWriteRepository"/> implementation must
/// satisfy, regardless of whether it's an in-memory fake or a real EF Core +
/// PostgreSQL adapter. Inherit from this class in your test project and supply
/// a <see cref="CreateAsync"/> factory.
///
/// The point of contract tests: the in-memory fake we hand to fast unit tests
/// must behave like the real database. When they diverge, business code starts
/// passing in unit tests and failing in production — this guards against that.
/// </summary>
public abstract class OrderRepositoryContract
{
    /// <summary>
    /// Create a fresh, empty repository. Implementations may also tear down
    /// state from previous tests (e.g. truncate tables).
    /// </summary>
    protected abstract Task<IOrderWriteRepository> CreateAsync();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task FindAsync_AfterAdd_ReturnsSameOrder()
    {
        var repo = await CreateAsync();
        var order = NewOrder();
        await repo.AddAsync(order);

        var found = await repo.FindAsync(order.Id);
        found.Should().NotBeNull();
        found!.Id.Should().Be(order.Id);
        found.CustomerId.Should().Be(order.CustomerId);
        found.Total.Should().Be(order.Total);
        found.Lines.Should().HaveCount(order.Lines.Count);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task FindAsync_UnknownId_ReturnsNull()
    {
        var repo = await CreateAsync();
        (await repo.FindAsync(OrderId.New())).Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task UpdateAsync_PersistsStatusTransitionToPaid()
    {
        var repo = await CreateAsync();
        var order = NewOrder();
        await repo.AddAsync(order);

        var paymentId = new PaymentId(Guid.NewGuid());
        order.MarkPaid(paymentId);
        await repo.UpdateAsync(order);

        var reloaded = await repo.FindAsync(order.Id);
        reloaded!.Status.Should().BeOfType<OrderStatus.Paid>()
            .Which.PaymentId.Should().Be(paymentId);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task UpdateAsync_PersistsRefundReason()
    {
        var repo = await CreateAsync();
        var order = NewOrder();
        await repo.AddAsync(order);
        order.MarkPaid(new PaymentId(Guid.NewGuid()));
        order.Refund("inventory short", () => DateTime.UtcNow);
        await repo.UpdateAsync(order);

        var reloaded = await repo.FindAsync(order.Id);
        reloaded!.Status.Should().BeOfType<OrderStatus.Refunded>()
            .Which.Reason.Should().Be("inventory short");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Lines_PreserveQuantitiesAndPrices()
    {
        var repo = await CreateAsync();
        var pid1 = ProductId.New();
        var pid2 = ProductId.New();
        var order = Order.Place(CustomerId.New(),
        [
            OrderLine.Create(pid1, 3, new Money(100m, "TWD")),
            OrderLine.Create(pid2, 1, new Money(50m, "TWD")),
        ]);
        await repo.AddAsync(order);

        var reloaded = await repo.FindAsync(order.Id);
        reloaded!.Lines.Should().HaveCount(2);
        reloaded.Lines.Should().Contain(l =>
            l.ProductId == pid1 && l.Quantity.Value == 3 && l.UnitPrice.Amount == 100m);
        reloaded.Lines.Should().Contain(l =>
            l.ProductId == pid2 && l.Quantity.Value == 1 && l.UnitPrice.Amount == 50m);
        reloaded.Total.Should().Be(new Money(350m, "TWD"));
    }

    private static Order NewOrder() => Order.Place(CustomerId.New(),
        [OrderLine.Create(ProductId.New(), 1, new Money(10m, "TWD"))]);
}
