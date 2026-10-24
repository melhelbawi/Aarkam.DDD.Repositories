using Aarkam.DDD.Repositories.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aarkam.DDD.Repositories.Sql.Configurations;

/// <summary>
/// Provides extension methods for EF Core's <see cref="ModelBuilder"/> to automatically register optimized entity schemas
/// for Outbox, Inbox, Saga, and Snapshot persistence tables.
/// </summary>
public static class AarkamSqlModelBuilderExtensions
{
    /// <summary>
    /// Configures standard database tables and high-performance indexes for Outbox, Inbox, Saga, and Snapshot patterns.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder instance.</param>
    /// <param name="schema">Optional database schema name (defaults to null / default schema).</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyAarkamSqlMessagingConfigurations(this ModelBuilder modelBuilder, string? schema = null)
    {
        if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

        // 1. Outbox Messages Configuration
        modelBuilder.Entity<OutboxMessageRecord>(entity =>
        {
            if (!string.IsNullOrWhiteSpace(schema))
                entity.ToTable("Aarkam_OutboxMessages", schema);
            else
                entity.ToTable("Aarkam_OutboxMessages");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(128);
            entity.Property(e => e.LastError).HasMaxLength(4000);

            // Polling query speed: Filter on uncompleted messages ordered by CreatedAt
            entity.HasIndex(e => new { e.ProcessedAt, e.CreatedAt });
            entity.HasIndex(e => e.CorrelationId);
        });

        // 2. Inbox Messages Configuration
        modelBuilder.Entity<InboxMessageRecord>(entity =>
        {
            if (!string.IsNullOrWhiteSpace(schema))
                entity.ToTable("Aarkam_InboxMessages", schema);
            else
                entity.ToTable("Aarkam_InboxMessages");

            // Composite primary key ensures independent consumer idempotency boundaries
            entity.HasKey(e => new { e.EventId, e.ConsumerName });
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ConsumerName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.TenantId).HasMaxLength(128);

            // Cleanup index
            entity.HasIndex(e => e.ProcessedAt);
        });

        // 3. Saga State Configuration
        modelBuilder.Entity<SagaStateRecord>(entity =>
        {
            if (!string.IsNullOrWhiteSpace(schema))
                entity.ToTable("Aarkam_Sagas", schema);
            else
                entity.ToTable("Aarkam_Sagas");

            entity.HasKey(e => e.SagaId);
            entity.Property(e => e.SagaType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DataJson).IsRequired();
            entity.Property(e => e.Version).IsConcurrencyToken();

            // Timeout queries: Filter on active state and started timestamp
            entity.HasIndex(e => new { e.Status, e.StartedAt });
        });

        // 4. Snapshots Configuration
        modelBuilder.Entity<SnapshotRecord>(entity =>
        {
            if (!string.IsNullOrWhiteSpace(schema))
                entity.ToTable("Aarkam_Snapshots", schema);
            else
                entity.ToTable("Aarkam_Snapshots");

            // Composite primary key captures aggregate history checkpoints
            entity.HasKey(e => new { e.AggregateId, e.Version });
            entity.Property(e => e.AggregateId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.AggregateType).IsRequired().HasMaxLength(256);
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.TenantId).HasMaxLength(128);

            // Fast rehydration: Find highest version for an aggregate
            entity.HasIndex(e => new { e.AggregateId, e.Version });
        });

        return modelBuilder;
    }
}
