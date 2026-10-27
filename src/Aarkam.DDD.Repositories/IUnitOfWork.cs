using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Lean transactional commit boundary coordinating physical persistence across operations.
/// </summary>
/// <remarks>
/// <para>
/// <b>Architectural Design:</b>
/// In modern Domain-Driven Design (DDD) and cloud microservices, <see cref="IUnitOfWork"/> is strictly 
/// an <b>execution and transaction boundary</b>. It does <b>NOT</b> act as a repository factory or Service Locator 
/// (avoiding hidden dependencies and constructor anti-patterns).
/// </para>
/// <para>
/// Repositories should always be injected explicitly via constructor Dependency Injection (<c>IServiceCollection</c>).
/// For pure DDD systems where operations are scoped strictly to a single aggregate, repositories 
/// (like <see cref="IAggregateRepository{TAggregate, TKey}"/>) can persist and commit directly via <c>SaveAsync</c>, 
/// making an explicit <see cref="IUnitOfWork"/> optional in application command handlers.
/// </para>
/// <para>
/// Supported persistence paradigms:
/// <list type="bullet">
///   <item><description>Relational database transactions (e.g. Entity Framework Core DbContext)</description></item>
///   <item><description>Document store session transactions (e.g. MongoDB ClientSession, Azure Cosmos DB transactional batches)</description></item>
///   <item><description>Transactional Outbox coordinator (atomically committing aggregate mutations alongside outbox messages)</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether a database transaction is currently in progress.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Saves all tracked changes to the underlying data store asynchronously.
    /// This represents the primary physical commit point. Domain events should be dispatched after this call succeeds.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries or affected rows committed to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database or session transaction asynchronously.
    /// Use when explicit transaction isolation, cross-operation rollback, or distributed coordination is required.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current active transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current active transaction asynchronously, discarding all uncommitted mutations.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Strongly-typed generic variant of <see cref="IUnitOfWork"/> bound to a specific application persistence or database context.
/// Enables disambiguation when multiple database contexts or bounded contexts exist within the same application service container.
/// </summary>
/// <typeparam name="TContext">The persistence or database context type.</typeparam>
public interface IUnitOfWork<TContext> : IUnitOfWork
{
}
