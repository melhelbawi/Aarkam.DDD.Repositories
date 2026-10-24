using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarkam.DDD.Repositories.Sql.Repositories;

/// <summary>
/// Entity Framework Core implementation of the pure Domain-Driven Design <see cref="IAggregateRepository{TAggregate, TKey}"/> pattern.
/// Enforces consistency boundaries by ensuring child entities cannot be independently persisted outside their aggregate root.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TKey">The identity type of the aggregate root.</typeparam>
public class EfCoreAggregateRepository<TAggregate, TKey> : EfCoreRepository<TAggregate, TKey>, IAggregateRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    public EfCoreAggregateRepository(DbContext dbContext) : base(dbContext)
    {
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        var entry = _dbContext.Entry(aggregate);

        if (entry.State == EntityState.Detached)
        {
            // If the entity is detached, determine whether to Add or Attach/Update
            if (await ExistsAsync(aggregate.Id, cancellationToken))
            {
                _dbSet.Update(aggregate);
            }
            else
            {
                await _dbSet.AddAsync(aggregate, cancellationToken);
            }
        }

        // Persist aggregate state transitions to the relational store
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreAggregateRepository{TAggregate, TKey}"/> binding to a specific <typeparamref name="TDbContext"/>.
/// </summary>
public class EfCoreAggregateRepository<TAggregate, TKey, TDbContext> : EfCoreAggregateRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
    where TDbContext : DbContext
{
    public EfCoreAggregateRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    public TDbContext Context => (TDbContext)_dbContext;
}
