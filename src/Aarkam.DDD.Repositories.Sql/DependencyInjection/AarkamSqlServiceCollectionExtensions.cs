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
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<TDbContext>>();
        services.AddScoped<EfCoreUnitOfWork<TDbContext>>();

        // 2. Open Generic Repositories
        services.AddScoped(typeof(IAggregateRepository<,>), typeof(EfCoreAggregateRepository<,>));
        services.AddScoped(typeof(IRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IBasicRepository<,>), typeof(EfCoreRepository<,>));
        services.AddScoped(typeof(IReadOnlyRepository<,>), typeof(EfCoreRepository<,>));

        // 3. Distributed Messaging & Resilience Repositories
        services.AddScoped<IOutboxRepository, EfCoreOutboxRepository>();
        services.AddScoped(typeof(IOutboxRepository<>), typeof(EfCoreOutboxRepository<>));

        services.AddScoped<IInboxRepository, EfCoreInboxRepository>();
        services.AddScoped<ISagaRepository, EfCoreSagaRepository>();

        services.AddScoped<ISnapshotRepository, EfCoreSnapshotRepository>();
        services.AddScoped(typeof(ISnapshotRepository<>), typeof(EfCoreSnapshotRepository<>));

        return services;
    }
}
