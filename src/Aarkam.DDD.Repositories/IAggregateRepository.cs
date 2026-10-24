using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Defines the strict Domain-Driven Design (DDD) contract for an Aggregate Root repository.
/// </summary>
/// <remarks>
/// <para>
/// In pure DDD:
/// <list type="bullet">
///   <item><description><b>Boundary Integrity:</b> Repositories must exist strictly for Aggregate Roots.</description></item>
///   <item><description><b>Invariant Protection:</b> Internal child entities and value objects must never be manipulated via separate repositories.</description></item>
///   <item><description><b>Atomic Units:</b> All business state changes inside an aggregate boundary commit atomically.</description></item>
///   <item><description><b>Self-Contained Commit:</b> By using <see cref="SaveAsync"/>, the aggregate repository encapsulates physical persistence and outbox event dispatching, making external <see cref="IUnitOfWork"/> dependencies optional in application handlers.</description></item>
/// </list>
/// </para>
/// <para>
/// This interface guarantees compile-time enforcement of the <see cref="IAggregateRoot{TKey}"/> constraint.
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root type managed by this repository.</typeparam>
/// <typeparam name="TKey">The identity type of the aggregate root.</typeparam>
public interface IAggregateRepository<TAggregate, TKey> : IBasicRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Asynchronously persists the aggregate root, extracts and persists its uncommitted domain events (e.g. to the Transactional Outbox),
    /// and commits the transaction boundary atomically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method embodies the core DDD principle that an Aggregate Root is itself the transactional consistency boundary.
    /// In pure DDD and microservices, command handlers call <see cref="SaveAsync"/> directly on the aggregate repository,
    /// eliminating the need to inject or coordinate an external <see cref="IUnitOfWork"/>.
    /// </para>
    /// <para>
    /// Implementation responsibilities:
    /// <list type="bullet">
    ///   <item><description>Persist state mutations of the aggregate root and its child entity graph.</description></item>
    ///   <item><description>Extract uncommitted domain events (via <see cref="IAggregateRoot"/>).</description></item>
    ///   <item><description>Persist events to the Transactional Outbox or dispatch to event handlers within the same transaction.</description></item>
    ///   <item><description>Clear the aggregate's uncommitted event stream after successful commit.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="aggregate">The aggregate root instance to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous save and commit operation.</returns>
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
}
