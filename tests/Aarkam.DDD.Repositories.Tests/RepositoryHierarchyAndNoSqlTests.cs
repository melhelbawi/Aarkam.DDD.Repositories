using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using Aarkam.DDD.Repositories.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Moq;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

// =============================================================================
// In-Memory Test Doubles for Repository Abstractions
// =============================================================================

public class InMemoryAggregateRepository : IAggregateRepository<TestCustomerAggregate, Guid>
{
    private readonly ConcurrentDictionary<Guid, TestCustomerAggregate> _store = new();
    public List<string> PublishedOutboxEvents { get; } = [];

    public Task<TestCustomerAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var agg);
        return Task.FromResult(agg);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey(id));
    }

    public Task<TestCustomerAggregate> AddAsync(TestCustomerAggregate entity, CancellationToken cancellationToken = default)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task AddRangeAsync(IEnumerable<TestCustomerAggregate> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities) _store[e.Id] = e;
        return Task.CompletedTask;
    }

    public Task<TestCustomerAggregate> UpdateAsync(TestCustomerAggregate entity, CancellationToken cancellationToken = default)
    {
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task UpdateRangeAsync(IEnumerable<TestCustomerAggregate> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities) _store[e.Id] = e;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(TestCustomerAggregate entity, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(entity.Id, out _));
    }

    public Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public Task<int> DeleteRangeAsync(Expression<Func<TestCustomerAggregate, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var matches = _store.Values.AsQueryable().Where(predicate).ToList();
        int count = 0;
        foreach (var m in matches)
        {
            if (_store.TryRemove(m.Id, out _)) count++;
        }
        return Task.FromResult(count);
    }

    public Task DeleteRangeAsync(IEnumerable<TestCustomerAggregate> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities) _store.TryRemove(e.Id, out _);
        return Task.CompletedTask;
    }

    public Task<TestCustomerAggregate?> FindAsync(ISpecification<TestCustomerAggregate> specification, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(specification.ToExpression()));
    }

    public Task<TestCustomerAggregate?> FindAsync(Expression<Func<TestCustomerAggregate, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(predicate));
    }

    public Task<IReadOnlyList<TestCustomerAggregate>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TestCustomerAggregate>>(_store.Values.ToList());
    }

    public Task<IReadOnlyList<TestCustomerAggregate>> ListAsync(ISpecification<TestCustomerAggregate> specification, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (specification != null) q = q.Where(specification.ToExpression());
        return Task.FromResult<IReadOnlyList<TestCustomerAggregate>>(q.ToList());
    }

    public Task<IReadOnlyList<TestCustomerAggregate>> ListAsync(Expression<Func<TestCustomerAggregate, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (predicate != null) q = q.Where(predicate);
        return Task.FromResult<IReadOnlyList<TestCustomerAggregate>>(q.ToList());
    }

    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((long)_store.Count);
    }

    public Task<long> CountAsync(ISpecification<TestCustomerAggregate> specification, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (specification != null) q = q.Where(specification.ToExpression());
        return Task.FromResult((long)q.Count());
    }

    public Task<long> CountAsync(Expression<Func<TestCustomerAggregate, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (predicate != null) q = q.Where(predicate);
        return Task.FromResult((long)q.Count());
    }

    public Task<PagedResult<TestCustomerAggregate>> GetPagedAsync(ISpecification<TestCustomerAggregate> specification, PagingOptions? pagingOptions = null, CancellationToken cancellationToken = default)
    {
        pagingOptions ??= new PagingOptions();
        var q = _store.Values.AsQueryable().Where(specification.ToExpression());
        var total = q.Count();
        var items = q.Skip((pagingOptions.PageNumber - 1) * pagingOptions.PageSize).Take(pagingOptions.PageSize).ToList();
        return Task.FromResult(new PagedResult<TestCustomerAggregate>(total, pagingOptions.PageNumber, pagingOptions.PageSize, items));
    }

    public Task SaveAsync(TestCustomerAggregate aggregate, CancellationToken cancellationToken = default)
    {
        // Pure DDD aggregate-level commit:
        _store[aggregate.Id] = aggregate;

        // Drain domain events to outbox and clear
        var events = aggregate.GetChanges().ToList();
        foreach (var ev in events)
        {
            PublishedOutboxEvents.Add(ev.GetType().Name);
        }
        aggregate.MarkEventsAsCommitted();

        return Task.CompletedTask;
    }
}

