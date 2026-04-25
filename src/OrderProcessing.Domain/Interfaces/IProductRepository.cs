using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Domain.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Gets a product with a pessimistic write lock to prevent concurrent overselling.
    /// Must be called within a transaction.
    /// </summary>
    Task<Product?> GetByIdWithLockAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IEnumerable<Product>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
}