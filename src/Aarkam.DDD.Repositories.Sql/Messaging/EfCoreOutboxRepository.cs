using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarkam.DDD.Repositories.Sql.Messaging;

/// <summary>
/// Entity Framework Core implementation of <see cref="IOutboxRepository"/> for transactional outbox dispatching.
/// </summary>
public class EfCoreOutboxRepository : IOutboxRepository
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<OutboxMessageRecord> _dbSet;

    public EfCoreOutboxRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<OutboxMessageRecord>();
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(IOutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null) throw new ArgumentNullException(nameof(message));

        if (message is OutboxMessageRecord record)
        {
            await _dbSet.AddAsync(record, cancellationToken);
        }
        else
        {
            var newRecord = new OutboxMessageRecord
            {
                Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id,
                EventType = message.EventType,
                Payload = message.Payload,
                CreatedAt = message.CreatedAt == default ? DateTimeOffset.UtcNow : message.CreatedAt,
                ProcessedAt = message.ProcessedAt,
                RetryCount = message.RetryCount,
                LastError = message.LastError,
                TenantId = message.TenantId,
                CorrelationId = message.CorrelationId == Guid.Empty ? Guid.NewGuid() : message.CorrelationId
            };
            await _dbSet.AddAsync(newRecord, cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<IOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) return Array.Empty<IOutboxMessage>();

        return await _dbSet
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await _dbSet.FindAsync(new object?[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.MarkAsProcessed();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task RecordFailureAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        var message = await _dbSet.FindAsync(new object?[] { messageId }, cancellationToken);
        if (message != null)
        {
            message.LogFailure(error);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public virtual async Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        var staleMessages = await _dbSet
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < olderThan)
            .ToListAsync(cancellationToken);

        if (staleMessages.Count == 0)
            return 0;

        _dbSet.RemoveRange(staleMessages);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return staleMessages.Count;
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreOutboxRepository"/> supporting custom message entity models.
/// </summary>
/// <typeparam name="TMessage">The outbox message entity type.</typeparam>
public class EfCoreOutboxRepository<TMessage> : EfCoreOutboxRepository, IOutboxRepository<TMessage>
    where TMessage : class, IOutboxMessage
{
    private readonly DbSet<TMessage> _typedDbSet;

    public EfCoreOutboxRepository(DbContext dbContext) : base(dbContext)
    {
        _typedDbSet = dbContext.Set<TMessage>();
    }

    /// <inheritdoc />
    public new async Task<IReadOnlyList<TMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) return Array.Empty<TMessage>();

        return await _typedDbSet
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