public class InMemoryDocumentRepository : IDocumentRepository<TestDocumentEntity, Guid, string>
{
    // (PartitionKey, Id) -> Document
    private readonly ConcurrentDictionary<(string, Guid), TestDocumentEntity> _store = new();

    public Task<TestDocumentEntity?> GetByIdAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue((partitionKey, id), out var doc);
        return Task.FromResult(doc);
    }

    public Task<TestDocumentEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var match = _store.FirstOrDefault(kv => kv.Key.Item2 == id).Value;
        return Task.FromResult<TestDocumentEntity?>(match);
    }

    public Task<bool> ExistsAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.ContainsKey((partitionKey, id)));
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.Any(kv => kv.Key.Item2 == id));
    }

    public Task<TestDocumentEntity> UpsertAsync(TestDocumentEntity document, string partitionKey, CancellationToken cancellationToken = default)
    {
        document.ETag = Guid.NewGuid().ToString("N");
        _store[(partitionKey, document.Id)] = document;
        return Task.FromResult(document);
    }

    public Task<TestDocumentEntity> ReplaceAsync(TestDocumentEntity document, string eTag, string partitionKey, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue((partitionKey, document.Id), out var existing))
            throw new KeyNotFoundException("Document not found");

        if (existing.ETag != eTag)
            throw new InvalidOperationException("Optimistic concurrency violation: ETag mismatch");

        document.ETag = Guid.NewGuid().ToString("N");
        _store[(partitionKey, document.Id)] = document;
        return Task.FromResult(document);
    }

    public Task<bool> DeleteByIdAsync(Guid id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_store.TryRemove((partitionKey, id), out _));
    }

    public Task<bool> DeleteByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = _store.Keys.FirstOrDefault(k => k.Item2 == id);
        if (key != default)
        {
            return Task.FromResult(_store.TryRemove(key, out _));
        }
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<TestDocumentEntity>> ListByPartitionAsync(string partitionKey, ISpecification<TestDocumentEntity>? specification = null, CancellationToken cancellationToken = default)
    {
        var q = _store.Where(kv => kv.Key.Item1 == partitionKey).Select(kv => kv.Value).AsQueryable();
        if (specification != null) q = q.Where(specification.ToExpression());
        return Task.FromResult<IReadOnlyList<TestDocumentEntity>>(q.ToList());
    }

    public Task<PagedResult<TestDocumentEntity>> GetPagedByPartitionAsync(string partitionKey, ISpecification<TestDocumentEntity> specification, PagingOptions? pagingOptions = null, CancellationToken cancellationToken = default)
    {
        pagingOptions ??= new PagingOptions();
        var q = _store.Where(kv => kv.Key.Item1 == partitionKey).Select(kv => kv.Value).AsQueryable().Where(specification.ToExpression());
        var total = q.Count();
        var items = q.Skip((pagingOptions.PageNumber - 1) * pagingOptions.PageSize).Take(pagingOptions.PageSize).ToList();
        return Task.FromResult(new PagedResult<TestDocumentEntity>(total, pagingOptions.PageNumber, pagingOptions.PageSize, items));
    }

    public Task BulkInsertAsync(IEnumerable<TestDocumentEntity> documents, string partitionKey, CancellationToken cancellationToken = default)
    {
        foreach (var doc in documents)
        {
            _store[(partitionKey, doc.Id)] = doc;
        }
        return Task.CompletedTask;
    }

    public Task BulkUpsertAsync(IEnumerable<TestDocumentEntity> documents, string partitionKey, CancellationToken cancellationToken = default)
    {
        return BulkInsertAsync(documents, partitionKey, cancellationToken);
    }

    public Task<TestDocumentEntity> AddAsync(TestDocumentEntity entity, CancellationToken cancellationToken = default)
        => UpsertAsync(entity, entity.PartitionKey, cancellationToken);

    public Task AddRangeAsync(IEnumerable<TestDocumentEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities) _store[(e.PartitionKey, e.Id)] = e;
        return Task.CompletedTask;
    }

    public Task<TestDocumentEntity> UpdateAsync(TestDocumentEntity entity, CancellationToken cancellationToken = default)
        => UpsertAsync(entity, entity.PartitionKey, cancellationToken);

    public Task UpdateRangeAsync(IEnumerable<TestDocumentEntity> entities, CancellationToken cancellationToken = default)
        => AddRangeAsync(entities, cancellationToken);

    public Task<bool> DeleteAsync(TestDocumentEntity entity, CancellationToken cancellationToken = default)
        => DeleteByIdAsync(entity.Id, entity.PartitionKey, cancellationToken);

    public Task<int> DeleteRangeAsync(Expression<Func<TestDocumentEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var matches = _store.Values.AsQueryable().Where(predicate).ToList();
        foreach (var m in matches) DeleteByIdAsync(m.Id, m.PartitionKey, cancellationToken);
        return Task.FromResult(matches.Count);
    }

    public Task DeleteRangeAsync(IEnumerable<TestDocumentEntity> entities, CancellationToken cancellationToken = default)
    {
        foreach (var e in entities) DeleteByIdAsync(e.Id, e.PartitionKey, cancellationToken);
        return Task.CompletedTask;
    }

    public Task<TestDocumentEntity?> FindAsync(ISpecification<TestDocumentEntity> specification, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(specification.ToExpression()));

    public Task<TestDocumentEntity?> FindAsync(Expression<Func<TestDocumentEntity, bool>> predicate, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(predicate));

    public Task<IReadOnlyList<TestDocumentEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TestDocumentEntity>>(_store.Values.ToList());
    }

    public Task<IReadOnlyList<TestDocumentEntity>> ListAsync(ISpecification<TestDocumentEntity> specification, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (specification != null) q = q.Where(specification.ToExpression());
        return Task.FromResult<IReadOnlyList<TestDocumentEntity>>(q.ToList());
    }

    public Task<IReadOnlyList<TestDocumentEntity>> ListAsync(Expression<Func<TestDocumentEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (predicate != null) q = q.Where(predicate);
        return Task.FromResult<IReadOnlyList<TestDocumentEntity>>(q.ToList());
    }

    public Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult((long)_store.Count);
    }

    public Task<long> CountAsync(ISpecification<TestDocumentEntity> specification, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (specification != null) q = q.Where(specification.ToExpression());
        return Task.FromResult((long)q.Count());
    }

    public Task<long> CountAsync(Expression<Func<TestDocumentEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var q = _store.Values.AsQueryable();
        if (predicate != null) q = q.Where(predicate);
        return Task.FromResult((long)q.Count());
    }

    public Task<PagedResult<TestDocumentEntity>> GetPagedAsync(ISpecification<TestDocumentEntity> specification, PagingOptions? pagingOptions = null, CancellationToken cancellationToken = default)
    {
        pagingOptions ??= new PagingOptions();
        var q = _store.Values.AsQueryable().Where(specification.ToExpression());
        var total = q.Count();
        var items = q.Skip((pagingOptions.PageNumber - 1) * pagingOptions.PageSize).Take(pagingOptions.PageSize).ToList();
        return Task.FromResult(new PagedResult<TestDocumentEntity>(total, pagingOptions.PageNumber, pagingOptions.PageSize, items));
    }
}

