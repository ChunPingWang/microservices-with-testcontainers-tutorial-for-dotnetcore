using Contracts;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;

namespace ProductService.Application.Tests.Contract;

/// <summary>
/// Run the shared <see cref="OrderRepositoryContract"/> against an in-memory
/// implementation — fast, no containers, runs in the Unit/Contract gate of CI.
///
/// The EF Core counterpart lives in
/// <c>tests/ProductService.Infrastructure.Tests/Contract/EfOrderRepositoryContractTests.cs</c>
/// and exercises the same assertions against a real PostgreSQL container.
/// </summary>
public class InMemoryOrderRepositoryContractTests : OrderRepositoryContract
{
    protected override Task<IOrderWriteRepository> CreateAsync()
        => Task.FromResult<IOrderWriteRepository>(new InMemoryOrderRepository());

    private sealed class InMemoryOrderRepository : IOrderWriteRepository
    {
        private readonly Dictionary<OrderId, Order> _store = new();

        public Task AddAsync(Order order, CancellationToken ct = default)
        { _store[order.Id] = order; return Task.CompletedTask; }

        public Task UpdateAsync(Order order, CancellationToken ct = default)
        { _store[order.Id] = order; return Task.CompletedTask; }

        public Task<Order?> FindAsync(OrderId id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));
    }
}
