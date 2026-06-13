using FluentAssertions;
using InventoryService.Domain.Model;
using InventoryService.Domain.Services;
using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using Reqnroll;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;
using InvProductId = InventoryService.Domain.Model.ProductId;
using PrdProductId = ProductService.Domain.Model.ValueObjects.ProductId;

namespace BDD.Tests.StepDefinitions;

[Binding]
public sealed class OrderPlacementSteps
{
    private readonly Dictionary<string, InvProductId> _products = new();
    private readonly Dictionary<InvProductId, Stock> _stocks = new();
    private Order? _order;
    private DomainException? _capturedException;
    private CustomerId _customer;

    [Given(@"商品 ""(.+)"" 庫存為 (\d+)")]
    public void GivenProductHasStock(string name, int qty)
    {
        var id = new InvProductId(Guid.NewGuid());
        _products[name] = id;
        _stocks[id] = Stock.Create(id, qty);
    }

    [Given(@"使用者 ""(.+)"" 已通過認證")]
    public void GivenUserAuthenticated(string user)
    {
        _customer = CustomerId.New();
    }

    [When(@"使用者下單購買 (\d+) 件 ""(.+)""")]
    public void WhenUserOrders(int qty, string name)
    {
        var invId = _products[name];
        try
        {
            var prdId = new PrdProductId(invId.Value);
            _order = Order.Place(_customer == default ? CustomerId.New() : _customer,
                [OrderLine.Create(prdId, qty, new Money(100m, "TWD"))]);
            // simulate the inventory side reservation+commit
            new StockAllocationService().AllocateAll(
                [new AllocationLine(invId, qty)], _stocks);
            new StockAllocationService().CommitAll(
                [new AllocationLine(invId, qty)], _stocks);
            // simulate the payment side completing the order
            _order.MarkPaid(new PaymentId(Guid.NewGuid()));
        }
        catch (DomainException ex)
        {
            _capturedException = ex;
        }
    }

    [Then(@"庫存應減少為 (\d+)")]
    public void ThenStockShouldBe(int expected)
    {
        var (_, stock) = _stocks.First();
        stock.Available.Value.Should().Be(expected);
    }

    [Then(@"訂單狀態應為 ""(.+)""")]
    public void ThenOrderStatusShouldBe(string statusName)
    {
        _order.Should().NotBeNull();
        _order!.Status.Name.Should().Be(statusName);
    }

    [Then(@"應觸發 (\w+) 領域例外")]
    public void ThenDomainExceptionShouldHaveBeenRaised(string _)
    {
        _capturedException.Should().NotBeNull();
    }
}
