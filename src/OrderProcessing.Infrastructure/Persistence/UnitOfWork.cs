using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.Repositories;
using System.Data;
using Microsoft.EntityFrameworkCore;

namespace OrderProcessing.Infrastructure.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IOrderRepository Orders { get; }
        public IProductRepository Products { get; }
        public IIdempotencyRepository Idempotency { get; }

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
            Orders = new OrderRepository(_context);
            Products = new ProductRepository(_context);
            Idempotency = new IdempotencyRepository(_context);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);

        public void Dispose() => _context.Dispose();

        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default)
        {
            var isolation = IsolationLevel.Serializable;
            await using var tx = await _context.Database.BeginTransactionAsync(isolation, cancellationToken);
            try
            {
                await action();
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default)
        {
            var isolation = IsolationLevel.Serializable;
            await using var tx = await _context.Database.BeginTransactionAsync(isolation, cancellationToken);
            try
            {
                var result = await action();
                await _context.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}