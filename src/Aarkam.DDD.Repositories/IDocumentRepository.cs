using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;

namespace Aarkam.Domain.Repositories;

/// <summary>
/// Specialized NoSQL document database repository contract tailored for partitioned distributed databases 
/// (e.g. Azure Cosmos DB, MongoDB, Amazon DynamoDB, Couchbase, RavenDB).
/// </summary>
/// <remarks>
/// <para>
/// In distributed NoSQL document architectures:
/// <list type="bullet">
///   <item><description><b>Partitioning:</b> Efficient operations ($O(1)$ point-reads, minimum Request Units/RCUs) require a Partition Key.</description></item>
///   <item><description><b>Upsert Semantics:</b> Inserting a document if new, or replacing it if it exists, is a native first-class capability.</description></item>
///   <item><description><b>Concurrency:</b> Optimistic concurrency is typically enforced via HTTP ETags (<c>_etag</c>) or document version sequences.</description></item>
///   <item><description><b>Self-Contained Roots:</b> Documents naturally represent whole Aggregate Roots with child entities embedded as nested JSON structures.</description></item>
/// </list>
/// </para>
/// </remarks>
/// <typeparam name="TDocument">The document type (must implement <see cref="IEntity{TKey}"/>).</typeparam>
/// <typeparam name="TKey">The document unique identity key type.</typeparam>
/// <typeparam name="TPartitionKey">The partition key type used for physical or logical data distribution (e.g., <see cref="string"/>, <see cref="Guid"/>, tenant ID, category ID).</typeparam>
public interface IDocumentRepository<TDocument, TKey, TPartitionKey> : IBasicRepository<TDocument, TKey>
    where TDocument : class, IEntity<TKey>
{
    /// <summary>
    /// Asynchronously retrieves a document by its ID and partition key with point-read efficiency ($O(1)$).
    /// </summary>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="partitionKey">The partition key defining the document's logical partition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The document if found; otherwise, <see langword="null"/>.</returns>
    Task<TDocument?> GetByIdAsync(TKey id, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously checks if a document exists within the specified partition.
    /// </summary>
    /// <param name="id">The unique identifier of the document.</param>
    /// <param name="partitionKey">The partition key of the document.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the document exists in the partition; otherwise, <see langword="false"/>.</returns>
    Task<bool> ExistsAsync(TKey id, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously inserts a document if it does not exist, or completely replaces it if it already exists (Upsert).
    /// </summary>
    /// <param name="document">The document to insert or update.</param>
    /// <param name="partitionKey">The partition key of the document.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The resulting persisted document instance.</returns>
    Task<TDocument> UpsertAsync(TDocument document, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously replaces an existing document under optimistic concurrency check using an ETag or concurrency token.
    /// </summary>
    /// <param name="document">The modified document to replace.</param>
    /// <param name="eTag">The expected concurrency token / ETag value. If the server token does not match, operation fails with a concurrency violation.</param>
    /// <param name="partitionKey">The partition key of the document.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The replaced document instance.</returns>
    Task<TDocument> ReplaceAsync(TDocument document, string eTag, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes a document by its ID and partition key without requiring prior entity loading.
    /// </summary>
    /// <param name="id">The unique identifier of the document to delete.</param>
    /// <param name="partitionKey">The partition key of the document.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the document was found and deleted; otherwise, <see langword="false"/>.</returns>
    Task<bool> DeleteByIdAsync(TKey id, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all documents residing within a specific partition, optionally filtered by a specification.
    /// </summary>
    /// <param name="partitionKey">The partition key to scope the query to.</param>
    /// <param name="specification">An optional specification to filter results within the partition.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A read-only collection of documents in the target partition.</returns>
    Task<IReadOnlyList<TDocument>> ListByPartitionAsync(TPartitionKey partitionKey, ISpecification<TDocument>? specification = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paged subset of documents within a single partition.
    /// </summary>
    /// <param name="partitionKey">The partition key to scope the paged query to.</param>
    /// <param name="specification">The specification defining query criteria.</param>
    /// <param name="pagingOptions">Paging options (page number and page size).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A paged result of documents from the specified partition.</returns>
    Task<PagedResult<TDocument>> GetPagedByPartitionAsync(TPartitionKey partitionKey, ISpecification<TDocument> specification, PagingOptions? pagingOptions = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs high-throughput bulk insertion of documents within a partition or batch.
    /// </summary>
    /// <param name="documents">The batch of documents to insert.</param>
    /// <param name="partitionKey">The partition key for the batch.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the bulk operation.</returns>
    Task BulkInsertAsync(IEnumerable<TDocument> documents, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously performs high-throughput bulk upsert of documents within a partition or batch.
    /// </summary>
    /// <param name="documents">The batch of documents to upsert.</param>
    /// <param name="partitionKey">The partition key for the batch.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the bulk operation.</returns>
    Task BulkUpsertAsync(IEnumerable<TDocument> documents, TPartitionKey partitionKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Specialized NoSQL document database repository contract for non-partitioned or string-partitioned collections (e.g. MongoDB, RavenDB).
/// </summary>
/// <typeparam name="TDocument">The document type (must implement <see cref="IEntity{TKey}"/>).</typeparam>
/// <typeparam name="TKey">The document unique identity key type.</typeparam>
public interface IDocumentRepository<TDocument, TKey> : IDocumentRepository<TDocument, TKey, string>
    where TDocument : class, IEntity<TKey>
{
}
