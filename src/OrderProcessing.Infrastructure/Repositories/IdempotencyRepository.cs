using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure.Repositories
{
    public class IdempotencyRepository : IIdempotencyRepository
    {
        private readonly AppDbContext _context;
        public IdempotencyRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
            => await _context.Set<IdempotencyRecord>().AddAsync(record, cancellationToken);

        public Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
        {
            _context.Set<IdempotencyRecord>().Update(record);
            return Task.CompletedTask;
        }

        public Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
            _context.Set<IdempotencyRecord>().FirstOrDefaultAsync(r => r.Key == key, cancellationToken);
    }
}
