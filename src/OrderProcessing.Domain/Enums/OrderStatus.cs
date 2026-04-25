namespace OrderProcessing.Domain.Enums;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    PaymentProcessing = 2,
    PaymentFailed = 3,
    Paid = 4,
    Processing = 5,
    Shipped = 6,
    Delivered = 7,
    Cancelled = 8
}