namespace OrderProcessing.Domain.Entities;

public class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    // EF Core constructor
    private Product() { }

    public Product(string name, string description, decimal price, int stockQuantity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));
        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));

        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reserves stock. Returns false if insufficient stock.
    /// This is the domain logic — the aggregate root enforces invariants.
    /// </summary>
    public bool TryReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (!IsActive)
            return false;
        if (StockQuantity < quantity)
            return false;

        StockQuantity -= quantity;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        StockQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string description, decimal price)
    {
        Name = name;
        Description = description;
        Price = price;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}