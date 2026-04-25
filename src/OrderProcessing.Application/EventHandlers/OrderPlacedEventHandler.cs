using Microsoft.Extensions.Logging;
using OrderProcessing.Domain.Events;
using OrderProcessing.Domain.Interfaces;

namespace OrderProcessing.Application.EventHandlers;

/// <summary>
/// Orchestrates the post-order pipeline by handling the OrderPlacedEvent.
///
/// Pipeline:
///   OrderPlaced → PaymentProcessing → InventoryUpdateConfirmation → CustomerNotification
///
/// Each step is independent and logged. In production, each of these could be
/// a separate message queue consumer (e.g., RabbitMQ/Azure Service Bus).
/// </summary>
public class OrderPlacedEventHandler
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderPlacedEventHandler> _logger;

    public OrderPlacedEventHandler(
        IUnitOfWork unitOfWork,
        IPaymentService paymentService,
        INotificationService notificationService,
        ILogger<OrderPlacedEventHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _paymentService = paymentService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPlacedEvent orderPlacedEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EVENT] OrderPlaced received: OrderId={OrderId}, Customer={Email}, Total={Total:C}, Items={ItemCount}",
            orderPlacedEvent.OrderId, orderPlacedEvent.CustomerEmail,
            orderPlacedEvent.TotalAmount, orderPlacedEvent.Items.Count);

        // Step 1: Process payment
        await ProcessPaymentAsync(orderPlacedEvent, cancellationToken);

        // Step 2: Confirm inventory update
        await ConfirmInventoryUpdateAsync(orderPlacedEvent, cancellationToken);

        // Step 3: Send customer notification
        await SendNotificationAsync(orderPlacedEvent, cancellationToken);
    }

    private async Task ProcessPaymentAsync(OrderPlacedEvent evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[PAYMENT] Initiating payment for Order {OrderId}, Amount: {Amount:C}",
            evt.OrderId, evt.TotalAmount);

        var order = await _unitOfWork.Orders.GetByIdAsync(evt.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogError("[PAYMENT] Order {OrderId} not found for payment processing.", evt.OrderId);
            return;
        }

        try
        {
            order.StartPaymentProcessing();
            await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var paymentSuccess = await _paymentService.ProcessPaymentAsync(evt.OrderId, evt.TotalAmount, evt.CustomerEmail, cancellationToken);

            if (paymentSuccess)
            {
                order.MarkAsPaid();
                _logger.LogInformation("[PAYMENT] Payment successful for Order {OrderId}.", evt.OrderId);
            }
            else
            {
                order.MarkPaymentFailed("Payment gateway declined the transaction.");
                _logger.LogWarning("[PAYMENT] Payment failed for Order {OrderId}.", evt.OrderId);
            }

            await _unitOfWork.Orders.UpdateAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PAYMENT] Exception during payment processing for Order {OrderId}.", evt.OrderId);
        }
    }

    private async Task ConfirmInventoryUpdateAsync(OrderPlacedEvent evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[INVENTORY] Confirming inventory update for Order {OrderId}.", evt.OrderId);

        foreach (var item in evt.Items)
        {
            _logger.LogInformation(
                "[INVENTORY] Stock reserved: ProductId={ProductId}, ProductName={Name}, Quantity={Qty}",
                item.ProductId, item.ProductName, item.Quantity);
        }

        // Simulate async inventory system confirmation (e.g., ERP/WMS callback)
        await Task.Delay(10, cancellationToken);

        _logger.LogInformation(
            "[INVENTORY] Inventory update confirmed for Order {OrderId}. {ItemCount} product(s) updated.",
            evt.OrderId, evt.Items.Count);
    }

    private async Task SendNotificationAsync(OrderPlacedEvent evt, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[NOTIFICATION] Sending order confirmation to {Email} for Order {OrderId}.",
            evt.CustomerEmail, evt.OrderId);

        await _notificationService.SendOrderConfirmationAsync(
            evt.CustomerEmail, evt.CustomerName, evt.OrderId, evt.TotalAmount, cancellationToken);

        _logger.LogInformation(
            "[NOTIFICATION] Order confirmation sent to {Email} for Order {OrderId}.",
            evt.CustomerEmail, evt.OrderId);
    }
}

/// <summary>Payment service abstraction — swap with real gateway in production.</summary>
public interface IPaymentService
{
    Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount, string customerEmail, CancellationToken cancellationToken = default);
}

/// <summary>Notification service abstraction — swap with real email/SMS provider in production.</summary>
public interface INotificationService
{
    Task SendOrderConfirmationAsync(string email, string customerName, Guid orderId, decimal totalAmount, CancellationToken cancellationToken = default);
}