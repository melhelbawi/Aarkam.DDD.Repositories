using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using System.Linq.Expressions;

namespace Aarkam.DDD.Repositories.Tests.Fakes;

public class TestCustomerAggregate : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public bool IsActive { get; private set; } = true;

    public TestCustomerAggregate() { }

    public TestCustomerAggregate(Guid id, string name, string email, decimal balance = 0)
    {
        Id = id;
        Name = name;
        Email = email;
        Balance = balance;
    }

    public void UpdateName(string newName)
    {
        Name = newName;
    }

    public void Deposit(decimal amount)
    {
        Balance += amount;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public override string GetAggregateType() => nameof(TestCustomerAggregate);
}

public class TestProductEntity : Entity<int>
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public bool IsActive { get; set; } = true;
    public double Rating { get; set; }
    public double? NullableScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? ParentId { get; set; }

    public TestProductEntity() { }

    public TestProductEntity(int id, string name, string category, decimal price, int stock, bool isActive = true)
    {
        Id = id;
        Name = name;
        Category = category;
        Price = price;
        Stock = stock;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        CorrelationId = Guid.NewGuid();
    }
}

public class TestDocumentEntity : Entity<Guid>
{
    public string PartitionKey { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string ETag { get; set; } = string.Empty;
    public int ViewCount { get; set; }

    public TestDocumentEntity() { }

    public TestDocumentEntity(Guid id, string partitionKey, string payload)
    {
        Id = id;
        PartitionKey = partitionKey;
        Payload = payload;
        ETag = Guid.NewGuid().ToString("N");
    }
}

public class TestSpecification<T> : ISpecification<T>
{
    private readonly Expression<Func<T, bool>> _criteria;
    public IReadOnlyList<Expression<Func<T, object>>> Includes { get; } = [];
    public Expression<Func<T, object>>? OrderBy { get; }
    public Expression<Func<T, object>>? OrderByDescending { get; }
    public int? Take { get; }
    public int? Skip { get; }

    public TestSpecification(Expression<Func<T, bool>> criteria)
    {
        _criteria = criteria;
    }

    public Expression<Func<T, bool>> ToExpression() => _criteria;

    public bool IsSatisfiedBy(T entity) => _criteria.Compile()(entity);
}

public class TestOutboxMessage : IOutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public string? TenantId { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();

    public TestOutboxMessage() { }

    public TestOutboxMessage(string eventType, string payload, string? tenantId = null)
    {
        EventType = eventType;
        Payload = payload;
        TenantId = tenantId;
    }

    public void MarkAsProcessed()
    {
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void LogFailure(string errorMessage)
    {
        RetryCount++;
        LastError = errorMessage;
    }
}

public class TestInboxMessage : IInboxMessage
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public string ConsumerName { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }

    public TestInboxMessage() { }

    public TestInboxMessage(Guid eventId, string eventType, string consumerName, string? tenantId = null)
    {
        EventId = eventId;
        EventType = eventType;
        ConsumerName = consumerName;
        TenantId = tenantId;
    }
}

public class TestSagaData
{
    public string OrderId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool InventoryReserved { get; set; }
    public bool PaymentProcessed { get; set; }
}

public class TestOrderSaga : ISaga<TestSagaData>
{
    public Guid SagaId { get; set; } = Guid.NewGuid();
    public TestSagaData Data { get; set; } = new();
    public SagaStatus Status { get; set; } = SagaStatus.Running;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long Version { get; set; } = 1;

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

public class TestSnapshot : ISnapshot
{
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public long Version { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }

    public TestSnapshot() { }

    public TestSnapshot(string aggregateId, string aggregateType, long version, string state, string? tenantId = null)
    {
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        Version = version;
        State = state;
        TenantId = tenantId;
    }
}