public class InMemoryUnitOfWork : IUnitOfWork
{
    public bool HasActiveTransaction { get; private set; }
    public int CommitsCount { get; private set; }
    public int RollbacksCount { get; private set; }
    public int SavesCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SavesCount++;
        return Task.FromResult(1);
    }

    public Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        HasActiveTransaction = true;
        return Task.CompletedTask;
    }

    public Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        HasActiveTransaction = false;
        CommitsCount++;
        return Task.CompletedTask;
    }

    public Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        HasActiveTransaction = false;
        RollbacksCount++;
        return Task.CompletedTask;
    }

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// =============================================================================
// Tests
// =============================================================================

public class RepositoryHierarchyAndNoSqlTests
{
    // =========================================================================
    // 1. Pure DDD Aggregate Root Repository Tests (SaveAsync)
    // =========================================================================

    [Fact]
    public async Task AggregateRepository_SaveAsync_ShouldCommitAggregateDirectly()
    {
        var repo = new InMemoryAggregateRepository();
        var id = Guid.NewGuid();
        var customer = new TestCustomerAggregate(id, "John Doe", "john@example.com", 500m);

        // Mutate aggregate
        customer.Deposit(250m);
        customer.UpdateName("Johnathon Doe");

        // SaveAsync commits directly without IUnitOfWork
        await repo.SaveAsync(customer);

        var retrieved = await repo.GetByIdAsync(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Johnathon Doe", retrieved.Name);
        Assert.Equal(750m, retrieved.Balance);
    }

    [Fact]
    public async Task AggregateRepository_DeleteByIdAsync_ShouldRemoveAggregate()
    {
        var repo = new InMemoryAggregateRepository();
        var id = Guid.NewGuid();
        var customer = new TestCustomerAggregate(id, "Alice", "alice@example.com");

        await repo.AddAsync(customer);
        Assert.True(await repo.ExistsAsync(id));

        var deleted = await repo.DeleteByIdAsync(id);
        Assert.True(deleted);
        Assert.False(await repo.ExistsAsync(id));
    }

    [Fact]
    public async Task AggregateRepository_ListAsync_WithSpecification_ShouldFilter()
    {
        var repo = new InMemoryAggregateRepository();
        await repo.AddAsync(new TestCustomerAggregate(Guid.NewGuid(), "Cust 1", "c1@ex.com", 100m));
        await repo.AddAsync(new TestCustomerAggregate(Guid.NewGuid(), "Cust 2", "c2@ex.com", 500m));
        await repo.AddAsync(new TestCustomerAggregate(Guid.NewGuid(), "Cust 3", "c3@ex.com", 50m));

        var spec = new TestSpecification<TestCustomerAggregate>(c => c.Balance >= 100m);
        var matches = await repo.ListAsync(spec);

        Assert.Equal(2, matches.Count);
        Assert.Equal(2, await repo.CountAsync(spec));
    }

    // =========================================================================
    // 2. NoSQL Document Repository Tests (IDocumentRepository)
    // =========================================================================

    [Fact]
    public async Task DocumentRepository_PointReadByPartition_ShouldRetrieveDocument()
    {
        var docRepo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var doc = new TestDocumentEntity(id, "USA", "User Data Payload");

        await docRepo.UpsertAsync(doc, "USA");

        var retrieved = await docRepo.GetByIdAsync(id, "USA");
        Assert.NotNull(retrieved);
        Assert.Equal("USA", retrieved.PartitionKey);
        Assert.Equal("User Data Payload", retrieved.Payload);
    }

    [Fact]
    public async Task DocumentRepository_PointReadWrongPartition_ShouldReturnNull()
    {
        var docRepo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var doc = new TestDocumentEntity(id, "USA", "Payload");

        await docRepo.UpsertAsync(doc, "USA");

        // Wrong partition key
        var notFound = await docRepo.GetByIdAsync(id, "UK");
        Assert.Null(notFound);
    }

    [Fact]
    public async Task DocumentRepository_UpsertAsync_ShouldInsertThenUpdate()
    {
        var docRepo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var doc = new TestDocumentEntity(id, "Europe", "Initial V1");

        // First Upsert -> Insert
        await docRepo.UpsertAsync(doc, "Europe");
        var v1 = await docRepo.GetByIdAsync(id, "Europe");
        Assert.Equal("Initial V1", v1!.Payload);

        // Second Upsert -> Replace
        doc.Payload = "Updated V2";
        await docRepo.UpsertAsync(doc, "Europe");
        var v2 = await docRepo.GetByIdAsync(id, "Europe");
        Assert.Equal("Updated V2", v2!.Payload);
    }

    [Fact]
    public async Task DocumentRepository_ReplaceAsync_WithMatchingETag_ShouldSucceed()
    {
        var docRepo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var doc = new TestDocumentEntity(id, "Asia", "Original Content");

        await docRepo.UpsertAsync(doc, "Asia");
        var initialETag = doc.ETag;

        doc.Payload = "Concurrency Safe Update";
        var updated = await docRepo.ReplaceAsync(doc, initialETag, "Asia");

        Assert.NotEqual(initialETag, updated.ETag);
        Assert.Equal("Concurrency Safe Update", updated.Payload);
    }

    [Fact]
    public async Task DocumentRepository_ReplaceAsync_WithMismatchedETag_ShouldThrowConcurrencyViolation()
    {
        var docRepo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var doc = new TestDocumentEntity(id, "Asia", "Original Content");

        await docRepo.UpsertAsync(doc, "Asia");

        // Simulate stale client with old/wrong ETag
        doc.Payload = "Stale Update";
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            docRepo.ReplaceAsync(doc, "wrong-stale-etag-12345", "Asia"));
    }

