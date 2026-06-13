using ProductService.Domain.Model;
using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Domain.Services;

public sealed class PricingService
{
    public Money PriceLines(IEnumerable<OrderLineDraft> lines,
        IReadOnlyDictionary<ProductId, Product> catalog)
    {
        var list = lines?.ToList() ?? throw new ArgumentNullException(nameof(lines));
        if (list.Count == 0)
            throw new DomainException("Cannot price an empty order.");

        string? currency = null;
        var total = Money.Zero("UNSET");

        foreach (var draft in list)
        {
            if (!catalog.TryGetValue(draft.ProductId, out var product))
                throw new DomainException($"Product {draft.ProductId} not in catalog.");
            if (!product.IsActive)
                throw new DomainException($"Product {product.Name} is not active.");

            currency ??= product.Price.Currency;
            if (total.Currency == "UNSET") total = Money.Zero(currency);

            total += product.Price * draft.Quantity;
        }

        return total;
    }

    public IReadOnlyList<OrderLine> BuildLines(IEnumerable<OrderLineDraft> drafts,
        IReadOnlyDictionary<ProductId, Product> catalog)
        => [.. drafts.Select(d =>
            OrderLine.Create(d.ProductId, d.Quantity, catalog[d.ProductId].Price))];
}

public readonly record struct OrderLineDraft(ProductId ProductId, int Quantity);
