using FluentAssertions;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace SharedKernel.Tests;

public class MoneyTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Addition_SameCurrency_Succeeds()
    {
        var a = new Money(10m, "USD");
        var b = new Money(2.5m, "USD");
        (a + b).Should().Be(new Money(12.5m, "USD"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Addition_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(10m, "EUR");
        FluentActions.Invoking(() => _ = a + b).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Multiplication_PreservesCurrency()
    {
        var price = new Money(7m, "TWD");
        (price * 3).Should().Be(new Money(21m, "TWD"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Equality_IsValueBased()
    {
        new Money(10m, "USD").Should().Be(new Money(10m, "USD"));
        new Money(10m, "USD").Should().NotBe(new Money(11m, "USD"));
    }
}
