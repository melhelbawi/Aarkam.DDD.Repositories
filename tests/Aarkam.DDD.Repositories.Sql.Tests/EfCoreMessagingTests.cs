using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.DDD.Repositories.Sql.Messaging;
using Aarkam.DDD.Repositories.Sql.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarkam.DDD.Repositories.Sql.Tests;

public class TestOrderSagaData
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class TestOrderSaga : ISaga<TestOrderSagaData>
{
    public Guid SagaId { get; set; }
    public TestOrderSagaData Data { get; set; } = new();
    public SagaStatus Status { get; set; } = SagaStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long Version { get; set; } = 1;

    public TestOrderSaga() { }

    public TestOrderSaga(Guid sagaId, string orderNumber, decimal amount)
    {
        SagaId = sagaId;
        Data = new TestOrderSagaData { OrderNumber = orderNumber, Amount = amount };
    }

    public void Complete()
    {
        Status = SagaStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string reason)
    {
        Status = SagaStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Compensate()
    {
        Status = SagaStatus.Compensating;
    }
}

public class EfCoreMessagingTests : IDisposable
{
    private readonly TestSqlDbContext _dbContext;
    private readonly EfCoreOutboxRepository _outboxRepo;
    private readonly EfCoreInboxRepository _inboxRepo;
    private readonly EfCoreSagaRepository _sagaRepo;
    private readonly EfCoreSnapshotRepository _snapshotRepo;

    public EfCoreMessagingTests()
    {
        var options = new DbContextOptionsBuilder<TestSqlDbContext>()
            .UseInMemoryDatabase(databaseName: $"EfMessagingTest_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TestSqlDbContext(options);
        _outboxRepo = new EfCoreOutboxRepository(_dbContext);
        _inboxRepo = new EfCoreInboxRepository(_dbContext);
        _sagaRepo = new EfCoreSagaRepository(_dbContext);
        _snapshotRepo = new EfCoreSnapshotRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // =========================================================================
    // 1. Outbox Pattern Tests
    // =========================================================================

    [Fact]
    public async Task Outbox_AddAndGetPending_ShouldReturnUnprocessedMessagesOrderedByCreatedAt()
    {
        var msg1 = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "OrderPlacedEvent",
            Payload = "{\"OrderId\": 1}",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        var msg2 = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "PaymentCapturedEvent",
            Payload = "{\"OrderId\": 1}",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        };

        await _outboxRepo.AddAsync(msg1);
        await _outboxRepo.AddAsync(msg2);
        await _dbContext.SaveChangesAsync();

        var pending = await _outboxRepo.GetPendingAsync(10);

        Assert.Equal(2, pending.Count);
        Assert.Equal(msg1.Id, pending[0].Id);
        Assert.Equal(msg2.Id, pending[1].Id);
    }

    [Fact]
    public async Task Outbox_MarkAsProcessed_ShouldUpdateStatusAndProcessedTimestamp()
    {
        var msg = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "OrderShippedEvent",
            Payload = "{}"
        };
        await _outboxRepo.AddAsync(msg);
        await _dbContext.SaveChangesAsync();

        await _outboxRepo.MarkAsProcessedAsync(msg.Id);

        var retrieved = await _dbContext.Set<OutboxMessageRecord>().FindAsync(msg.Id);
        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.ProcessedAt);

        var pending = await _outboxRepo.GetPendingAsync(10);
        Assert.DoesNotContain(pending, m => m.Id == msg.Id);
    }

    [Fact]
    public async Task Outbox_RecordFailure_ShouldIncrementRetryCountAndSetError()
    {
        var msg = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "EmailNotificationEvent",
            Payload = "{}"
        };
        await _outboxRepo.AddAsync(msg);
        await _dbContext.SaveChangesAsync();

        await _outboxRepo.RecordFailureAsync(msg.Id, "SMTP connection timed out");

        var retrieved = await _dbContext.Set<OutboxMessageRecord>().FindAsync(msg.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved.RetryCount);
        Assert.Equal("SMTP connection timed out", retrieved.LastError);
    }

    [Fact]
    public async Task Outbox_CleanupAsync_ShouldRemoveProcessedMessagesOlderThanCutoff()
    {
        var oldMsg = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "OldEvent",
            Payload = "{}",
            ProcessedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var recentMsg = new OutboxMessageRecord
        {
            Id = Guid.NewGuid(),
            EventType = "RecentEvent",
            Payload = "{}",
            ProcessedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        await _outboxRepo.AddAsync(oldMsg);
        await _outboxRepo.AddAsync(recentMsg);
        await _dbContext.SaveChangesAsync();

        var removed = await _outboxRepo.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-2));

        Assert.Equal(1, removed);
        var remaining = await _dbContext.Set<OutboxMessageRecord>().ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(recentMsg.Id, remaining[0].Id);
    }

    // =========================================================================
    // 2. Inbox Pattern Tests (Idempotency)
    // =========================================================================

    [Fact]
    public async Task Inbox_HasBeenProcessedAsync_ShouldReturnTrueOnlyAfterRecordAsync()
    {
        var eventId = Guid.NewGuid();
        var consumer = "OrderBillingHandler";

        Assert.False(await _inboxRepo.HasBeenProcessedAsync(eventId, consumer));

        await _inboxRepo.RecordAsync(new InboxMessageRecord
        {
            EventId = eventId,
            ConsumerName = consumer,
            EventType = "OrderPlacedEvent"
        });
        await _dbContext.SaveChangesAsync();

        Assert.True(await _inboxRepo.HasBeenProcessedAsync(eventId, consumer));
        Assert.False(await _inboxRepo.HasBeenProcessedAsync(eventId, "DifferentConsumer"));
    }

    [Fact]
    public async Task Inbox_CleanupAsync_ShouldRemoveRecordsOlderThanCutoff()
    {
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        await _inboxRepo.RecordAsync(new InboxMessageRecord
        {
            EventId = eventId1,
            ConsumerName = "ConsumerA",
            EventType = "OldEvent",
            ProcessedAt = DateTimeOffset.UtcNow.AddDays(-30)
        });
        await _inboxRepo.RecordAsync(new InboxMessageRecord
        {
            EventId = eventId2,
            ConsumerName = "ConsumerA",
            EventType = "FreshEvent",
            ProcessedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var removed = await _inboxRepo.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));
        Assert.Equal(1, removed);

        Assert.False(await _inboxRepo.HasBeenProcessedAsync(eventId1, "ConsumerA"));
        Assert.True(await _inboxRepo.HasBeenProcessedAsync(eventId2, "ConsumerA"));
    }

    // =========================================================================
    // 3. Saga Pattern Tests (Distributed Transactions & Concurrency)
    // =========================================================================

    [Fact]
    public async Task Saga_SaveAndLoad_ShouldSerializeAndRestoreSagaState()
    {
        var sagaId = Guid.NewGuid();
        var saga = new TestOrderSaga(sagaId, "ORD-SAGA-100", 350.50m);

        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(saga);

        var loaded = await _sagaRepo.LoadAsync<TestOrderSaga, TestOrderSagaData>(sagaId);
        Assert.NotNull(loaded);
        Assert.Equal(sagaId, loaded.SagaId);
        Assert.Equal("ORD-SAGA-100", loaded.Data.OrderNumber);
        Assert.Equal(350.50m, loaded.Data.Amount);
        Assert.Equal(SagaStatus.Running, loaded.Status);
    }

    [Fact]
    public async Task Saga_SaveAsync_WhenOptimisticConcurrencyConflict_ShouldThrowException()
    {
        var sagaId = Guid.NewGuid();
        var saga1 = new TestOrderSaga(sagaId, "ORD-OCC", 100m) { Version = 1 };
        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(saga1);

        // Instance 1 loads and updates version
        var loaded1 = await _sagaRepo.LoadAsync<TestOrderSaga, TestOrderSagaData>(sagaId);
        Assert.NotNull(loaded1);

        // Simulate stale instance with version 1 attempting to save after another transaction bumped version
        var staleSaga = new TestOrderSaga(sagaId, "ORD-OCC-STALE", 200m) { Version = 999 };

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(staleSaga));
    }

    [Fact]
    public async Task Saga_FindTimedOutSagasAsync_ShouldReturnActiveSagasExceedingThreshold()
    {
        var timedOutId = Guid.NewGuid();
        var activeId = Guid.NewGuid();

        var timedOutSaga = new TestOrderSaga(timedOutId, "ORD-TIMEOUT", 10m)
        {
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-60),
            Status = SagaStatus.Running
        };

        var activeSaga = new TestOrderSaga(activeId, "ORD-ACTIVE", 20m)
        {
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            Status = SagaStatus.Running
        };

        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(timedOutSaga);
        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(activeSaga);

        var timedOutList = await _sagaRepo.FindTimedOutSagasAsync(TimeSpan.FromMinutes(30));

        Assert.Contains(timedOutId, timedOutList);
        Assert.DoesNotContain(activeId, timedOutList);
    }

    [Fact]
    public async Task Saga_CleanupAsync_ShouldRemoveCompletedSagasOlderThanCutoff()
    {
        var oldCompletedId = Guid.NewGuid();
        var runningId = Guid.NewGuid();

        var oldCompletedSaga = new TestOrderSaga(oldCompletedId, "ORD-COMP", 10m)
        {
            Status = SagaStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow.AddDays(-14)
        };
        var runningSaga = new TestOrderSaga(runningId, "ORD-RUN", 20m)
        {
            Status = SagaStatus.Running
        };

        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(oldCompletedSaga);
        await _sagaRepo.SaveAsync<TestOrderSaga, TestOrderSagaData>(runningSaga);

        var removed = await _sagaRepo.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7));
        Assert.Equal(1, removed);

        var loadedCompleted = await _sagaRepo.LoadAsync<TestOrderSaga, TestOrderSagaData>(oldCompletedId);
        var loadedRunning = await _sagaRepo.LoadAsync<TestOrderSaga, TestOrderSagaData>(runningId);

        Assert.Null(loadedCompleted);
        Assert.NotNull(loadedRunning);
    }

    // =========================================================================
    // 4. Snapshot Pattern Tests (Event Sourcing State Checkpoints)
    // =========================================================================

    [Fact]
    public async Task Snapshot_SaveAndGetLatest_ShouldReturnHighestVersionCheckpoint()
    {
        var aggregateId = Guid.NewGuid();

        await _snapshotRepo.SaveAsync(new SnapshotRecord
        {
            AggregateId = aggregateId.ToString(),
            AggregateType = "Order",
            Version = 5,
            State = "{\"Total\": 500}"
        });

        await _snapshotRepo.SaveAsync(new SnapshotRecord
        {
            AggregateId = aggregateId.ToString(),
            AggregateType = "Order",
            Version = 10,
            State = "{\"Total\": 1000}"
        });

        var latest = await _snapshotRepo.GetLatestAsync(aggregateId);

        Assert.NotNull(latest);
        Assert.Equal(10, latest.Version);
        Assert.Equal("{\"Total\": 1000}", latest.State);
    }

    [Fact]
    public async Task Snapshot_GetAtVersionAsync_ShouldReturnExactOrLatestPriorVersion()
    {
        var aggregateId = Guid.NewGuid();

        await _snapshotRepo.SaveAsync(new SnapshotRecord { AggregateId = aggregateId.ToString(), AggregateType = "Order", Version = 5, State = "V5" });
        await _snapshotRepo.SaveAsync(new SnapshotRecord { AggregateId = aggregateId.ToString(), AggregateType = "Order", Version = 10, State = "V10" });
        await _snapshotRepo.SaveAsync(new SnapshotRecord { AggregateId = aggregateId.ToString(), AggregateType = "Order", Version = 15, State = "V15" });

        var atVersion7 = await _snapshotRepo.GetAtVersionAsync(aggregateId, 7);
        Assert.NotNull(atVersion7);
        Assert.Equal(5, atVersion7.Version);

        var atVersion10 = await _snapshotRepo.GetAtVersionAsync(aggregateId, 10);
        Assert.NotNull(atVersion10);
        Assert.Equal(10, atVersion10.Version);
    }

    [Fact]
    public async Task Snapshot_PruneAsync_ShouldKeepSpecifiedNumberOfLatestSnapshots()
    {
        var aggregateId = Guid.NewGuid();

        for (int v = 1; v <= 5; v++)
        {
            await _snapshotRepo.SaveAsync(new SnapshotRecord
            {
                AggregateId = aggregateId.ToString(),
                AggregateType = "Order",
                Version = v,
                State = $"Snapshot_V{v}"
            });
        }

        // Keep 2 latest snapshots (versions 5 and 4), prune versions 1, 2, 3
        var prunedCount = await _snapshotRepo.PruneAsync(aggregateId, keepCount: 2);
        Assert.Equal(3, prunedCount);

        var allRemaining = await _dbContext.Set<SnapshotRecord>()
            .Where(s => s.AggregateId == aggregateId.ToString())
            .OrderBy(s => s.Version)
            .ToListAsync();

        Assert.Equal(2, allRemaining.Count);
        Assert.Equal(4, allRemaining[0].Version);
        Assert.Equal(5, allRemaining[1].Version);
    }
}
