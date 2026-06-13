using FluentAssertions;
using InventoryService.Domain.Model;
using InventoryService.Domain.Services;
using SharedKernel.Domain;
using Xunit;

namespace InventoryService.Domain.Tests;

public class StockTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Reserve_DecreasesAvailable_IncreasesReserved()
    {
        var stock = Stock.Create(new ProductId(Guid.NewGuid()), 100);
        stock.Reserve(30);
        stock.Available.Value.Should().Be(70);
        stock.Reserved.Value.Should().Be(30);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Reserve_InsufficientStock_Throws()
    {
        var stock = Stock.Create(new ProductId(Guid.NewGuid()), 5);
        FluentActions.Invoking(() => stock.Reserve(10))
            .Should().Throw<DomainException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Commit_DecreasesReserved()
    {
        var stock = Stock.Create(new ProductId(Guid.NewGuid()), 50);
        stock.Reserve(20);
        stock.Commit(20);
        stock.Reserved.Value.Should().Be(0);
        stock.Available.Value.Should().Be(30);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Release_RestoresAvailable()
    {
        var stock = Stock.Create(new ProductId(Guid.NewGuid()), 10);
        stock.Reserve(7);
        stock.Release(7);
        stock.Available.Value.Should().Be(10);
        stock.Reserved.Value.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Allocator_AllOrNothing_OnPartialAvailability()
    {
        var p1 = new ProductId(Guid.NewGuid());
        var p2 = new ProductId(Guid.NewGuid());
        var stocks = new Dictionary<ProductId, Stock>
        {
            [p1] = Stock.Create(p1, 10),
            [p2] = Stock.Create(p2, 1),
        };

        FluentActions.Invoking(() => new StockAllocationService().AllocateAll(
            [new AllocationLine(p1, 5), new AllocationLine(p2, 5)], stocks))
            .Should().Throw<DomainException>();

        // Neither stock should have been mutated
        stocks[p1].Available.Value.Should().Be(10);
        stocks[p2].Available.Value.Should().Be(1);
    }
}
