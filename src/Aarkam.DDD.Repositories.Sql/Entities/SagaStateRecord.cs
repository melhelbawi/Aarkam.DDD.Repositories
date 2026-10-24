using Aarkam.DDD.Domain;

namespace Aarkam.DDD.Repositories.Sql.Entities;

/// <summary>
/// Persistent database entity record for persisting long-running Saga (Process Manager) state documents.
/// </summary>
public class SagaStateRecord
{
    public Guid SagaId { get; set; }
    public string SagaType { get; set; } = string.Empty;
    public string DataJson { get; set; } = string.Empty;
    public SagaStatus Status { get; set; } = SagaStatus.Pending;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long Version { get; set; } = 1;
    public string? LastError { get; set; }

    public SagaStateRecord() { }

    public SagaStateRecord(Guid sagaId, string sagaType, string dataJson, SagaStatus status, DateTimeOffset startedAt, long version = 1)
    {
        SagaId = sagaId;
        SagaType = sagaType;
        DataJson = dataJson;
        Status = status;
        StartedAt = startedAt;
        Version = version;
    }
}
