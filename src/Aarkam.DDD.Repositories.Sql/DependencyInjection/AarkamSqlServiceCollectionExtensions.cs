using Aarkam.DDD.Repositories.Sql.Messaging;
using Aarkam.DDD.Repositories.Sql.Repositories;
using Aarkam.DDD.Repositories.Sql.UnitOfWork;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aarkam.DDD.Repositories.Sql.DependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to seamlessly register EF Core repository and unit of work implementations.
/// </summary>
public static class AarkamSqlServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Aarkam SQL Repository implementations, Unit of Work, Outbox, Inbox, Saga, and Snapshot services for the specified <typeparamref name="TDbContext"/>.
    /// </summary>
    /// <typeparam name="TDbContext">The application DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAarkamSqlRepositories<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
    {
        if (services == null) throw new ArgumentNullException(nameof(services));

        // Ensure DbContext resolves as base DbContext
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

        // 1. Lean Unit of Work
        services.AddScoped<EfCoreUnitOfWork<TDbContext>>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EfCoreUnitOfWork<TDbContext>>());
        services.AddScoped<IUnitOfWork<TDbContext>>(sp => sp.GetRequiredService<EfCoreUnitOfWork<TDbContext>>());

        // 2. Open Generic Repositories (1-Generic)
        services.AddScoped(typeof(IReadOnlyRepository<>), typeof(EfCoreRepository<>));
        services.AddScoped(typeof(IBasicRepository<>), typeof(EfCoreRepository<>));
        services.AddScoped(typeof(IRepository<>), typeof(EfCoreRepository<>));
        services.AddScoped(typeof(IAggregateRepository<>), typeof(EfCoreAggregateRepository<>));

        // 3. Open Generic Repositories (2-Generic, Keyed)
        services.AddScoped(typeof(IReadOnlyRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IBasicRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IAggregateRepository<,>), typeof(EfCoreAggregateRepository<,>));

        // 4. Context-Bound Repositories (Direct resolution for multiple DbContext architectures)
        services.AddScoped(typeof(EfCoreContextRepository<,>));
        services.AddScoped(typeof(EfCoreRepository<,,>));
        services.AddScoped(typeof(EfCoreContextAggregateRepository<,>));
        services.AddScoped(typeof(EfCoreAggregateRepository<,,>));

        // 5. Distributed Messaging & Resilience Repositories
        services.AddScoped<IOutboxRepository, EfCoreOutboxRepository>();
        services.AddScoped(typeof(IOutboxRepository<>), typeof(EfCoreOutboxRepository<>));
        services.AddScoped(typeof(EfCoreOutboxRepository<,>));

        services.AddScoped<IInboxRepository, EfCoreInboxRepository>();
        services.AddScoped<EfCoreInboxRepository<TDbContext>>();

        services.AddScoped<ISagaRepository, EfCoreSagaRepository>();
        services.AddScoped<EfCoreSagaRepository<TDbContext>>();

        services.AddScoped<ISnapshotRepository, EfCoreSnapshotRepository>();
        services.AddScoped(typeof(ISnapshotRepository<>), typeof(EfCoreSnapshotRepository<>));
        services.AddScoped(typeof(EfCoreSnapshotRepository<,>));

        return services;
    }
}
