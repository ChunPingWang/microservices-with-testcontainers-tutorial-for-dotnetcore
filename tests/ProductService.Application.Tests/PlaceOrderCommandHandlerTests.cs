using FluentAssertions;
using NSubstitute;
using ProductService.Application.Commands;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using ProductService.Domain.Ports.Outbound;
using ProductService.Domain.Services;
using SharedKernel.Domain;
using SharedKernel.Domain.Events;
using SharedKernel.Domain.Ports;
using SharedKernel.Domain.ValueObjects;
using Xunit;

namespace ProductService.Application.Tests;

public class PlaceOrderCommandHandlerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_HappyPath_PersistsOrderAndPublishesEvent()
    {
        var productId = ProductId.New();
        var customerId = CustomerId.New();
        var product = new Product(productId, "iPhone", "Smartphone", new Money(35000m, "TWD"));

        var products = new InMemoryProductRepository([product]);
        var orders = new InMemoryOrderRepository();
        var publisher = new CapturingEventPublisher();

        var handler = new PlaceOrderCommandHandler(products, orders, publisher,
            new PricingService(), TimeProvider.System);

        var cmd = new PlaceOrderCommand(customerId.Value,
            [new OrderLineDto(productId.Value, 2)]);

        var id = await handler.Handle(cmd, CancellationToken.None);

        var stored = await orders.FindAsync(new OrderId(id));
        stored.Should().NotBeNull();
        stored!.Total.Should().Be(new Money(70000m, "TWD"));
        publisher.Captured.Should().ContainSingle().Which
            .Should().BeOfType<OrderCreatedIntegrationEvent>()
            .Which.OrderId.Should().Be(id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_UnknownProduct_Throws()
    {
        var handler = new PlaceOrderCommandHandler(
            new InMemoryProductRepository([]),
            new InMemoryOrderRepository(),
            new CapturingEventPublisher(),
            new PricingService(), TimeProvider.System);

        var cmd = new PlaceOrderCommand(Guid.NewGuid(),
            [new OrderLineDto(Guid.NewGuid(), 1)]);

        await FluentActions.Invoking(() => handler.Handle(cmd, CancellationToken.None))
            .Should().ThrowAsync<DomainException>();
    }

    private sealed class InMemoryProductRepository(IEnumerable<Product> seed) : IProductRepository
    {
        private readonly Dictionary<ProductId, Product> _store = seed.ToDictionary(p => p.Id);

        public Task<Product?> GetAsync(ProductId id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Product>> GetManyAsync(
            IReadOnlyCollection<ProductId> ids, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Product>>(
                [.. ids.Where(_store.ContainsKey).Select(i => _store[i])]);

        public Task AddAsync(Product product, CancellationToken ct = default)
        {
            _store[product.Id] = product;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken ct = default)
        {
            _store[product.Id] = product;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryOrderRepository : IOrderWriteRepository, IOrderReadRepository
    {
        private readonly Dictionary<OrderId, Order> _store = new();

        public Task AddAsync(Order order, CancellationToken ct = default)
        { _store[order.Id] = order; return Task.CompletedTask; }

        public Task UpdateAsync(Order order, CancellationToken ct = default)
        { _store[order.Id] = order; return Task.CompletedTask; }

        public Task<Order?> FindAsync(OrderId id, CancellationToken ct = default)
            => Task.FromResult(_store.GetValueOrDefault(id));

        Task<Order?> IOrderReadRepository.GetAsync(OrderId id, CancellationToken ct)
            => FindAsync(id, ct);

        public Task<IReadOnlyList<Order>> ListByCustomerAsync(CustomerId customerId,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Order>>(
                [.. _store.Values.Where(o => o.CustomerId == customerId)]);
    }

    private sealed class CapturingEventPublisher : IEventPublisher
    {
        public List<IIntegrationEvent> Captured { get; } = [];

        public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
            where TEvent : class, IIntegrationEvent
        {
            Captured.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }
}