    [Fact]
    public async Task DocumentRepository_ListByPartition_ShouldOnlyReturnPartitionItems()
    {
        var docRepo = new InMemoryDocumentRepository();
        await docRepo.UpsertAsync(new TestDocumentEntity(Guid.NewGuid(), "RegionA", "A1"), "RegionA");
        await docRepo.UpsertAsync(new TestDocumentEntity(Guid.NewGuid(), "RegionA", "A2"), "RegionA");
        await docRepo.UpsertAsync(new TestDocumentEntity(Guid.NewGuid(), "RegionB", "B1"), "RegionB");

        var regionAItems = await docRepo.ListByPartitionAsync("RegionA");
        var regionBItems = await docRepo.ListByPartitionAsync("RegionB");

        Assert.Equal(2, regionAItems.Count);
        Assert.Single(regionBItems);
    }

    [Fact]
    public async Task DocumentRepository_BulkInsert_ShouldInsertAll()
    {
        var docRepo = new InMemoryDocumentRepository();
        var docs = Enumerable.Range(1, 10)
            .Select(i => new TestDocumentEntity(Guid.NewGuid(), "BatchTenant", $"Doc {i}"))
            .ToList();

        await docRepo.BulkInsertAsync(docs, "BatchTenant");

        var items = await docRepo.ListByPartitionAsync("BatchTenant");
        Assert.Equal(10, items.Count);
    }

