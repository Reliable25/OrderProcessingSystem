namespace OrderProcessing.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty; 
    public decimal UnitPrice { get; private set; }                   
    public int Quantity { get; private set; }
    public decimal TotalPrice => UnitPrice * Quantity;

    // Navigation
    public Order? Order { get; private set; }
    public Product? Product { get; private set; }

    // EF Core constructor
    private OrderItem() { }

    public OrderItem(Guid orderId, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (unitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }
}