using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Unit of Work interface that coordinates changes across one or more repositories 
/// within a single transaction boundary.
/// </summary>
/// <remarks>
/// This interface acts as a thin facade over EF Core's DbContext, providing:
/// - Access to repositories (generic or specific)
/// - Explicit SaveChanges / Commit point
/// - Optional transaction support
/// - Automatic disposal (via IAsyncDisposable)
/// 
/// In DDD, the Unit of Work is typically used in application services / command handlers 
/// to ensure atomicity and to dispatch domain events after persistence.
/// </remarks>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a generic repository for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type (must inherit from class).</typeparam>
    /// <returns>An instance of <see cref="IRepository{TEntity}"/>.</returns>
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Saves all tracked changes to the database asynchronously.
    /// This is the main commit point — domain events should be dispatched after this call.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction asynchronously.
    /// Use when you need stricter isolation or rollback capability.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction asynchronously (if one is active).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
