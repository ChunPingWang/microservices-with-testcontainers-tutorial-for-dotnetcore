using FluentAssertions;
using InventoryService.Domain.Model;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure.Containers;
using Xunit;

namespace InventoryService.Infrastructure.Tests;

[Collection("SharedContainers")]
public class EfStockRepositoryTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrencyToken_RejectsStaleUpdate()
    {
        await using var db = NewContext(containers);
        await db.Database.EnsureCreatedAsync();

        var productId = new ProductId(Guid.NewGuid());
        var stock = Stock.Create(productId, 100);
        await new EfStockRepository(db).AddAsync(stock);

        await using var dbA = NewContext(containers);
        await using var dbB = NewContext(containers);
        var repoA = new EfStockRepository(dbA);
        var repoB = new EfStockRepository(dbB);
        var stockA = (await repoA.GetAsync(productId))!;
        var stockB = (await repoB.GetAsync(productId))!;

        stockA.Reserve(10);
        await repoA.UpdateAsync(stockA);
        await repoA.SaveChangesAsync();

        stockB.Reserve(20);
        await repoB.UpdateAsync(stockB);

        await FluentActions.Invoking(() => repoB.SaveChangesAsync(CancellationToken.None))
            .Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static StockDbContext NewContext(SharedContainerFixture fx)
    {
        var opts = new DbContextOptionsBuilder<StockDbContext>()
            .UseNpgsql(fx.InventoryDb.GetConnectionString())
            .Options;
        return new StockDbContext(opts);
    }
}
