using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Domain.Events;

public class OrderPlacedEvent : DomainEvent
{
    public override string EventType => "OrderPlaced";

    public Guid OrderId { get; }
    public string CustomerEmail { get; }
    public string CustomerName { get; }
    public decimal TotalAmount { get; }
    public IReadOnlyList<OrderItemSnapshot> Items { get; }

    public OrderPlacedEvent(
        Guid orderId,
        string customerEmail,
        string customerName,
        decimal totalAmount,
        IReadOnlyList<OrderItem> items)
    {
        OrderId = orderId;
        CustomerEmail = customerEmail;
        CustomerName = customerName;
        TotalAmount = totalAmount;
        Items = items.Select(i => new OrderItemSnapshot(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity)).ToList();
    }
}

public record OrderItemSnapshot(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity);