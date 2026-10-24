using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using System.Linq.Expressions;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Defines a store-agnostic, read-only repository contract for querying entities in Domain-Driven Design (DDD) and CQRS architectures.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts read operations from underlying storage mechanisms. It provides first-class support for:
/// <list type="bullet">
///   <item><description>Relational databases (SQL Server, PostgreSQL, MySQL via EF Core or Dapper)</description></item>
///   <item><description>Document and NoSQL engines (Azure Cosmos DB, MongoDB, DynamoDB, RavenDB)</description></item>
///   <item><description>Distributed memory caches and In-Memory collections for testing</description></item>
/// </list>
/// </para>
/// <para>
/// Queries are expressed through strongly-typed keys or declarative specifications (<see cref="ISpecification{TEntity}"/>),
/// ensuring database-specific query syntax and translation leaks are prevented from invading application or domain layers.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The type of the domain entity or aggregate root.</typeparam>
/// <typeparam name="TKey">The identity type of the entity (e.g. <see cref="Guid"/>, <see cref="int"/>, <see cref="string"/>).</typeparam>
public interface IReadOnlyRepository<TEntity, TKey> where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identity key of the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity instance if found; otherwise, <see langword="null"/>.</returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if an entity with the specified identifier exists in the store.
    /// </summary>
    /// <param name="id">The unique identity key to verify.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the entity exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously finds the first entity matching the specified domain specification criteria.
    /// </summary>
    /// <param name="specification">The specification defining query criteria and eager-loading rules.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The matching entity instance, or <see langword="null"/> if none match.</returns>
    Task<TEntity?> FindAsync(ISpecification<TEntity> specification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously finds the first entity matching the specified predicate expression.
    /// </summary>
    /// <param name="predicate">An expression defining the search condition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The matching entity instance, or <see langword="null"/> if none match.</returns>
    Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a read-only list of entities satisfying the provided specification.
    /// </summary>
    /// <param name="specification">An optional specification. If <see langword="null"/>, all entities are returned.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only collection of matching entities.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(ISpecification<TEntity>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a read-only list of entities satisfying the provided predicate expression.
    /// </summary>
    /// <param name="predicate">An optional predicate expression. If <see langword="null"/>, all entities are returned.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only collection of matching entities.</returns>
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously counts the number of entities satisfying the provided specification.
    /// </summary>
    /// <param name="specification">An optional specification. If <see langword="null"/>, counts all entities.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The total number of matching entities in the underlying store.</returns>
    Task<long> CountAsync(ISpecification<TEntity>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously counts the number of entities satisfying the provided predicate expression.
    /// </summary>
    /// <param name="predicate">An optional predicate expression. If <see langword="null"/>, counts all entities.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The total number of matching entities in the underlying store.</returns>
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated subset of entities satisfying the provided specification.
    /// </summary>
    /// <param name="specification">The specification defining filtering and sorting criteria.</param>
    /// <param name="pagingOptions">Paging configuration (page number and page size). If <see langword="null"/>, defaults are applied.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="PagedResult{TEntity}"/> containing the items for the current page and total item count.</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(ISpecification<TEntity> specification, PagingOptions? pagingOptions = null, CancellationToken cancellationToken = default);
}
