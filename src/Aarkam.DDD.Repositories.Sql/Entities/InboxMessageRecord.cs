using Aarkam.DDD.Domain;

namespace Aarkam.DDD.Repositories.Sql.Entities;

/// <summary>
/// Persistent database entity record implementing <see cref="IInboxMessage"/> for idempotent consumer deduplication.
/// </summary>
public class InboxMessageRecord : IInboxMessage
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ConsumerName { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }

    public InboxMessageRecord() { }

    public InboxMessageRecord(Guid eventId, string eventType, string consumerName, string? tenantId = null)
    {
        EventId = eventId;
        EventType = eventType;
        ConsumerName = consumerName;
        TenantId = tenantId;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}
