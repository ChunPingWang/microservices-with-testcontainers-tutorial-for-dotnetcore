using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace InventoryService.Domain.Model;

public readonly record struct StockId(Guid Value);
public readonly record struct ProductId(Guid Value);

public sealed class Stock
{
    public StockId Id { get; }
    public ProductId ProductId { get; }
    public Quantity Available { get; private set; }
    public Quantity Reserved { get; private set; }
    public uint Version { get; private set; }   // EF Core Concurrency Token

    private Stock(StockId id, ProductId productId, Quantity available, Quantity reserved, uint version)
    {
        Id = id;
        ProductId = productId;
        Available = available;
        Reserved = reserved;
        Version = version;
    }

    public static Stock Create(ProductId productId, int initialAvailable)
        => new(new StockId(Guid.NewGuid()), productId, Quantity.Of(initialAvailable),
            Quantity.Zero, 0);

    public static Stock Restore(StockId id, ProductId productId, int available, int reserved,
        uint version)
        => new(id, productId, Quantity.Of(available), Quantity.Of(reserved), version);

    public void Reserve(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Reservation quantity must be positive.");
        if (Available.Value < quantity)
            throw new DomainException(
                $"Insufficient stock for {ProductId}: requested {quantity}, available {Available}.");
        Available = Quantity.Of(Available.Value - quantity);
        Reserved = Quantity.Of(Reserved.Value + quantity);
    }

    public void Commit(int quantity)
    {
        if (Reserved.Value < quantity)
            throw new DomainException(
                $"Cannot commit {quantity}: only {Reserved} reserved.");
        Reserved = Quantity.Of(Reserved.Value - quantity);
    }

    public void Release(int quantity)
    {
        if (Reserved.Value < quantity)
            throw new DomainException("Release exceeds reserved.");
        Reserved = Quantity.Of(Reserved.Value - quantity);
        Available = Quantity.Of(Available.Value + quantity);
    }

    public void Restock(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Restock must be positive.");
        Available = Quantity.Of(Available.Value + quantity);
    }
}

public sealed record Reservation(Guid OrderId, ProductId ProductId, int Quantity);
