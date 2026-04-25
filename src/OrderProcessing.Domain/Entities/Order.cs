using OrderProcessing.Domain.Enums;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Exceptions;

namespace OrderProcessing.Domain.Entities;

public class Order
{
    private readonly List<OrderItem> _items = new();
    private readonly List<DomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string CustomerEmail { get; private set; } = string.Empty;
    public string CustomerName { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // EF Core constructor
    private Order() { }

    public Order(string customerEmail, string customerName, string? idempotencyKey = null)
    {
        if (string.IsNullOrWhiteSpace(customerEmail))
            throw new ArgumentException("Customer email cannot be empty.", nameof(customerEmail));
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name cannot be empty.", nameof(customerName));

        Id = Guid.NewGuid();
        CustomerEmail = customerEmail;
        CustomerName = customerName;
        Status = OrderStatus.Pending;
        TotalAmount = 0;
        IdempotencyKey = idempotencyKey;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException($"Cannot add items to an order in '{Status}' status.");

        var existingItem = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
            throw new OrderDomainException($"Product '{productName}' is already in the order. Adjust the quantity instead.");

        var item = new OrderItem(Id, productId, productName, unitPrice, quantity);
        _items.Add(item);
        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException($"Cannot confirm an order in '{Status}' status.");
        if (!_items.Any())
            throw new OrderDomainException("Cannot confirm an order with no items.");

        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderPlacedEvent(Id, CustomerEmail, CustomerName, TotalAmount, _items.ToList()));
    }

    public void StartPaymentProcessing()
    {
        if (Status != OrderStatus.Confirmed)
            throw new OrderDomainException($"Cannot start payment processing for an order in '{Status}' status.");

        Status = OrderStatus.PaymentProcessing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.PaymentProcessing)
            throw new OrderDomainException($"Cannot mark an order as paid when in '{Status}' status.");

        Status = OrderStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaymentFailed(string reason)
    {
        if (Status != OrderStatus.PaymentProcessing)
            throw new OrderDomainException($"Cannot mark payment as failed when order is in '{Status}' status.");

        Status = OrderStatus.PaymentFailed;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Delivered)
            throw new OrderDomainException($"Cannot cancel an order in '{Status}' status.");

        Status = OrderStatus.Cancelled;
        FailureReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    private void RecalculateTotal() => TotalAmount = _items.Sum(i => i.TotalPrice);
}