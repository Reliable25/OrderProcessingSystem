using Microsoft.Extensions.Logging;
using Polly;
using OrderProcessing.Application.EventHandlers;

namespace OrderProcessing.Infrastructure.Service
{
    /// <summary>
    /// Decorator around an IPaymentService that applies resilience policies (retry, timeout, circuit-breaker).
    /// Accepts the payment abstraction so it can be decorated and tested with mocks.
    /// </summary>
    public class ResilientPaymentService : IPaymentService
    {
        private readonly IPaymentService _inner;
        private readonly IAsyncPolicy<bool> _policy;
        private readonly ILogger<ResilientPaymentService> _logger;

        public ResilientPaymentService(
            IPaymentService inner,
            IAsyncPolicy<bool> policy,
            ILogger<ResilientPaymentService> logger)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<bool> ProcessPaymentAsync(Guid orderId, decimal amount, string customerEmail, CancellationToken cancellationToken = default)
        {
            // Execute the inner payment call under the policy. Policy handles exceptions and false results as transient.
            return _policy.ExecuteAsync(ct => _inner.ProcessPaymentAsync(orderId, amount, customerEmail, ct), cancellationToken);
        }
    }
}