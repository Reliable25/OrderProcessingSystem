using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderProcessing.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context) => _context = context;

        public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        /// <summary>
        /// Acquires a pessimistic write lock on the product row.
        /// Uses SQL Server UPDLOCK; falls back to tracked read otherwise.
        /// Caller is expected to call this inside a transaction when strong concurrency guarantees are required.
        /// </summary>
        public async Task<Product?> GetByIdWithLockAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var dbProvider = _context.Database.ProviderName ?? string.Empty;

            if (dbProvider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                // SQL Server: acquire update lock on the row
                return await _context.Products
                    .FromSqlInterpolated($"SELECT * FROM Products WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
                    .AsTracking()
                    .FirstOrDefaultAsync(cancellationToken);
            }

            if (dbProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                // PostgreSQL: FOR UPDATE
                var products = await _context.Products
                    .FromSqlRaw(@"SELECT * FROM ""Products"" WHERE ""Id"" = {0} FOR UPDATE", id)
                    .ToListAsync(cancellationToken);
                return products.FirstOrDefault();
            }

            // Fallback: tracked read - rely on surrounding transaction isolation
            return await _context.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default) =>
            await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

        public async Task<IEnumerable<Product>> GetAllActiveAsync(CancellationToken cancellationToken = default) =>
            await _context.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(cancellationToken);

        public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
            await _context.Products.AddAsync(product, cancellationToken);

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            _context.Products.Update(product);
            return Task.CompletedTask;
        }
    }
}