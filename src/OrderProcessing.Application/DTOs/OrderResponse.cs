using OrderProcessing.Domain.Enums;

namespace OrderProcessing.Application.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string StatusDescription => Status.ToString();
    public decimal TotalAmount { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public bool IsIdempotentDuplicate { get; set; } = false;
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
}