using Aarkam.DDD.Domain;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Repository interface for persisting and retrieving saga (process manager) state.
/// </summary>
/// <remarks>
/// <para>
/// This repository manages the persistence of saga instances, enabling long-running
/// business processes to survive application restarts and be distributed across
/// multiple nodes.
/// </para>
/// <para>
/// <strong>Key responsibilities:</strong>
/// <list type="bullet">
///   <item><description>Persist saga state after each event is handled</description></item>
///   <item><description>Load saga state when resuming processing</description></item>
///   <item><description>Handle optimistic concurrency to prevent race conditions</description></item>
///   <item><description>Query for sagas needing timeout processing</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage pattern:</strong>
/// <code>
/// // Loading a saga
/// var saga = await _sagaRepository.LoadAsync&lt;OrderFulfillmentSaga&gt;(orderId);
/// if (saga == null)
/// {
///     saga = new OrderFulfillmentSaga(orderId);
///     saga.Start();
/// }
/// 
/// // Handle event and save
/// saga.Handle(event);
/// await _sagaRepository.SaveAsync(saga);
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="ISaga{TSagaData}"/>
/// <seealso cref="SagaBase{TSagaData}"/>
public interface ISagaRepository
{
    /// <summary>
    /// Loads a saga instance by its identifier.
    /// </summary>
    /// <typeparam name="TSaga">The type of the saga.</typeparam>
    /// <typeparam name="TSagaData">The type of the saga's state data.</typeparam>
    /// <param name="sagaId">The unique identifier of the saga.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The saga instance, or null if not found.</returns>
    Task<TSaga> LoadAsync<TSaga, TSagaData>(Guid sagaId, CancellationToken cancellationToken = default)
        where TSaga : ISaga<TSagaData>
        where TSagaData : class, new();

    /// <summary>
    /// Saves a saga instance, creating or updating as appropriate.
    /// </summary>
    /// <typeparam name="TSaga">The type of the saga.</typeparam>
    /// <typeparam name="TSagaData">The type of the saga's state data.</typeparam>
    /// <param name="saga">The saga instance to save.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync<TSaga, TSagaData>(TSaga saga, CancellationToken cancellationToken = default)
        where TSaga : ISaga<TSagaData>
        where TSagaData : class, new();

    /// <summary>
    /// Finds sagas that have been running longer than the specified timeout.
    /// </summary>
    /// <param name="timeout">The duration after which a saga is considered timed out.</param>
    /// <param name="status">The status to filter by (typically Running).</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of saga identifiers that have timed out.</returns>
    Task<IReadOnlyList<Guid>> FindTimedOutSagasAsync(
        TimeSpan timeout,
        SagaStatus status = SagaStatus.Running,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes completed sagas older than the specified retention period.
    /// </summary>
    /// <param name="olderThan">The cutoff date; sagas completed before this will be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of sagas deleted.</returns>
    Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
