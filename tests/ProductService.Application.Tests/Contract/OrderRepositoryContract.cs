using FluentAssertions;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace ProductService.Application.Tests.Contract;

/// <summary>
/// Behaviour contract every IOrderWriteRepository implementation must satisfy.
/// Inherit and provide a real repository factory — in-memory tests run here
/// instantly; EF Core + Postgres tests inherit the same fixtures in
/// ProductService.Infrastructure.Tests.
/// </summary>
public abstract class OrderRepositoryContract
{
    protected abstract Task<IOrderWriteRepository> CreateAsync();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task FindAsync_AfterAdd_ReturnsSameOrder()
    {
        var repo = await CreateAsync();
        var order = Order.Place(CustomerId.New(),
            [OrderLine.Create(ProductId.New(), 1, new Money(10m, "TWD"))]);
        await repo.AddAsync(order);

        var found = await repo.FindAsync(order.Id);
        found.Should().NotBeNull();
        found!.Id.Should().Be(order.Id);
        found.Lines.Should().HaveCount(1);
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
    public async Task UpdateAsync_PersistsStatusTransition()
    {
        var repo = await CreateAsync();
        var order = Order.Place(CustomerId.New(),
            [OrderLine.Create(ProductId.New(), 1, new Money(10m, "TWD"))]);
        await repo.AddAsync(order);
        order.MarkPaid(new PaymentId(Guid.NewGuid()));
        await repo.UpdateAsync(order);

        var reloaded = await repo.FindAsync(order.Id);
        reloaded!.Status.Should().BeOfType<OrderStatus.Paid>();
    }
}

public class InMemoryOrderRepositoryContractTests : OrderRepositoryContract
{
    protected override Task<IOrderWriteRepository> CreateAsync()
        => Task.FromResult<IOrderWriteRepository>(new InMemoryRepo());

    private sealed class InMemoryRepo : IOrderWriteRepository
    {
        private readonly Dictionary<OrderId, Order> _store = new();
        public Task AddAsync(Order o, CancellationToken ct = default)
        { _store[o.Id] = o; return Task.CompletedTask; }
        public Task UpdateAsync(Order o, CancellationToken ct = default)
        { _store[o.Id] = o; return Task.CompletedTask; }
        public Task<Order?> FindAsync(OrderId id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));
    }
}
