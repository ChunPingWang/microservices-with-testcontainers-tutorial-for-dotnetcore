using FluentAssertions;
using PaymentService.Domain.Events;
using PaymentService.Domain.Model;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace PaymentService.Domain.Tests;

public class PaymentTests
{
    private static Func<DateTime> Clock(DateTime t) => () => t;

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_Pending_EmitsInitiated()
    {
        var p = Payment.Create(new OrderId(Guid.NewGuid()),
            new Money(99m, "TWD"), IdempotencyKey.Of("k1"));
        p.Status.Should().BeOfType<PaymentStatus.Pending>();
        p.DomainEvents.Should().ContainSingle(e => e is PaymentEvent.PaymentInitiated);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NonPositive_Throws()
    {
        FluentActions.Invoking(() =>
            Payment.Create(new OrderId(Guid.NewGuid()),
                new Money(0m, "TWD"), IdempotencyKey.Of("x")))
            .Should().Throw<DomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkCompleted_TransitionsAndEmits()
    {
        var p = Payment.Create(new OrderId(Guid.NewGuid()),
            new Money(10m, "TWD"), IdempotencyKey.Of("k2"));
        p.MarkCompleted("receipts/abc");
        p.Status.Should().BeOfType<PaymentStatus.Completed>()
            .Which.ReceiptKey.Should().Be("receipts/abc");
        p.DomainEvents.Should().Contain(e => e is PaymentEvent.PaymentCompleted);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refund_OnlyFromCompleted()
    {
        var p = Payment.Create(new OrderId(Guid.NewGuid()),
            new Money(10m, "TWD"), IdempotencyKey.Of("k3"));
        FluentActions.Invoking(() => p.Refund("oops"))
            .Should().Throw<DomainException>();

        p.MarkCompleted("r/1");
        p.Refund("inventory");
        p.Status.Should().BeOfType<PaymentStatus.Refunded>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IdempotencyKey_TooLong_Throws()
    {
        FluentActions.Invoking(() => IdempotencyKey.Of(new string('a', 129)))
            .Should().Throw<DomainException>();
    }
}
