using FluentAssertions;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Services;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace ProductService.Domain.Tests;

public class PricingServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void PriceLines_SumsCorrectly()
    {
        var pid1 = ProductId.New();
        var pid2 = ProductId.New();
        var catalog = new Dictionary<ProductId, Product>
        {
            [pid1] = new(pid1, "A", "", new Money(100m, "TWD")),
            [pid2] = new(pid2, "B", "", new Money(50m, "TWD")),
        };

        var total = new PricingService().PriceLines(
            [new OrderLineDraft(pid1, 2), new OrderLineDraft(pid2, 4)], catalog);

        total.Should().Be(new Money(400m, "TWD"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PriceLines_InactiveProduct_Throws()
    {
        var pid = ProductId.New();
        var product = new Product(pid, "X", "", new Money(10m, "TWD"));
        product.Deactivate();

        FluentActions.Invoking(() =>
                new PricingService().PriceLines([new OrderLineDraft(pid, 1)],
                    new Dictionary<ProductId, Product> { [pid] = product }))
            .Should().Throw<DomainException>();
    }
}
