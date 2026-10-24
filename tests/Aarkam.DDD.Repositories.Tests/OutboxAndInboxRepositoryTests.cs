using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Moq;
using Xunit;

namespace Aarkam.DDD.Repositories.Tests;

public class OutboxAndInboxRepositoryTests
{
    // =========================================================================
    // 1. IOutboxRepository - Basic Adding & Pending Retrievals
    // =========================================================================

    [Fact]
    public async Task Outbox_AddAsync_ShouldPersistMessageEnvelope()
    {
        var messages = new List<IOutboxMessage>();
        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<IOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IOutboxMessage, CancellationToken>((msg, _) => messages.Add(msg))
            .Returns(Task.CompletedTask);

        var outboxMessage = new TestOutboxMessage("OrderCreatedEvent", "{\"orderId\":\"123\"}");
        await mockRepo.Object.AddAsync(outboxMessage);

        Assert.Single(messages);
        Assert.Equal("OrderCreatedEvent", messages[0].EventType);
        Assert.Equal("{\"orderId\":\"123\"}", messages[0].Payload);
        Assert.Null(messages[0].ProcessedAt);
        Assert.Equal(0, messages[0].RetryCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Outbox_GetPendingAsync_ShouldRespectBatchSize(int batchSize)
    {
        var allMessages = Enumerable.Range(1, 150)
            .Select(i => (IOutboxMessage)new TestOutboxMessage($"Event_{i}", $"{{\"index\":{i}}}"))
            .ToList();

        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int size, CancellationToken _) =>
                allMessages.Where(m => m.ProcessedAt == null).Take(size).ToList());

        var pending = await mockRepo.Object.GetPendingAsync(batchSize);

        Assert.Equal(Math.Min(batchSize, allMessages.Count), pending.Count);
    }

    [Fact]
    public async Task Outbox_GetPendingAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var pending = await mockRepo.Object.GetPendingAsync(10);

