using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Moq;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class SagaAndSnapshotRepositoryTests
{
    // =========================================================================
    // 1. ISagaRepository - Loading & State Restoration
    // =========================================================================

    [Fact]
    public async Task Saga_LoadAsync_WhenExists_ShouldReturnSagaInstance()
    {
        var sagaId = Guid.NewGuid();
        var existingSaga = new TestOrderSaga
        {
            SagaId = sagaId,
            Data = new TestSagaData { OrderId = "ORD-999", TotalAmount = 450m },
            Status = SagaStatus.Running,
            Version = 3
        };

        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.LoadAsync<TestOrderSaga, TestSagaData>(sagaId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSaga);

        var loaded = await mockRepo.Object.LoadAsync<TestOrderSaga, TestSagaData>(sagaId);

        Assert.NotNull(loaded);
        Assert.Equal(sagaId, loaded.SagaId);
        Assert.Equal("ORD-999", loaded.Data.OrderId);
        Assert.Equal(450m, loaded.Data.TotalAmount);
        Assert.Equal(SagaStatus.Running, loaded.Status);
        Assert.Equal(3, loaded.Version);
    }

    [Fact]
    public async Task Saga_LoadAsync_WhenNotFound_ShouldReturnNull()
    {
        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.LoadAsync<TestOrderSaga, TestSagaData>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestOrderSaga?)null);

        var loaded = await mockRepo.Object.LoadAsync<TestOrderSaga, TestSagaData>(Guid.NewGuid());

        Assert.Null(loaded);
    }

    // =========================================================================
    // 2. ISagaRepository - Saving & Optimistic Concurrency
    // =========================================================================

    [Fact]
    public async Task Saga_SaveAsync_ShouldPersistMutatedState()
    {
        var sagaStore = new Dictionary<Guid, (SagaStatus Status, long Version)>();

        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.SaveAsync<TestOrderSaga, TestSagaData>(It.IsAny<TestOrderSaga>(), It.IsAny<CancellationToken>()))
            .Callback<TestOrderSaga, CancellationToken>((s, _) => sagaStore[s.SagaId] = (s.Status, s.Version))
            .Returns(Task.CompletedTask);

        var saga = new TestOrderSaga();
        saga.Data.InventoryReserved = true;
        saga.Data.PaymentProcessed = true;
        saga.Complete();

        await mockRepo.Object.SaveAsync<TestOrderSaga, TestSagaData>(saga);

        Assert.True(sagaStore.ContainsKey(saga.SagaId));
        Assert.Equal(SagaStatus.Completed, sagaStore[saga.SagaId].Status);
    }

    [Theory]
    [InlineData(SagaStatus.Pending)]
    [InlineData(SagaStatus.Running)]
    [InlineData(SagaStatus.Completed)]
    [InlineData(SagaStatus.Failed)]
    [InlineData(SagaStatus.Compensating)]
    [InlineData(SagaStatus.TimedOut)]
    public async Task Saga_StatusTransitions_ShouldReflectAccurately(SagaStatus status)
    {
        var saga = new TestOrderSaga { Status = status };

        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.SaveAsync<TestOrderSaga, TestSagaData>(saga, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mockRepo.Object.SaveAsync<TestOrderSaga, TestSagaData>(saga);

        Assert.Equal(status, saga.Status);
    }

    [Fact]
    public void Saga_Compensate_ShouldTransitionStatusToCompensating()
    {
        var saga = new TestOrderSaga();
        Assert.Equal(SagaStatus.Running, saga.Status);

        saga.Compensate();

        Assert.Equal(SagaStatus.Compensating, saga.Status);
        Assert.Null(saga.CompletedAt); // Still running compensation
    }

    [Fact]
    public void Saga_Fail_ShouldTransitionStatusToFailedAndRecordTimestamp()
    {
        var saga = new TestOrderSaga();
        saga.Fail("Payment gateway declined transaction");

        Assert.Equal(SagaStatus.Failed, saga.Status);
        Assert.NotNull(saga.CompletedAt);
    }

    // =========================================================================
    // 3. ISagaRepository - Timeout Detection & Queries
    // =========================================================================

    [Theory]
    [InlineData(10, 2)]  // 10s timeout, finds 2 sagas
    [InlineData(30, 1)]  // 30s timeout, finds 1 saga
    [InlineData(60, 0)]  // 60s timeout, finds 0 sagas
    public async Task Saga_FindTimedOutSagasAsync_ShouldReturnStaleSagas(int timeoutSeconds, int expectedCount)
    {
        var now = DateTimeOffset.UtcNow;
        var sagas = new List<(Guid Id, DateTimeOffset StartedAt, SagaStatus Status)>
        {
            (Guid.NewGuid(), now.AddSeconds(-45), SagaStatus.Running), // 45s old
            (Guid.NewGuid(), now.AddSeconds(-20), SagaStatus.Running), // 20s old
            (Guid.NewGuid(), now.AddSeconds(-5), SagaStatus.Running),  // 5s old
            (Guid.NewGuid(), now.AddSeconds(-50), SagaStatus.Completed) // Completed, not running
        };

        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.FindTimedOutSagasAsync(It.IsAny<TimeSpan>(), It.IsAny<SagaStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TimeSpan timeout, SagaStatus status, CancellationToken _) =>
            {
                var threshold = now - timeout;
                return sagas
                    .Where(s => s.Status == status && s.StartedAt <= threshold)
                    .Select(s => s.Id)
                    .ToList();
            });

        var timedOut = await mockRepo.Object.FindTimedOutSagasAsync(
            TimeSpan.FromSeconds(timeoutSeconds),
            SagaStatus.Running);

        Assert.Equal(expectedCount, timedOut.Count);
    }

    // =========================================================================
    // 4. ISagaRepository - Retention Cleanup
    // =========================================================================

    [Theory]
    [InlineData(30, 2)]
    [InlineData(60, 1)]
    [InlineData(90, 0)]
    public async Task Saga_CleanupAsync_ShouldPurgeCompletedSagasOlderThanCutoff(int daysOld, int expectedPurged)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new List<(Guid Id, SagaStatus Status, DateTimeOffset? CompletedAt)>
        {
            (Guid.NewGuid(), SagaStatus.Completed, now.AddDays(-75)),
            (Guid.NewGuid(), SagaStatus.Completed, now.AddDays(-45)),
            (Guid.NewGuid(), SagaStatus.Completed, now.AddDays(-10)),
            (Guid.NewGuid(), SagaStatus.Running, null) // Running must never be deleted
        };

        var mockRepo = new Mock<ISagaRepository>();
        mockRepo.Setup(r => r.CleanupAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset cutoff, CancellationToken _) =>
            {
                return list.RemoveAll(s => s.Status == SagaStatus.Completed && s.CompletedAt < cutoff);
            });

        var purged = await mockRepo.Object.CleanupAsync(now.AddDays(-daysOld));

        Assert.Equal(expectedPurged, purged);
    }

    // =========================================================================
    // 5. ISnapshotRepository - Snapshot Management
    // =========================================================================

    [Fact]
    public async Task Snapshot_GetLatestAsync_ShouldReturnHighestVersionSnapshot()
    {
        var aggregateId = Guid.NewGuid();
        var snapshots = new List<ISnapshot>
        {
            new TestSnapshot(aggregateId.ToString(), "Order", 5, "{\"state\":\"v5\"}"),
            new TestSnapshot(aggregateId.ToString(), "Order", 10, "{\"state\":\"v10\"}"),
            new TestSnapshot(aggregateId.ToString(), "Order", 15, "{\"state\":\"v15\"}")
        };

        var mockRepo = new Mock<ISnapshotRepository>();
        mockRepo.Setup(r => r.GetLatestAsync(aggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshots.OrderByDescending(s => s.Version).FirstOrDefault());

        var latest = await mockRepo.Object.GetLatestAsync(aggregateId);

        Assert.NotNull(latest);
        Assert.Equal(15, latest.Version);
        Assert.Equal("{\"state\":\"v15\"}", latest.State);
    }

    [Theory]
    [InlineData(20, 15)]  // requested <= 20 -> snapshot 15
    [InlineData(12, 10)]  // requested <= 12 -> snapshot 10
    [InlineData(7, 5)]    // requested <= 7  -> snapshot 5
    [InlineData(4, 0)]    // requested <= 4  -> none exists
    public async Task Snapshot_GetAtVersionAsync_ShouldReturnHighestCheckpointUpToMaxVersion(long maxVersion, long expectedVersion)
    {
        var aggregateId = Guid.NewGuid();
        var snapshots = new List<ISnapshot>
        {
            new TestSnapshot(aggregateId.ToString(), "Order", 5, "{\"v\":5}"),
            new TestSnapshot(aggregateId.ToString(), "Order", 10, "{\"v\":10}"),
            new TestSnapshot(aggregateId.ToString(), "Order", 15, "{\"v\":15}")
        };

        var mockRepo = new Mock<ISnapshotRepository>();
        mockRepo.Setup(r => r.GetAtVersionAsync(aggregateId, It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, long maxVer, CancellationToken _) =>
                snapshots.Where(s => s.Version <= maxVer).OrderByDescending(s => s.Version).FirstOrDefault());

        var result = await mockRepo.Object.GetAtVersionAsync(aggregateId, maxVersion);

        if (expectedVersion == 0)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(expectedVersion, result.Version);
        }
    }

    [Fact]
    public async Task Snapshot_SaveAsync_ShouldStoreSnapshot()
    {
        var stored = new List<ISnapshot>();
        var mockRepo = new Mock<ISnapshotRepository>();
        mockRepo.Setup(r => r.SaveAsync(It.IsAny<ISnapshot>(), It.IsAny<CancellationToken>()))
            .Callback<ISnapshot, CancellationToken>((s, _) => stored.Add(s))
            .Returns(Task.CompletedTask);

        var aggregateId = Guid.NewGuid().ToString();
        var snap = new TestSnapshot(aggregateId, "Customer", 25, "{\"Balance\":500}", "tenant-omega");

        await mockRepo.Object.SaveAsync(snap);

        Assert.Single(stored);
        Assert.Equal(aggregateId, stored[0].AggregateId);
        Assert.Equal("Customer", stored[0].AggregateType);
        Assert.Equal(25, stored[0].Version);
        Assert.Equal("tenant-omega", stored[0].TenantId);
    }

    [Theory]
    [InlineData(1, 4)] // keep 1 out of 5 -> prune 4
    [InlineData(2, 3)] // keep 2 out of 5 -> prune 3
    [InlineData(3, 2)] // keep 3 out of 5 -> prune 2
    [InlineData(5, 0)] // keep 5 out of 5 -> prune 0
    public async Task Snapshot_PruneAsync_ShouldRetainConfiguredCountAndPruneOlder(int keepCount, int expectedPruned)
    {
        var aggregateId = Guid.NewGuid();
        var snapshots = Enumerable.Range(1, 5)
            .Select(i => (ISnapshot)new TestSnapshot(aggregateId.ToString(), "Order", i * 10, $"v{i}"))
            .ToList();

        var mockRepo = new Mock<ISnapshotRepository>();
        mockRepo.Setup(r => r.PruneAsync(aggregateId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, int keep, CancellationToken _) =>
            {
                var toKeep = snapshots.OrderByDescending(s => s.Version).Take(keep).ToHashSet();
                var toRemove = snapshots.Where(s => !toKeep.Contains(s)).ToList();
                foreach (var s in toRemove) snapshots.Remove(s);
                return toRemove.Count;
            });

        var pruned = await mockRepo.Object.PruneAsync(aggregateId, keepCount);

        Assert.Equal(expectedPruned, pruned);
        Assert.Equal(5 - expectedPruned, snapshots.Count);
    }

    // =========================================================================
    // 6. ISnapshotRepository<TAggregate> - Typed Aggregate Snapshot
    // =========================================================================

    [Fact]
    public async Task Snapshot_TypedRepository_ShouldQueryLatestByAggregateType()
    {
        var aggregateId = Guid.NewGuid();
        var mockRepo = new Mock<ISnapshotRepository<TestCustomerAggregate>>();
        var snapshot = new TestSnapshot(aggregateId.ToString(), nameof(TestCustomerAggregate), 50, "{\"Balance\":1000}");

        mockRepo.Setup(r => r.GetLatestAsync(aggregateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var result = await mockRepo.Object.GetLatestAsync(aggregateId);

        Assert.NotNull(result);
        Assert.Equal(nameof(TestCustomerAggregate), result.AggregateType);
        Assert.Equal(50, result.Version);
    }
}
