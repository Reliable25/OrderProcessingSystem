using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.HostedServices
{
    /// <summary>
    /// Periodically cleans up old idempotency records to prevent unbounded table growth.
    /// Only deletes Completed/Failed records older than configured TTL.
    /// </summary>
    public class IdempotencyCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<IdempotencyCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);
        private readonly TimeSpan _ttl = TimeSpan.FromDays(30);

        public IdempotencyCleanupService(IServiceProvider services, ILogger<IdempotencyCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("IdempotencyCleanupService started. TTL: {Ttl}", _ttl);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var cutoff = DateTime.UtcNow - _ttl;
                    var toDelete = await db.IdempotencyRecords
                        .Where(r => (r.Status == Domain.Entities.IdempotencyStatus.Completed || r.Status == Domain.Entities.IdempotencyStatus.Failed) && r.UpdatedAt < cutoff)
                        .ToListAsync(stoppingToken);

                    if (toDelete.Any())
                    {
                        db.IdempotencyRecords.RemoveRange(toDelete);
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Deleted {Count} old idempotency records.", toDelete.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while cleaning idempotency records.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