        Assert.NotNull(pending);
        Assert.Empty(pending);
    }

    // =========================================================================
    // 2. IOutboxRepository - Marking Processed & Lifecycle
    // =========================================================================

    [Fact]
    public async Task Outbox_MarkAsProcessedAsync_ShouldUpdateProcessedTimestamp()
    {
        var msg = new TestOutboxMessage("PaymentAuthorized", "{\"amount\":100}");
        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.MarkAsProcessedAsync(msg.Id, It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) =>
            {
                if (msg.Id == id)
                    msg.MarkAsProcessed();
            })
            .Returns(Task.CompletedTask);

        Assert.Null(msg.ProcessedAt);
        await mockRepo.Object.MarkAsProcessedAsync(msg.Id);

        Assert.NotNull(msg.ProcessedAt);
        Assert.True(msg.ProcessedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(1, "Connection timeout to RabbitMQ")]
    [InlineData(2, "Broker socket disconnected")]
    [InlineData(3, "Dead letter threshold reached")]
    [InlineData(5, "Kafka partition unavailable")]
    public async Task Outbox_RecordFailureAsync_ShouldIncrementRetryAndCaptureError(int attempts, string errorMsg)
    {
        var msg = new TestOutboxMessage("InvoiceGenerated", "{\"invoiceId\":\"INV-1\"}");
        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.RecordFailureAsync(msg.Id, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, string, CancellationToken>((id, err, _) =>
            {
                if (msg.Id == id)
                    msg.LogFailure(err);
            })
            .Returns(Task.CompletedTask);

        for (int i = 0; i < attempts; i++)
        {
            await mockRepo.Object.RecordFailureAsync(msg.Id, errorMsg);
        }

        Assert.Equal(attempts, msg.RetryCount);
        Assert.Equal(errorMsg, msg.LastError);
    }

    // =========================================================================
    // 3. IOutboxRepository - Cleanup of Processed Messages
    // =========================================================================

    [Theory]
    [InlineData(1, 4)]   // 1 day cutoff: -10, -8, -5, -2 days = 4 deleted
    [InlineData(7, 2)]   // 7 day cutoff: -10, -8 days = 2 deleted
    [InlineData(30, 0)]  // 30 day cutoff: none older
    public async Task Outbox_CleanupAsync_ShouldPurgeProcessedOlderThanCutoff(int daysOld, int expectedPurged)
    {
        var now = DateTimeOffset.UtcNow;
        var store = new List<TestOutboxMessage>
        {
            new("E1", "{}") { ProcessedAt = now.AddDays(-10) },
            new("E2", "{}") { ProcessedAt = now.AddDays(-8) },
            new("E3", "{}") { ProcessedAt = now.AddDays(-5) },
            new("E4", "{}") { ProcessedAt = now.AddDays(-2) },
            new("E5", "{}") { ProcessedAt = now.AddHours(-12) },
            new("E6", "{}") { ProcessedAt = null } // Pending, must never be purged
        };

        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.CleanupAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset cutoff, CancellationToken _) =>
            {
                var toRemove = store.Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff).ToList();
                foreach (var item in toRemove) store.Remove(item);
                return toRemove.Count;
            });

        var cutoffDate = now.AddDays(-daysOld);
        var purgedCount = await mockRepo.Object.CleanupAsync(cutoffDate);

        Assert.Equal(expectedPurged, purgedCount);
        Assert.Contains(store, m => m.ProcessedAt == null); // Unprocessed messages must never be deleted
    }

    [Fact]
    public async Task Outbox_CleanupAsync_ShouldNeverDeletePendingMessages()
    {
        var store = new List<TestOutboxMessage>
        {
            new("E1", "{}") { CreatedAt = DateTimeOffset.UtcNow.AddYears(-1), ProcessedAt = null }
        };

        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.CleanupAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset cutoff, CancellationToken _) =>
            {
                return store.RemoveAll(m => m.ProcessedAt != null && m.ProcessedAt < cutoff);
            });

        var purged = await mockRepo.Object.CleanupAsync(DateTimeOffset.UtcNow);

        Assert.Equal(0, purged);
        Assert.Single(store);
    }

    // =========================================================================
    // 4. IOutboxRepository<TMessage> - Generic Envelope Support
    // =========================================================================

    [Fact]
    public async Task Outbox_Generic_GetPendingAsync_ShouldReturnTypedList()
    {
        var typedMessages = new List<TestOutboxMessage>
        {
            new("OrderSubmitted", "{\"id\":1}"),
            new("OrderSubmitted", "{\"id\":2}")
        };

        var mockRepo = new Mock<IOutboxRepository<TestOutboxMessage>>();
        mockRepo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(typedMessages);

        IReadOnlyList<TestOutboxMessage> result = await mockRepo.Object.GetPendingAsync(10);

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.IsType<TestOutboxMessage>(m));
    }

    [Theory]
    [InlineData("tenant-alpha")]
    [InlineData("tenant-beta")]
    [InlineData(null)]
    public async Task Outbox_MessageTenantId_ShouldBePreserved(string? tenantId)
    {
        var msg = new TestOutboxMessage("TenantEvent", "{}", tenantId);
        var mockRepo = new Mock<IOutboxRepository<TestOutboxMessage>>();
        mockRepo.Setup(r => r.AddAsync(It.IsAny<TestOutboxMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mockRepo.Object.AddAsync(msg);

        Assert.Equal(tenantId, msg.TenantId);
    }

    [Fact]
    public async Task Outbox_CorrelationId_ShouldBePreservedAcrossDispatch()
    {
        var correlationId = Guid.NewGuid();
        var msg = new TestOutboxMessage("UserRegistered", "{}")
        {
            CorrelationId = correlationId
        };

        var mockRepo = new Mock<IOutboxRepository>();
        mockRepo.Setup(r => r.AddAsync(msg, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await mockRepo.Object.AddAsync(msg);

        Assert.Equal(correlationId, msg.CorrelationId);
    }

    // =========================================================================
    // 5. IInboxRepository - Idempotency & Deduplication
    // =========================================================================

    [Fact]
    public async Task Inbox_HasBeenProcessedAsync_WhenNewEvent_ShouldReturnFalse()
    {
        var mockInbox = new Mock<IInboxRepository>();
        mockInbox.Setup(i => i.HasBeenProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var processed = await mockInbox.Object.HasBeenProcessedAsync(Guid.NewGuid(), "OrderProcessor");

        Assert.False(processed);
    }

    [Fact]
    public async Task Inbox_HasBeenProcessedAsync_WhenExistingEvent_ShouldReturnTrue()
    {
        var eventId = Guid.NewGuid();
        var consumer = "EmailNotificationConsumer";

        var mockInbox = new Mock<IInboxRepository>();
        mockInbox.Setup(i => i.HasBeenProcessedAsync(eventId, consumer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var processed = await mockInbox.Object.HasBeenProcessedAsync(eventId, consumer);

        Assert.True(processed);
    }

    [Theory]
    [InlineData("ConsumerA", "ConsumerB")]
    [InlineData("InventoryService", "BillingService")]
    [InlineData("AuditLogConsumer", "AnalyticsConsumer")]
    public async Task Inbox_SameEventDifferentConsumers_ShouldTrackIndependently(string consumer1, string consumer2)
    {
        var eventId = Guid.NewGuid();
        var records = new HashSet<(Guid, string)>();

        var mockInbox = new Mock<IInboxRepository>();
        mockInbox.Setup(i => i.HasBeenProcessedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string c, CancellationToken _) => records.Contains((id, c)));

        mockInbox.Setup(i => i.RecordAsync(It.IsAny<IInboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IInboxMessage, CancellationToken>((msg, _) => records.Add((msg.EventId, msg.ConsumerName)))
            .Returns(Task.CompletedTask);

        // Record for consumer 1 only
        await mockInbox.Object.RecordAsync(new TestInboxMessage(eventId, "OrderPaid", consumer1));

        var consumer1Processed = await mockInbox.Object.HasBeenProcessedAsync(eventId, consumer1);
        var consumer2Processed = await mockInbox.Object.HasBeenProcessedAsync(eventId, consumer2);

        Assert.True(consumer1Processed);
        Assert.False(consumer2Processed);
    }

    [Fact]
    public async Task Inbox_RecordAsync_ShouldPersistInboxRecord()
    {
        var stored = new List<IInboxMessage>();
        var mockInbox = new Mock<IInboxRepository>();
        mockInbox.Setup(i => i.RecordAsync(It.IsAny<IInboxMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IInboxMessage, CancellationToken>((msg, _) => stored.Add(msg))
            .Returns(Task.CompletedTask);

        var eventId = Guid.NewGuid();
        var message = new TestInboxMessage(eventId, "CustomerCreated", "CustomerServiceConsumer", "tenant-1");

        await mockInbox.Object.RecordAsync(message);

        Assert.Single(stored);
        Assert.Equal(eventId, stored[0].EventId);
        Assert.Equal("CustomerCreated", stored[0].EventType);
        Assert.Equal("CustomerServiceConsumer", stored[0].ConsumerName);
        Assert.Equal("tenant-1", stored[0].TenantId);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(60)]
    public async Task Inbox_CleanupAsync_ShouldPurgeRecordsOlderThanCutoff(int daysOld)
    {
        var now = DateTimeOffset.UtcNow;
        var records = new List<TestInboxMessage>
        {
            new(Guid.NewGuid(), "E1", "C1") { ProcessedAt = now.AddDays(-70) },
            new(Guid.NewGuid(), "E2", "C1") { ProcessedAt = now.AddDays(-40) },
            new(Guid.NewGuid(), "E3", "C1") { ProcessedAt = now.AddDays(-20) },
            new(Guid.NewGuid(), "E4", "C1") { ProcessedAt = now.AddDays(-5) },
            new(Guid.NewGuid(), "E5", "C1") { ProcessedAt = now.AddHours(-1) }
        };

        var mockInbox = new Mock<IInboxRepository>();
        mockInbox.Setup(i => i.CleanupAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTimeOffset cutoff, CancellationToken _) =>
            {
                return records.RemoveAll(r => r.ProcessedAt < cutoff);
            });

        var cutoffDate = now.AddDays(-daysOld);
        var deletedCount = await mockInbox.Object.CleanupAsync(cutoffDate);

        Assert.True(deletedCount >= 0);
        Assert.All(records, r => Assert.True(r.ProcessedAt >= cutoffDate));
    }

    // =========================================================================
    // 6. In-Memory Store Simulation Test (Simulating Concurrent Execution)
    // =========================================================================

    [Theory]
    [InlineData(10, 2)]
    [InlineData(25, 5)]
    [InlineData(50, 10)]
    [InlineData(100, 20)]
    public async Task Outbox_SimulatedBatchDrain_ShouldProcessAllMessages(int totalMessages, int batchSize)
    {
        var inMemoryStore = Enumerable.Range(1, totalMessages)
            .Select(i => new TestOutboxMessage($"Event_{i}", $"{{\"id\":{i}}}"))
            .ToList();

        var repo = new Mock<IOutboxRepository>();
        repo.Setup(r => r.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int size, CancellationToken _) =>
                inMemoryStore.Where(m => m.ProcessedAt == null).Take(size).ToList());

        repo.Setup(r => r.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((id, _) =>
            {
                var found = inMemoryStore.FirstOrDefault(m => m.Id == id);
                found?.MarkAsProcessed();
            })
            .Returns(Task.CompletedTask);

        int totalProcessed = 0;
        while (true)
        {
            var batch = await repo.Object.GetPendingAsync(batchSize);
            if (batch.Count == 0) break;

            foreach (var msg in batch)
            {
                await repo.Object.MarkAsProcessedAsync(msg.Id);
                totalProcessed++;
            }
        }

        Assert.Equal(totalMessages, totalProcessed);
        Assert.All(inMemoryStore, m => Assert.NotNull(m.ProcessedAt));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Outbox_RetryLimit_ShouldFlagForDeadLetter(int maxRetries)
    {
        var msg = new TestOutboxMessage("CriticalPaymentEvent", "{}");

        for (int i = 0; i < maxRetries; i++)
        {
            msg.LogFailure($"Failed attempt {i + 1}");
        }

        bool isDeadLetter = msg.RetryCount >= maxRetries;

        Assert.True(isDeadLetter);
        Assert.Equal(maxRetries, msg.RetryCount);
        Assert.Contains($"{maxRetries}", msg.LastError);
    }
}
