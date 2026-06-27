using Aarkam.DDD.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Repository interface for managing inbox messages for idempotent event processing.
/// </summary>
/// <remarks>
/// <para>
/// This repository is used to track which events have been processed by which consumers,
/// enabling exactly-once processing semantics in distributed systems.
/// </para>
/// <para>
/// <strong>Usage pattern:</strong>
/// <code>
/// // In your event handler
/// if (await _inboxRepository.HasBeenProcessedAsync(eventId, consumerName))
/// {
///     _logger.LogInformation("Event already processed, skipping");
///     return;
/// }
/// 
/// // Process the event...
/// await ProcessEventAsync(event);
/// 
/// // Record that we processed it
/// await _inboxRepository.RecordAsync(eventId, eventType, consumerName);
/// await _unitOfWork.SaveChangesAsync();
/// </code>
/// </para>
/// </remarks>
/// <seealso cref="IInboxMessage"/>
public interface IInboxRepository
{
    /// <summary>
    /// Checks if an event has already been processed by the specified consumer.
    /// </summary>
    /// <param name="eventId">The unique identifier of the event.</param>
    /// <param name="consumerName">The name of the consumer/handler.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the event has been processed; otherwise false.</returns>
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an event has been processed by a consumer.
    /// </summary>
    /// <param name="message">The inbox message to record.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordAsync(IInboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes inbox records older than the specified retention period.
    /// </summary>
    /// <param name="olderThan">The cutoff date; records older than this will be deleted.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of records deleted.</returns>
    Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
