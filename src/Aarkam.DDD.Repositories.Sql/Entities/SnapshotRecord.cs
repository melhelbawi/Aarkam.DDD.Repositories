using Aarkam.DDD.Domain;

namespace Aarkam.DDD.Repositories.Sql.Entities;

/// <summary>
/// Persistent database entity record implementing <see cref="ISnapshot"/> for Event Sourcing snapshot storage.
/// </summary>
public class SnapshotRecord : ISnapshot
{
    public string AggregateId { get; set; } = string.Empty;
    public string AggregateType { get; set; } = string.Empty;
    public long Version { get; set; }
    public string State { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }

    public SnapshotRecord() { }

    public SnapshotRecord(string aggregateId, string aggregateType, long version, string state, string? tenantId = null)
    {
        AggregateId = aggregateId;
        AggregateType = aggregateType;
        Version = version;
        State = state;
        TenantId = tenantId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
