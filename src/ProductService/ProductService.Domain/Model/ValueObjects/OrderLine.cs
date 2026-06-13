using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Domain.Model.ValueObjects;

public sealed class OrderLine : IEquatable<OrderLine>
{
    public ProductId ProductId { get; private set; }
    public Quantity Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    public Money LineTotal => UnitPrice * Quantity.Value;

    private OrderLine()
    {
        Quantity = Quantity.Zero;
        UnitPrice = Money.Zero("TWD");
    }

    public OrderLine(ProductId productId, Quantity quantity, Money unitPrice)
    {
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public static OrderLine Create(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new DomainException("OrderLine quantity must be positive.");
        if (unitPrice.Amount < 0)
            throw new DomainException("OrderLine unit price must be non-negative.");
        return new OrderLine(productId, Quantity.Of(quantity), unitPrice);
    }

    public bool Equals(OrderLine? other) => other is not null
        && ProductId.Equals(other.ProductId)
        && Quantity.Equals(other.Quantity)
        && UnitPrice.Equals(other.UnitPrice);

    public override bool Equals(object? obj) => obj is OrderLine ol && Equals(ol);
    public override int GetHashCode() => HashCode.Combine(ProductId, Quantity, UnitPrice);
}
