namespace OrderProcessing.Domain.Exceptions;

public class InsufficientStockException : Exception
{
    public Guid ProductId { get; }
    public string ProductName { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockException(Guid productId, string productName, int requested, int available)
        : base($"Insufficient stock for product '{productName}' (ID: {productId}). Requested: {requested}, Available: {available}.")
    {
        ProductId = productId;
        ProductName = productName;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}