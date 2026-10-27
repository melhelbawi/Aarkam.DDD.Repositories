using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.DDD.Repositories.Sql.Repositories;

/// <summary>
/// Entity Framework Core implementation of the pure Domain-Driven Design <see cref="IAggregateRepository{TAggregate}"/> pattern.
/// Enforces consistency boundaries by ensuring child entities cannot be independently persisted outside their aggregate root,
/// extracts uncommitted domain events, and dispatches them atomically alongside state transitions.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
public class EfCoreAggregateRepository<TAggregate> : EfCoreRepository<TAggregate>, IAggregateRepository<TAggregate>
    where TAggregate : class, IAggregateRoot, IEntity
{
    protected readonly IOutboxRepository? _outboxRepository;

    public EfCoreAggregateRepository(DbContext dbContext) : base(dbContext)
    {
    }

    public EfCoreAggregateRepository(DbContext dbContext, IOutboxRepository? outboxRepository) : base(dbContext)
    {
        _outboxRepository = outboxRepository;
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        var entry = _dbContext.Entry(aggregate);

        if (entry.State == EntityState.Detached)
        {
            await AttachOrAddDetachedAsync(aggregate, cancellationToken);
        }

        // Domain event extraction & transactional outbox dispatching
        if (aggregate is IHasDomainEvents eventSource)
        {
            var changes = eventSource.GetChanges();
            if (changes != null && changes.Count > 0 && _outboxRepository != null)
            {
                foreach (var domainEvent in changes)
                {
                    await StageOutboxMessageAsync(domainEvent, cancellationToken);
                }
            }
        }

        // Persist aggregate state transitions to the relational store
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Mark aggregate domain events as committed once transaction successfully completes
        if (aggregate is IHasDomainEvents committedEventSource)
        {
            committedEventSource.MarkEventsAsCommitted();
        }
    }

    /// <summary>
    /// Attaches or adds a detached aggregate root to the change tracker.
    /// </summary>
    protected virtual async Task AttachOrAddDetachedAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        var entry = _dbContext.Entry(aggregate);
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey == null)
        {
            await _dbSet.AddAsync(aggregate, cancellationToken);
            return;
        }

        var keyValues = primaryKey.Properties
            .Select(p => entry.Property(p.Name).CurrentValue)
            .ToArray();

        var existing = await _dbSet.FindAsync(keyValues, cancellationToken);
        if (existing != null)
        {
            if (!ReferenceEquals(existing, aggregate))
            {
                _dbContext.Entry(existing).CurrentValues.SetValues(aggregate);
            }
            else
            {
                _dbSet.Update(aggregate);
            }
        }
        else
        {
            await _dbSet.AddAsync(aggregate, cancellationToken);
        }
    }

    /// <summary>
    /// Stages an uncommitted domain event into the transactional outbox repository.
    /// </summary>
    protected virtual async Task StageOutboxMessageAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (_outboxRepository == null || domainEvent == null) return;

        var message = new OutboxMessageRecord
        {
            Id = domainEvent.EventId == Guid.Empty ? Guid.NewGuid() : domainEvent.EventId,
            EventType = domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            CreatedAt = domainEvent.OccurredOn == default ? DateTimeOffset.UtcNow : domainEvent.OccurredOn,
            CorrelationId = Guid.NewGuid()
        };

        await _outboxRepository.AddAsync(message, cancellationToken);
    }
}

/// <summary>
/// Entity Framework Core implementation of the pure Domain-Driven Design <see cref="IAggregateRepository{TAggregate, TKey}"/> pattern
/// for aggregate roots with a strongly typed identity key.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The identity type of the aggregate root.</typeparam>
public class EfCoreAggregateRepository<TAggregate, TKey> : EfCoreAggregateRepository<TAggregate>, IAggregateRepository<TAggregate, TKey>, IBasicRepository<TAggregate, TKey>, IReadOnlyRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    public EfCoreAggregateRepository(DbContext dbContext) : base(dbContext)
    {
    }

    public EfCoreAggregateRepository(DbContext dbContext, IOutboxRepository? outboxRepository) : base(dbContext, outboxRepository)
    {
    }

    // =========================================================================
    // Storage-Agnostic & Basic Read/Delete Operations with Key
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<TAggregate?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object?[] { id }, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public virtual Task<TAggregate?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(e => e.Id!.Equals(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return false;

        _dbSet.Remove(entity);
        return true;
    }

    /// <inheritdoc />
    protected override async Task AttachOrAddDetachedAsync(TAggregate aggregate, CancellationToken cancellationToken)
    {
        var existing = await _dbSet.FindAsync(new object?[] { aggregate.Id }, cancellationToken);
        if (existing != null)
        {
            if (!ReferenceEquals(existing, aggregate))
            {
                _dbContext.Entry(existing).CurrentValues.SetValues(aggregate);
            }
            else
            {
                _dbSet.Update(aggregate);
            }
        }
        else
        {
            await _dbSet.AddAsync(aggregate, cancellationToken);
        }
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreAggregateRepository{TAggregate}"/> binding directly to a specific <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TDbContext">The application DbContext type.</typeparam>
public class EfCoreContextAggregateRepository<TAggregate, TDbContext> : EfCoreAggregateRepository<TAggregate>
    where TAggregate : class, IAggregateRoot, IEntity
    where TDbContext : DbContext
{
    public EfCoreContextAggregateRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    public EfCoreContextAggregateRepository(TDbContext dbContext, IOutboxRepository? outboxRepository) : base(dbContext, outboxRepository)
    {
    }

    /// <summary>
    /// Gets the strongly typed application <typeparamref name="TDbContext"/> instance.
    /// </summary>
    public TDbContext Context => (TDbContext)_dbContext;
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreAggregateRepository{TAggregate, TKey}"/> binding to a specific <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The identity type of the aggregate root.</typeparam>
/// <typeparam name="TDbContext">The application DbContext type.</typeparam>
public class EfCoreAggregateRepository<TAggregate, TKey, TDbContext> : EfCoreAggregateRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
    where TDbContext : DbContext
{
    public EfCoreAggregateRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    public EfCoreAggregateRepository(TDbContext dbContext, IOutboxRepository? outboxRepository) : base(dbContext, outboxRepository)
    {
    }

    /// <summary>
    /// Gets the strongly typed application <typeparamref name="TDbContext"/> instance.
    /// </summary>
    public TDbContext Context => (TDbContext)_dbContext;
}
