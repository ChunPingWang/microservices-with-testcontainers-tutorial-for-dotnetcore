using FluentAssertions;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace SharedKernel.Tests;

public class QuantityTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Of_Negative_Throws()
    {
        FluentActions.Invoking(() => Quantity.Of(-1)).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Subtract_BelowZero_Throws()
    {
        var a = Quantity.Of(3);
        var b = Quantity.Of(4);
        FluentActions.Invoking(() => _ = a - b).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_Subtract_Roundtrip()
    {
        (Quantity.Of(10) + Quantity.Of(5) - Quantity.Of(3)).Should().Be(Quantity.Of(12));
    }
}
