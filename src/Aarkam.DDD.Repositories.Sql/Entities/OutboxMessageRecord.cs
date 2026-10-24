using Aarkam.DDD.Domain;

namespace Aarkam.DDD.Repositories.Sql.Entities;

/// <summary>
/// Persistent database entity record implementing <see cref="IOutboxMessage"/> for the Transactional Outbox pattern.
/// </summary>
public class OutboxMessageRecord : IOutboxMessage
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

    public OutboxMessageRecord() { }

    public OutboxMessageRecord(string eventType, string payload, string? tenantId = null, Guid? correlationId = null)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        Payload = payload;
        TenantId = tenantId;
        CreatedAt = DateTimeOffset.UtcNow;
        CorrelationId = correlationId ?? Guid.NewGuid();
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
