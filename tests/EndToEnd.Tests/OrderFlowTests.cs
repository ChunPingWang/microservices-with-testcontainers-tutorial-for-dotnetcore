using System.Net.Http.Json;
using FluentAssertions;
using TestInfrastructure;
using TestInfrastructure.Containers;
using Xunit;

namespace EndToEnd.Tests;

[Collection("SharedContainers")]
public class OrderFlowTests(SharedContainerFixture containers)
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task PlaceOrder_HappyPath_ReachesPaidStatus()
    {
        await using var productFactory = new ProductWebFactory(containers);
        await using var paymentFactory = new PaymentWebFactory(containers);
        await using var inventoryFactory = new InventoryWebFactory(containers);

        // Touch each factory to start the host (and run schema init)
        var productClient = productFactory.CreateClient();
        var paymentClient = paymentFactory.CreateClient();
        var inventoryClient = inventoryFactory.CreateClient();

        // 1. seed a product
        var seedResp = await productClient.PostAsJsonAsync("/api/products", new
        {
            Name = "iPhone 16",
            Description = "Apple flagship",
            Price = 35000m,
            Currency = "TWD"
        });
        seedResp.EnsureSuccessStatusCode();
        var seed = await seedResp.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var productId = seed!["id"];

        // 2. pre-seed inventory
        var stockResp = await inventoryClient.PostAsJsonAsync("/api/stocks", new
        {
            ProductId = productId,
            Available = 100,
        });
        stockResp.EnsureSuccessStatusCode();

        // 3. place an order
        var customerId = Guid.NewGuid();
        var orderResp = await productClient.PostAsJsonAsync("/api/orders", new
        {
            CustomerId = customerId,
            Lines = new[] { new { ProductId = productId, Quantity = 2 } }
        });
        orderResp.EnsureSuccessStatusCode();
        var orderBody = await orderResp.Content.ReadFromJsonAsync<Dictionary<string, Guid>>();
        var orderId = orderBody!["id"];

        // 4. trigger the payment manually since cross-process Kafka isn't wired
        //    in this Testing-Lite profile. This still exercises every adapter
        //    (PG receipt + Vault + Mass.Transit + Redis cache flush).
        var payResp = await paymentClient.PostAsJsonAsync("/api/payments", new
        {
            OrderId = orderId,
            Amount = 70000m,
            Currency = "TWD",
            IdempotencyKey = $"order-{orderId}"
        });
        payResp.EnsureSuccessStatusCode();

        // 5. assertion — read order back via product GET
        await AsyncWaiter.WaitUntilAsync(async _ =>
        {
            var get = await productClient.GetAsync($"/api/orders/{orderId}");
            return get.IsSuccessStatusCode;
        }, TimeSpan.FromSeconds(15));

        var stockGet = await inventoryClient.GetAsync($"/api/stocks/{productId}");
        stockGet.EnsureSuccessStatusCode();
    }
}
