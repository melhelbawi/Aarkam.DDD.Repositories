using Aarkam.DDD.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Universal data store-agnostic repository interface defining asynchronous mutation and lifecycle operations for domain entities.
/// </summary>
/// <remarks>
/// <para>
/// Unlike relational-specific repository interfaces, <see cref="IBasicRepository{TEntity}"/> is completely decoupled 
/// from Object-Relational Mappers (ORMs) and LINQ providers. It can be implemented across:
/// <list type="bullet">
///   <item><description>NoSQL Document databases (MongoDB, Azure Cosmos DB, AWS DynamoDB, Couchbase, RavenDB)</description></item>
///   <item><description>Relational databases (PostgreSQL, SQL Server, MySQL via EF Core or Dapper)</description></item>
///   <item><description>Key-Value stores (Redis)</description></item>
///   <item><description>Event-driven state caches and distributed services</description></item>
/// </list>
/// </para>
/// <para>
/// In strict Domain-Driven Design (DDD), write operations should be applied to Aggregate Roots to safeguard boundary invariants.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The type of the domain entity or aggregate root.</typeparam>
public interface IBasicRepository<TEntity> : IReadOnlyRepository<TEntity>
    where TEntity : class, IEntity
{
    /// <summary>
    /// Asynchronously persists a new entity into the underlying data store.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The persisted entity instance (including any server-generated identity or timestamp values).</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously persists a batch of entities into the underlying data store.
    /// </summary>
    /// <param name="entities">The collection of entities to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates the state of an existing entity in the underlying data store.
    /// </summary>
    /// <param name="entity">The entity containing updated property values.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The updated entity instance.</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously updates a batch of existing entities in the underlying data store.
    /// </summary>
    /// <param name="entities">The collection of entities with modified states.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an entity from the underlying data store.
    /// </summary>
    /// <param name="entity">The entity instance to remove.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the deletion was executed successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes multiple entities matching the given predicate expression.
    /// </summary>
    /// <param name="predicate">An expression defining the criteria for entities to remove.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The total count of entities removed from the data store.</returns>
    Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes a batch of entity instances from the underlying store.
    /// </summary>
    /// <param name="entities">The collection of entities to remove.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
}

/// <summary>
/// Universal data store-agnostic repository interface defining asynchronous mutation and lifecycle operations for domain entities with a strongly typed identity key.
/// Extends <see cref="IBasicRepository{TEntity}"/> and <see cref="IReadOnlyRepository{TEntity, TKey}"/> with identity-based deletion (<see cref="DeleteByIdAsync"/>).
/// </summary>
/// <typeparam name="TEntity">The type of the domain entity or aggregate root.</typeparam>
/// <typeparam name="TKey">The identity type of the entity.</typeparam>
public interface IBasicRepository<TEntity, TKey> : IBasicRepository<TEntity>, IReadOnlyRepository<TEntity, TKey> 
    where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// Asynchronously deletes an entity by its unique identity key without requiring prior entity loading.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if an entity with the specified ID was found and deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default);
}