    // =========================================================================
    // 3. Lean IUnitOfWork Transaction Boundary Tests
    // =========================================================================

    [Fact]
    public async Task UnitOfWork_TransactionLifecycle_ShouldCoordinateState()
    {
        var uow = new InMemoryUnitOfWork();

        Assert.False(uow.HasActiveTransaction);

        await uow.BeginTransactionAsync();
        Assert.True(uow.HasActiveTransaction);

        await uow.SaveChangesAsync();
        Assert.Equal(1, uow.SavesCount);

        await uow.CommitTransactionAsync();
        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(1, uow.CommitsCount);
    }

    [Fact]
    public async Task UnitOfWork_Rollback_ShouldResetTransaction()
    {
        var uow = new InMemoryUnitOfWork();

        await uow.BeginTransactionAsync();
        Assert.True(uow.HasActiveTransaction);

        await uow.RollbackTransactionAsync();
        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(1, uow.RollbacksCount);
    }

    // =========================================================================
    // 4. Extended NoSQL Partition Routing & Concurrency Tests
    // =========================================================================

    [Theory]
    [InlineData("tenant-europe", 3)]
    [InlineData("tenant-americas", 5)]
    [InlineData("tenant-asia", 2)]
    public async Task DocumentRepository_PartitionIsolation_ShouldOnlyQueryPartition(string partitionKey, int count)
    {
        var repo = new InMemoryDocumentRepository();

        for (int i = 0; i < count; i++)
        {
            var doc = new TestDocumentEntity(Guid.NewGuid(), partitionKey, $"Payload {i}");
            await repo.UpsertAsync(doc, partitionKey);
        }

        // Add foreign document in another partition
        await repo.UpsertAsync(new TestDocumentEntity(Guid.NewGuid(), "foreign-partition", "foreign"), "foreign-partition");

        var partitionDocs = await repo.ListByPartitionAsync(partitionKey);

        Assert.Equal(count, partitionDocs.Count);
        Assert.All(partitionDocs, d => Assert.Equal(partitionKey, d.PartitionKey));
    }

