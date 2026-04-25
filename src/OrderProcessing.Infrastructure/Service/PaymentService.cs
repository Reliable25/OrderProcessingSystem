using Microsoft.Extensions.Logging;
using OrderProcessing.Application.EventHandlers;

namespace OrderProcessing.Infrastructure.Service
{
    public class PaymentService : IPaymentService
    {
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(ILogger<PaymentService> logger) => _logger = logger;

        public async Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount, string customerEmail, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("[SIM PAYMENT] Processing payment for Order {OrderId} Amount={Amount}", orderId, amount);
            await Task.Delay(200, cancellationToken);
            _logger.LogInformation("[SIM PAYMENT] Payment processed for Order {OrderId}", orderId);
            return true;
        }
    }
}