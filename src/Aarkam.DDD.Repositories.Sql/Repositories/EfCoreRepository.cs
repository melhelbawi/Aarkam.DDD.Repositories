using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.DDD.Repositories.Sql.Repositories;

/// <summary>
/// Comprehensive Entity Framework Core implementation of the relational and store-agnostic repository contract <see cref="IRepository{TEntity}"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class EfCoreRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IEntity
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<TEntity> _dbSet;

    public EfCoreRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<TEntity>();
    }

    public DbContext DbContext => _dbContext;
    public DbSet<TEntity> DbSet => _dbSet;

    // =========================================================================
    // 1. Storage-Agnostic & Basic Read Operations
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));
        return await ApplySpecification(specification).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));
        var query = ApplySpecification(specification);
        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.LongCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));
        var query = ApplySpecification(specification);
        return await query.LongCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return await _dbSet.Where(predicate).LongCountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<TEntity>> GetPagedAsync(
        ISpecification<TEntity> specification,
        PagingOptions? pagingOptions = null,
        CancellationToken cancellationToken = default)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));
        pagingOptions ??= new PagingOptions();

        var query = ApplySpecification(specification);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pagingOptions.PageNumber - 1) * pagingOptions.PageSize)
            .Take(pagingOptions.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TEntity>(totalCount, pagingOptions.PageNumber, pagingOptions.PageSize, items);
    }

    // =========================================================================
    // 2. Storage-Agnostic & Basic Write Operations
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        await _dbSet.AddRangeAsync(entities, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        var entry = _dbSet.Update(entity);
        return Task.FromResult(entry.Entity);
    }

    /// <inheritdoc />
    public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        _dbSet.UpdateRange(entities);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        _dbSet.Remove(entity);
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public virtual async Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        var count = await DeleteRangeAsync(predicate, cancellationToken);
        return count > 0;
    }

    /// <inheritdoc />
    public virtual async Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        if (entities.Count == 0)
            return 0;

        _dbSet.RemoveRange(entities);
        return entities.Count;
    }

    /// <inheritdoc />
    public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));
        _dbSet.RemoveRange(entities);
        return Task.CompletedTask;
    }

    // =========================================================================
    // 3. Relational & LINQ Querying Extensions
    // =========================================================================

    /// <inheritdoc />
    public virtual IQueryable<TEntity> AsQueryable()
    {
        return _dbSet.AsQueryable();
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> AsNoTracking()
    {
        return _dbSet.AsNoTracking();
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return _dbSet.Where(predicate);
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        return condition ? _dbSet.Where(predicate) : _dbSet;
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> WithIncludes(params Expression<Func<TEntity, object>>[] includes)
    {
        IQueryable<TEntity> query = _dbSet;
        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }
        return query;
    }

    /// <inheritdoc />
    public virtual IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification)
    {
        if (specification == null) throw new ArgumentNullException(nameof(specification));

        IQueryable<TEntity> query = _dbSet;

        // Apply includes
        if (specification.Includes != null)
        {
            foreach (var include in specification.Includes)
            {
                query = query.Include(include);
            }
        }

        // Apply where criteria
        var criteria = specification.ToExpression();
        if (criteria != null)
        {
            query = query.Where(criteria);
        }

        // Apply OrderBy / OrderByDescending
        if (specification.OrderBy != null)
        {
            query = query.OrderBy(specification.OrderBy);
        }
        else if (specification.OrderByDescending != null)
        {
            query = query.OrderByDescending(specification.OrderByDescending);
        }

        // Apply pagination
        if (specification.Skip.HasValue && specification.Skip.Value > 0)
        {
            query = query.Skip(specification.Skip.Value);
        }

        if (specification.Take.HasValue && specification.Take.Value > 0)
        {
            query = query.Take(specification.Take.Value);
        }

        return query;
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> ToListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        var query = predicate != null ? _dbSet.Where(predicate) : _dbSet;
        return await query.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    [Obsolete("In modern asynchronous and cloud microservices architectures, prefer ToListAsync to prevent thread-pool starvation.")]
    public virtual List<TEntity> ToList(Expression<Func<TEntity, bool>>? predicate = null)
    {
        var query = predicate != null ? _dbSet.Where(predicate) : _dbSet;
        return query.ToList();
    }

    /// <inheritdoc />
    public virtual async Task<PagedResult<TEntity>> ToPagedResultAsync(PagingOptions? pagingOptions = null)
    {
        pagingOptions ??= new PagingOptions();
        var totalCount = await _dbSet.CountAsync();
        var items = await _dbSet
            .Skip((pagingOptions.PageNumber - 1) * pagingOptions.PageSize)
            .Take(pagingOptions.PageSize)
            .ToListAsync();

        return new PagedResult<TEntity>(totalCount, pagingOptions.PageNumber, pagingOptions.PageSize, items);
    }
}

/// <summary>
/// Comprehensive Entity Framework Core implementation of relational and store-agnostic repository contracts
/// unifying <see cref="IRepository{TEntity, TKey}"/>, <see cref="IBasicRepository{TEntity, TKey}"/>, and <see cref="IReadOnlyRepository{TEntity, TKey}"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TKey">The identity type of the entity.</typeparam>
public class EfCoreRepository<TEntity, TKey> : EfCoreRepository<TEntity>, IRepository<TEntity, TKey>, IBasicRepository<TEntity, TKey>, IReadOnlyRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    public EfCoreRepository(DbContext dbContext) : base(dbContext)
    {
    }

    // =========================================================================
    // 1. Storage-Agnostic & Basic Read Operations with Key
    // =========================================================================

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object?[] { id }, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public virtual Task<TEntity?> FindByIdAsync(TKey id, CancellationToken cancellationToken = default)
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
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreRepository{TEntity}"/> binding directly to a specific <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TDbContext">The application DbContext type.</typeparam>
public class EfCoreContextRepository<TEntity, TDbContext> : EfCoreRepository<TEntity>
    where TEntity : class, IEntity
    where TDbContext : DbContext
{
    public EfCoreContextRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    /// <summary>
    /// Gets the strongly typed application <typeparamref name="TDbContext"/> instance.
    /// </summary>
    public TDbContext Context => (TDbContext)_dbContext;
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreRepository{TEntity, TKey}"/> binding to a specific <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TKey">The identity type of the entity.</typeparam>
/// <typeparam name="TDbContext">The application DbContext type.</typeparam>
public class EfCoreRepository<TEntity, TKey, TDbContext> : EfCoreRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TDbContext : DbContext
{
    public EfCoreRepository(TDbContext dbContext) : base(dbContext)
    {
    }

    /// <summary>
    /// Gets the strongly typed application <typeparamref name="TDbContext"/> instance.
    /// </summary>
    public TDbContext Context => (TDbContext)_dbContext;
}
