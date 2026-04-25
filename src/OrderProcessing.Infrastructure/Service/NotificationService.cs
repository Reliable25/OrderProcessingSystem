using Microsoft.Extensions.Logging;
using OrderProcessing.Application.EventHandlers;

namespace OrderProcessing.Infrastructure.Service;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public Task SendOrderConfirmationAsync(string email, string customerName, Guid orderId, decimal totalAmount, CancellationToken cancellationToken = default)
    {
        // Simulate sending notification
        _logger.LogInformation("[SIM NOTIFICATION] Sending order confirmation to {Email} for Order {OrderId} Amount={Amount}", email, orderId, totalAmount);
        return Task.CompletedTask;
    }
}


