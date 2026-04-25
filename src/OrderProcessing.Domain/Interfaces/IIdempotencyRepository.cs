using OrderProcessing.Domain.Entities;

namespace OrderProcessing.Domain.Interfaces
{
    public interface IIdempotencyRepository
    {
        Task<IdempotencyRecord?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
        Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
        Task UpdateAsync(IdempotencyRecord record, CancellationToken cancellationToken = default);
    }
}
