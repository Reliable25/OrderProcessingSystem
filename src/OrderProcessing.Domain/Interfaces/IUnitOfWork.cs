namespace OrderProcessing.Domain.Interfaces;

/// <summary>
/// Unit of Work pattern - coordinates atomic transactions across repositories.
/// Critical for ensuring inventory deduction and order creation happen atomically.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IOrderRepository Orders { get; }
    IProductRepository Products { get; }
    IIdempotencyRepository Idempotency { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action within a database transaction.
    /// Used for pessimistic locking during stock reservation to prevent overselling.
    /// </summary>
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);

    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
}