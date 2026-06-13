using InventoryService.Domain.Model;
using SharedKernel.Domain;

namespace InventoryService.Domain.Services;

public sealed record AllocationLine(ProductId ProductId, int Quantity);

public sealed class StockAllocationService
{
    public void AllocateAll(IEnumerable<AllocationLine> lines, IDictionary<ProductId, Stock> stocks)
    {
        // All-or-nothing: validate first, then mutate
        var list = lines.ToList();
        foreach (var line in list)
        {
            if (!stocks.TryGetValue(line.ProductId, out var stock))
                throw new DomainException($"Unknown product {line.ProductId}.");
            if (stock.Available.Value < line.Quantity)
                throw new DomainException(
                    $"Insufficient stock for {line.ProductId}: need {line.Quantity}, have {stock.Available}.");
        }
        foreach (var line in list)
            stocks[line.ProductId].Reserve(line.Quantity);
    }

    public void CommitAll(IEnumerable<AllocationLine> lines, IDictionary<ProductId, Stock> stocks)
    {
        foreach (var line in lines)
            stocks[line.ProductId].Commit(line.Quantity);
    }

    public void ReleaseAll(IEnumerable<AllocationLine> lines, IDictionary<ProductId, Stock> stocks)
    {
        foreach (var line in lines)
            stocks[line.ProductId].Release(line.Quantity);
    }
}
