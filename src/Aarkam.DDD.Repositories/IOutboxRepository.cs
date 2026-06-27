using Aarkam.DDD.Domain;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Repository interface for managing outbox messages in the transactional outbox pattern.
/// </summary>
/// <remarks>
/// <para>
/// This repository provides operations for persisting and retrieving outbox messages.
/// It is typically used by:
/// <list type="bullet">
///   <item><description>Application services to add new outbox messages alongside domain changes</description></item>
///   <item><description>Background processors to fetch and publish pending messages</description></item>
///   <item><description>Cleanup jobs to remove old processed messages</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage pattern:</strong>
/// <code>
/// // In your application service
/// await _outboxRepository.AddAsync(outboxMessage, cancellationToken);
/// await _unitOfWork.SaveChangesAsync(cancellationToken);
/// 
/// // In your background processor
/// var pending = await _outboxRepository.GetPendingAsync(batchSize, cancellationToken);
/// foreach (var message in pending)
/// {
///     await PublishToMessageBroker(message);
///     message.MarkAsProcessed();
/// }
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="IOutboxMessage"/>
public interface IOutboxRepository
{
    /// <summary>
    /// Adds a new outbox message to the repository.
    /// </summary>
    /// <param name="message">The outbox message to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(IOutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a batch of pending (unprocessed) messages ordered by creation time.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A collection of pending outbox messages.</returns>
    Task<IReadOnlyList<IOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully processed.
    /// </summary>
    /// <param name="messageId">The ID of the message to mark as processed.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed processing attempt for a message.
    /// </summary>
    /// <param name="messageId">The ID of the message that failed.</param>
    /// <param name="error">The error message from the failure.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordFailureAsync(Guid messageId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes processed messages older than the specified retention period.
    /// </summary>
    /// <param name="olderThan">The cutoff date; messages processed before this will be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of messages deleted.</returns>
    Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
