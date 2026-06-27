using Aarkam.DDD.Domain;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Repository interface for managing aggregate snapshots in event-sourced systems.
/// </summary>
/// <remarks>
/// <para>
/// This repository handles the persistence and retrieval of aggregate snapshots,
/// which are used to optimize event sourcing by reducing the number of events
/// that need to be replayed when loading an aggregate.
/// </para>
/// <para>
/// <strong>Snapshot loading strategy:</strong>
/// <code>
/// // In your event store implementation
/// var snapshot = await _snapshotRepository.GetLatestAsync&lt;Order&gt;(orderId);
/// if (snapshot != null)
/// {
///     aggregate = DeserializeFromSnapshot(snapshot);
///     var newEvents = await GetEventsAfterVersion(orderId, snapshot.Version);
///     aggregate.ReplayEvents(newEvents);
/// }
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="ISnapshot"/>
public interface ISnapshotRepository
{
    /// <summary>
    /// Gets the latest snapshot for an aggregate.
    /// </summary>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The latest snapshot, or null if no snapshot exists.</returns>
    Task<ISnapshot> GetLatestAsync(Guid aggregateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot at or before the specified version.
    /// </summary>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="maxVersion">The maximum version to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The snapshot at or before the specified version, or null if none exists.</returns>
    Task<ISnapshot> GetAtVersionAsync(Guid aggregateId, long maxVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new snapshot for an aggregate.
    /// </summary>
    /// <param name="snapshot">The snapshot to save.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveAsync(ISnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old snapshots, keeping only the specified number of most recent snapshots.
    /// </summary>
    /// <param name="aggregateId">The unique identifier of the aggregate.</param>
    /// <param name="keepCount">The number of recent snapshots to retain.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of snapshots deleted.</returns>
    Task<int> PruneAsync(Guid aggregateId, int keepCount = 3, CancellationToken cancellationToken = default);
}
