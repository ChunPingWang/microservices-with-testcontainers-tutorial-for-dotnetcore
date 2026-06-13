using FluentAssertions;
using ProductService.Domain.Events;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace ProductService.Domain.Tests;

public class OrderTests
{
    private readonly DateTime _now = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private DateTime Clock() => _now;

    private static OrderLine Line(decimal price, int qty)
        => OrderLine.Create(ProductId.New(), qty, new Money(price, "TWD"));

    [Fact]
    [Trait("Category", "Unit")]
    public void Place_CalculatesTotal_AndEmitsCreatedEvent()
    {
        var order = Order.Place(CustomerId.New(), [Line(100, 2), Line(50, 1)], Clock);

        order.Total.Should().Be(new Money(250m, "TWD"));
        order.Status.Should().BeOfType<OrderStatus.Created>();
        order.DomainEvents.Should().ContainSingle(e => e is OrderEvent.OrderCreated);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Place_NoLines_Throws()
    {
        FluentActions
            .Invoking(() => Order.Place(CustomerId.New(), [], Clock))
            .Should().Throw<DomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkPaid_FromCreated_Succeeds()
    {
        var order = Order.Place(CustomerId.New(), [Line(100, 1)], Clock);
        var paymentId = new PaymentId(Guid.NewGuid());

        order.MarkPaid(paymentId, Clock);

        order.Status.Should().BeOfType<OrderStatus.Paid>()
            .Which.PaymentId.Should().Be(paymentId);
        order.DomainEvents.Should().Contain(e => e is OrderEvent.OrderPaid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkPaid_FromPaid_Throws()
    {
        var order = Order.Place(CustomerId.New(), [Line(100, 1)], Clock);
        order.MarkPaid(new PaymentId(Guid.NewGuid()), Clock);

        FluentActions
            .Invoking(() => order.MarkPaid(new PaymentId(Guid.NewGuid()), Clock))
            .Should().Throw<DomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkCompleted_RequiresPaid()
    {
        var order = Order.Place(CustomerId.New(), [Line(100, 1)], Clock);

        FluentActions.Invoking(() => order.MarkCompleted(Clock))
            .Should().Throw<DomainException>();

        order.MarkPaid(new PaymentId(Guid.NewGuid()), Clock);
        order.MarkCompleted(Clock);
        order.Status.Should().BeOfType<OrderStatus.Completed>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refund_OnlyFromPaid()
    {
        var order = Order.Place(CustomerId.New(), [Line(100, 1)], Clock);
        FluentActions.Invoking(() => order.Refund("test", Clock))
            .Should().Throw<DomainException>();
        order.MarkPaid(new PaymentId(Guid.NewGuid()), Clock);
        order.Refund("inventory short", Clock);
        order.Status.Should().BeOfType<OrderStatus.Refunded>();
    }
}
