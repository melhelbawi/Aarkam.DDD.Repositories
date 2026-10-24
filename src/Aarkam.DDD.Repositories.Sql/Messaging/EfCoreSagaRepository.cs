using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace Aarkam.DDD.Repositories.Sql.Messaging;

/// <summary>
/// Entity Framework Core implementation of <see cref="ISagaRepository"/> for persisting distributed Saga state.
/// </summary>
public class EfCoreSagaRepository : ISagaRepository
{
    protected readonly DbContext _dbContext;
    protected readonly DbSet<SagaStateRecord> _dbSet;
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EfCoreSagaRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _dbSet = dbContext.Set<SagaStateRecord>();
    }

    /// <inheritdoc />
    public virtual async Task<TSaga?> LoadAsync<TSaga, TSagaData>(Guid sagaId, CancellationToken cancellationToken = default)
        where TSaga : class, ISaga<TSagaData>
        where TSagaData : class, new()
    {
        var record = await _dbSet.FindAsync(new object?[] { sagaId }, cancellationToken);
        if (record == null)
            return null;

        var sagaData = JsonSerializer.Deserialize<TSagaData>(record.DataJson, JsonOptions) ?? new TSagaData();

        // Instantiate the saga and populate its state via reflection
        var saga = (TSaga)Activator.CreateInstance(typeof(TSaga), nonPublic: true)!;

        SetProperty(saga, nameof(ISaga<TSagaData>.SagaId), record.SagaId);
        SetProperty(saga, nameof(ISaga<TSagaData>.Data), sagaData);
        SetProperty(saga, nameof(ISaga<TSagaData>.Status), record.Status);
        SetProperty(saga, nameof(ISaga<TSagaData>.StartedAt), record.StartedAt);
        SetProperty(saga, nameof(ISaga<TSagaData>.CompletedAt), record.CompletedAt);
        SetProperty(saga, nameof(ISaga<TSagaData>.Version), record.Version);

        return saga;
    }

    /// <inheritdoc />
    public virtual async Task SaveAsync<TSaga, TSagaData>(TSaga saga, CancellationToken cancellationToken = default)
        where TSaga : class, ISaga<TSagaData>
        where TSagaData : class, new()
    {
        if (saga == null) throw new ArgumentNullException(nameof(saga));

        var record = await _dbSet.FindAsync(new object?[] { saga.SagaId }, cancellationToken);
        var dataJson = JsonSerializer.Serialize(saga.Data, JsonOptions);

        if (record == null)
        {
            // Create new record
            var newRecord = new SagaStateRecord
            {
                SagaId = saga.SagaId,
                SagaType = typeof(TSaga).FullName ?? typeof(TSaga).Name,
                DataJson = dataJson,
                Status = saga.Status,
                StartedAt = saga.StartedAt,
                CompletedAt = saga.CompletedAt,
                Version = saga.Version
            };
            await _dbSet.AddAsync(newRecord, cancellationToken);
        }
        else
        {
            // Optimistic concurrency verification
            if (record.Version != saga.Version)
            {
                throw new DbUpdateConcurrencyException(
                    $"Concurrency violation for Saga ID '{saga.SagaId}'. Expected version {record.Version}, but active instance has version {saga.Version}.");
            }

            record.DataJson = dataJson;
            record.Status = saga.Status;
            record.CompletedAt = saga.CompletedAt;
            record.Version++;

            _dbSet.Update(record);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<Guid>> FindTimedOutSagasAsync(
        TimeSpan timeout,
        SagaStatus status = SagaStatus.Running,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - timeout;

        return await _dbSet
            .Where(s => s.Status == status && s.StartedAt <= cutoff)
            .Select(s => s.SagaId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<int> CleanupAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        var terminalStatuses = new[] { SagaStatus.Completed, SagaStatus.Failed, SagaStatus.TimedOut };

        var staleSagas = await _dbSet
            .Where(s => terminalStatuses.Contains(s.Status) && s.CompletedAt != null && s.CompletedAt < olderThan)
            .ToListAsync(cancellationToken);

        if (staleSagas.Count == 0)
            return 0;

        _dbSet.RemoveRange(staleSagas);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return staleSagas.Count;
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(target, value);
        }
    }
}