    [Fact]
    public async Task DocumentRepository_DeleteWithPartitionKey_ShouldRemoveTargetOnly()
    {
        var repo = new InMemoryDocumentRepository();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var part = "partition-A";

        await repo.UpsertAsync(new TestDocumentEntity(id1, part, "data1"), part);
        await repo.UpsertAsync(new TestDocumentEntity(id2, part, "data2"), part);

        await repo.DeleteByIdAsync(id1, part);

        var remaining = await repo.ListByPartitionAsync(part);
        Assert.Single(remaining);
        Assert.Equal(id2, remaining[0].Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DocumentRepository_ETagOptimisticConcurrency_ShouldValidateETag(bool matchExpected)
    {
        var repo = new InMemoryDocumentRepository();
        var id = Guid.NewGuid();
        var partition = "sales-dept";
        var originalDoc = new TestDocumentEntity(id, partition, "Initial State");

        await repo.UpsertAsync(originalDoc, partition);

        var existing = await repo.GetByIdAsync(id, partition);
        Assert.NotNull(existing);

        string testEtag = matchExpected ? existing.ETag : "mismatched-etag";
        bool success;
        if (existing.ETag == testEtag)
        {
            existing.Payload = "Updated State";
            await repo.ReplaceAsync(existing, testEtag, partition);
            success = true;
        }
        else
        {
            success = false;
        }

        Assert.Equal(matchExpected, success);
    }

    // =========================================================================
    // 5. Extended Basic & Read-Only Repository Query Tests
    // =========================================================================

    [Theory]
    [InlineData(1, true)]
    [InlineData(999, false)]
    public async Task ReadOnlyRepository_ExistsAsync_ShouldVerifyEntityPresence(int id, bool expected)
    {
        var mockRepo = new Mock<IReadOnlyRepository<TestProductEntity, int>>();
        var ids = new HashSet<int> { 1, 2, 3 };

        mockRepo.Setup(r => r.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int queryId, CancellationToken _) => ids.Contains(queryId));

        var exists = await mockRepo.Object.ExistsAsync(id);

        Assert.Equal(expected, exists);
    }

    [Theory]
    [InlineData("Electronics", 2)]
    [InlineData("Furniture", 1)]
    [InlineData("Clothing", 0)]
    public async Task ReadOnlyRepository_CountAsync_WithPredicate_ShouldReturnMatchingCount(string category, int expectedCount)
    {
        var mockRepo = new Mock<IReadOnlyRepository<TestProductEntity, int>>();
        var products = new List<TestProductEntity>
        {
            new(1, "Phone", "Electronics", 800m, 10),
            new(2, "Headphones", "Electronics", 150m, 25),
            new(3, "Desk", "Furniture", 400m, 5)
        };

        mockRepo.Setup(r => r.CountAsync(It.IsAny<Expression<Func<TestProductEntity, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<TestProductEntity, bool>> expr, CancellationToken _) =>
                (long)products.AsQueryable().Count(expr));

        var count = await mockRepo.Object.CountAsync(p => p.Category == category);

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public async Task UnitOfWork_MultipleOperationsInSingleTransaction_ShouldCommitTogether()
    {
        var uow = new InMemoryUnitOfWork();
        await uow.BeginTransactionAsync();

        // Simulate 3 saves in single transaction
        await uow.SaveChangesAsync();
        await uow.SaveChangesAsync();
        await uow.SaveChangesAsync();

        Assert.Equal(3, uow.SavesCount);
        Assert.True(uow.HasActiveTransaction);

        await uow.CommitTransactionAsync();
        Assert.False(uow.HasActiveTransaction);
        Assert.Equal(1, uow.CommitsCount);
    }
}
