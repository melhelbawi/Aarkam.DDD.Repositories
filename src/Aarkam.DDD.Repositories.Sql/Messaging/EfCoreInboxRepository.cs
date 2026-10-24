using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarkam.DDD.Repositories.Sql.Messaging;

/// <summary>
/// Entity Framework Core implementation of <see cref="IInboxRepository"/> for idempotent consumer event deduplication.
/// </summary>
public class EfCoreInboxRepository : IInboxRepository
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<InboxMessageRecord> _dbSet;

    public EfCoreInboxRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<InboxMessageRecord>();
    }

    /// <inheritdoc />
    public virtual async Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consumerName))
            throw new ArgumentException("Consumer name cannot be null or whitespace.", nameof(consumerName));

        return await _dbSet.AnyAsync(m => m.EventId == eventId && m.ConsumerName == consumerName, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task RecordAsync(IInboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        if (message is InboxMessageRecord record)
        {
            await _dbSet.AddAsync(record, cancellationToken);
        }
        else
        {
            var newRecord = new InboxMessageRecord
            {
                EventId = message.EventId,
                EventType = message.EventType,
                ConsumerName = message.ConsumerName,
                ProcessedAt = message.ProcessedAt == default ? DateTimeOffset.UtcNow : message.ProcessedAt,
                TenantId = message.TenantId
            };
            await _dbSet.AddAsync(newRecord, cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        var staleRecords = await _dbSet
            .Where(m => m.ProcessedAt < olderThan)
            .ToListAsync(cancellationToken);

        if (staleRecords.Count == 0)
            return 0;

        _dbSet.RemoveRange(staleRecords);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return staleRecords.Count;
    }
}
