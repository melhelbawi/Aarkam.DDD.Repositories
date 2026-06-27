using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Generic repository interface providing common CRUD and query operations for entities.
/// </summary>
/// <typeparam name="TEntity">The type of entity this repository manages. Must be a reference type (class).</typeparam>
/// <remarks>
/// This interface follows the Repository pattern and is designed to work with Entity Framework Core (or similar ORMs).
/// It provides both synchronous-style async methods and flexible querying capabilities.
/// All operations support cancellation via <see cref="CancellationToken"/>.
/// </remarks>
public interface IRepository<TEntity> where TEntity : class
{
    /// <summary>
    /// Adds a new entity to the underlying data store asynchronously.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The added entity (with generated ID if applicable).</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple entities to the underlying data store asynchronously.
    /// </summary>
    /// <param name="entities">Collection of entities to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the underlying data store asynchronously.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The updated entity.</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates multiple entities in the underlying data store asynchronously.
    /// </summary>
    /// <param name="entities">Collection of entities to update.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a single entity from the underlying data store asynchronously.
    /// </summary>
    /// <param name="entity">The entity to delete (must be tracked or have its key set).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if the deletion succeeded, <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes entities matching the specified predicate asynchronously.
    /// </summary>
    /// <param name="predicate">Expression defining which entities to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if at least one entity was deleted, <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all entities matching the specified predicate asynchronously.
    /// </summary>
    /// <param name="predicate">Expression defining which entities to delete.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of entities deleted.</returns>
    Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the underlying <see cref="IQueryable{TEntity}"/> for this entity type.
    /// Allows full LINQ querying before materialization.
    /// </summary>
    /// <returns>An <see cref="IQueryable{TEntity}"/> that can be further filtered/projected.</returns>
    IQueryable<TEntity> AsQueryable();

    /// <summary>
    /// Applies a filter to the query.
    /// </summary>
    /// <param name="predicate">The filter expression.</param>
    /// <returns>Filtered <see cref="IQueryable{TEntity}"/>.</returns>
    IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// Conditionally applies a filter to the query (useful for optional filtering).
    /// </summary>
    /// <param name="condition">If <c>true</c>, the predicate is applied; otherwise skipped.</param>
    /// <param name="predicate">The filter expression to apply when condition is true.</param>
    /// <returns>The query with or without the filter.</returns>
    IQueryable<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate);

    /// <summary>
    /// Includes related navigation properties in the query (eager loading).
    /// </summary>
    /// <param name="includes">Array of navigation property expressions to include.</param>
    /// <returns>The query with the specified includes applied.</returns>
    IQueryable<TEntity> WithIncludes(params Expression<Func<TEntity, object>>[] includes);
    /// <summary>
    /// Applies the given specification to filter, include, and shape the query.
    /// </summary>
    /// <param name="specification">The specification containing criteria, includes, etc.</param>
    /// <returns>Query configured by the specification.</returns>
    IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification);
    /// <summary>
    /// Asynchronously creates a list of entities from the repository that match the specified predicate.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity contained in the repository.</typeparam>
    /// <param name="repository">The repository from which to retrieve entities.</param>
    /// <param name="predicate">An optional expression to filter the entities. If null, all entities are included.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of entities that match the
    /// predicate, or all entities if no predicate is specified.</returns>
    Task<List<TEntity>> ToListAsync(Expression<Func<TEntity, bool>> predicate = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Retrieves a list of entities that satisfy the specified predicate.
    /// </summary>
    /// <param name="predicate">An expression used to filter the entities to be returned. If null, all entities are included.</param>
    /// <returns>A list of entities of type TEntity that match the predicate. Returns all entities if the predicate is null. The
    /// list will be empty if no entities match.</returns>
    List<TEntity> ToList(Expression<Func<TEntity, bool>> predicate = null);

    /// <summary>
    /// Asynchronously creates a paged result from the specified query, applying the given paging options.
    /// </summary>
    /// <remarks>The method executes the query twice: once to retrieve the paged items and once to count the
    /// total number of items. The returned <see cref="PagedResult{TEntity}"/> includes the total count, current page
    /// number, page size, and the items for the current page.</remarks>
    /// <typeparam name="TEntity">The type of the entities in the query.</typeparam>
    /// <param name="query">The source query to paginate. Must not be null.</param>
    /// <param name="pagingOptions">The paging options to apply to the query. If null, default paging options are used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="PagedResult{TEntity}"/>
    /// with the items for the requested page and the total item count.</returns>
    Task<PagedResult<TEntity>> ToPagedResultAsync(PagingOptions pagingOptions = null);
}