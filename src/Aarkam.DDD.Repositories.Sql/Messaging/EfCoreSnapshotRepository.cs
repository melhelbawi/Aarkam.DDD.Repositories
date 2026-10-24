using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Aarkam.DDD.Repositories.Sql.Messaging;

/// <summary>
/// Entity Framework Core implementation of <see cref="ISnapshotRepository"/> for aggregate Event Sourcing snapshots.
/// </summary>
public class EfCoreSnapshotRepository : ISnapshotRepository
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<SnapshotRecord> _dbSet;

    public EfCoreSnapshotRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<SnapshotRecord>();
    }

    /// <inheritdoc />
    public virtual async Task<ISnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var idStr = aggregateId.ToString();
        return await _dbSet
            .Where(s => s.AggregateId == idStr)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<ISnapshot?> GetAtVersionAsync(Guid aggregateId, long maxVersion, CancellationToken cancellationToken = default)
    {
        var idStr = aggregateId.ToString();
        return await _dbSet
            .Where(s => s.AggregateId == idStr && s.Version <= maxVersion)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync(ISnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        if (snapshot is SnapshotRecord record)
        {
            await _dbSet.AddAsync(record, cancellationToken);
        }
        else
        {
            var newRecord = new SnapshotRecord
            {
                AggregateId = snapshot.AggregateId,
                AggregateType = snapshot.AggregateType,
                Version = snapshot.Version,
                State = snapshot.State,
                CreatedAt = snapshot.CreatedAt == default ? DateTimeOffset.UtcNow : snapshot.CreatedAt,
                TenantId = snapshot.TenantId
            };
            await _dbSet.AddAsync(newRecord, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<int> PruneAsync(Guid aggregateId, int keepCount = 3, CancellationToken cancellationToken = default)
    {
        if (keepCount < 1) keepCount = 1;

        var idStr = aggregateId.ToString();
        var allVersions = await _dbSet
            .Where(s => s.AggregateId == idStr)
            .OrderByDescending(s => s.Version)
            .ToListAsync(cancellationToken);

        if (allVersions.Count <= keepCount)
            return 0;

        var toDelete = allVersions.Skip(keepCount).ToList();
        _dbSet.RemoveRange(toDelete);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return toDelete.Count;
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreSnapshotRepository"/> bound to a specific <typeparamref name="TAggregate"/> root.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
public class EfCoreSnapshotRepository<TAggregate> : EfCoreSnapshotRepository, ISnapshotRepository<TAggregate>
    where TAggregate : class, IAggregateRoot
{
    private readonly string _aggregateType = typeof(TAggregate).Name;

    public EfCoreSnapshotRepository(DbContext dbContext) : base(dbContext)
    {
    }

    /// <inheritdoc />
    public new async Task<ISnapshot?> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        var idStr = aggregateId.ToString();
        return await _dbSet
            .Where(s => s.AggregateId == idStr && s.AggregateType == _aggregateType)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public new async Task<ISnapshot?> GetAtVersionAsync(Guid aggregateId, long maxVersion, CancellationToken cancellationToken = default)
    {
        var idStr = aggregateId.ToString();
        return await _dbSet
            .Where(s => s.AggregateId == idStr && s.AggregateType == _aggregateType && s.Version <= maxVersion)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
