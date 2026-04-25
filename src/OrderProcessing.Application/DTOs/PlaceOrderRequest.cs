namespace OrderProcessing.Application.DTOs;

/// <summary>
/// Request DTO for placing a new order.
/// IdempotencyKey allows safe retries — duplicate requests with the same key return the original response.
/// </summary>
public class PlaceOrderRequest
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Optional client-generated idempotency key (e.g., UUID).
    /// If provided, duplicate requests return the original order rather than creating a new one.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public List<OrderItemRequest> Items { get; set; } = new();
}

public class OrderItemRequest
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}