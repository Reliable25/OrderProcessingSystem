namespace OrderProcessing.Domain.Exceptions;

public class ProductNotFoundException : Exception
{
    public Guid ProductId { get; }

    public ProductNotFoundException(Guid productId)
        : base($"Product with ID '{productId}' was not found.")
    {
        ProductId = productId;
    }
}