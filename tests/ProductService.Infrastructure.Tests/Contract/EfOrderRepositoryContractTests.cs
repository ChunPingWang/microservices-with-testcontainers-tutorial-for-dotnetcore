using Contracts;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Ports.Outbound;
using ProductService.Infrastructure.Persistence;
using TestInfrastructure.Containers;
using Xunit;

namespace ProductService.Infrastructure.Tests.Contract;

/// <summary>
/// Run the shared <see cref="OrderRepositoryContract"/> against the EF Core +
/// PostgreSQL adapter wired to a real Testcontainers container.
/// Same five assertions as the in-memory variant in Application.Tests; if a
/// behaviour starts diverging, this test catches it.
/// </summary>
[Collection("SharedContainers")]
public class EfOrderRepositoryContractTests(SharedContainerFixture containers)
    : OrderRepositoryContract
{
    protected override async Task<IOrderWriteRepository> CreateAsync()
    {
        // Each test uses fresh random IDs so isolation doesn't require a full
        // schema reset — EnsureCreatedAsync is idempotent and skipping the DROP
        // avoids long waits on lingering connections.
        var db = new ProductDbContext(new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql(containers.ProductsDb.GetConnectionString())
            .Options);
        await db.Database.EnsureCreatedAsync();
        return new EfOrderRepository(db);
    }
}
