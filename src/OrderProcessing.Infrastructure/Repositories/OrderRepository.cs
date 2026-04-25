using OrderProcessing.Domain.Entities;
using OrderProcessing.Domain.Interfaces;
using OrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace OrderProcessing.Infrastructure.Repositories
{
    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public OrderRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            await _context.Orders.AddAsync(order, cancellationToken);
        }

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
        {
            _context.Orders.Update(order);
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default) =>
            _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, cancellationToken);

        public async Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await _context.Orders.Include(o => o.Items).ToListAsync(cancellationToken);

        public async Task<IEnumerable<Order>> GetByCustomerEmailAsync(string email, CancellationToken cancellationToken = default) =>
            await _context.Orders.Include(o => o.Items).Where(o => o.CustomerEmail == email).ToListAsync(cancellationToken);
    }
}