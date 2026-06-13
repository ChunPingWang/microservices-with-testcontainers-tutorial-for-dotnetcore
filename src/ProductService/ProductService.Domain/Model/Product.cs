using ProductService.Domain.Model.ValueObjects;
using SharedKernel.Domain;
using SharedKernel.Domain.ValueObjects;

namespace ProductService.Domain.Model;

public sealed class Product
{
    public ProductId Id { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Money Price { get; private set; }
    public string? ImageStorageKey { get; private set; }
    public bool IsActive { get; private set; }

    // EF Core materialisation
    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        Price = Money.Zero("TWD");
    }

    public Product(ProductId id, string name, string description, Money price,
        string? imageStorageKey = null, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Product name is required.");
        if (price.Amount < 0)
            throw new DomainException("Product price must be non-negative.");

        Id = id;
        Name = name;
        Description = description ?? string.Empty;
        Price = price;
        ImageStorageKey = imageStorageKey;
        IsActive = isActive;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Product name is required.");
        Name = newName;
    }

    public void Reprice(Money newPrice)
    {
        if (newPrice.Amount < 0)
            throw new DomainException("Product price must be non-negative.");
        Price = newPrice;
    }

    public void AttachImage(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new DomainException("Storage key is required.");
        ImageStorageKey = storageKey;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
