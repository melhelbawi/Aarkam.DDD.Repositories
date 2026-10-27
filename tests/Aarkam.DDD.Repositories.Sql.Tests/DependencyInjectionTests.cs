using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.DependencyInjection;
using Aarkam.DDD.Repositories.Sql.Entities;
using Aarkam.DDD.Repositories.Sql.Messaging;
using Aarkam.DDD.Repositories.Sql.Repositories;
using Aarkam.DDD.Repositories.Sql.Tests.Fakes;
using Aarkam.DDD.Repositories.Sql.UnitOfWork;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aarkam.DDD.Repositories.Sql.Tests;

public class DependencyInjectionTests
{
    private readonly IServiceProvider _serviceProvider;

    public DependencyInjectionTests()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestSqlDbContext>(options =>
            options.UseInMemoryDatabase($"EfDiTest_{Guid.NewGuid():N}"));

        services.AddAarkamSqlRepositories<TestSqlDbContext>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void AddAarkamSqlRepositories_WithNullServices_ShouldThrowArgumentNullException()
    {
        IServiceCollection nullServices = null!;
        Assert.Throws<ArgumentNullException>(() => nullServices.AddAarkamSqlRepositories<TestSqlDbContext>());
    }

    [Fact]
    public void Resolve_UnitOfWork_ShouldReturnEfCoreUnitOfWork()
    {
        using var scope = _serviceProvider.CreateScope();
        var uow = scope.ServiceProvider.GetService<IUnitOfWork>();

        Assert.NotNull(uow);
        Assert.IsType<EfCoreUnitOfWork<TestSqlDbContext>>(uow);

        var typedUow = scope.ServiceProvider.GetService<IUnitOfWork<TestSqlDbContext>>();
        Assert.NotNull(typedUow);
        Assert.Same(uow, typedUow);
    }

    [Fact]
    public void Resolve_AggregateRepositories_1GenericAnd2Generic_ShouldResolveSuccessfully()
    {
        using var scope = _serviceProvider.CreateScope();

        // 1-Generic
        var singleAggRepo = scope.ServiceProvider.GetService<IAggregateRepository<TestOrderAggregate>>();
        Assert.NotNull(singleAggRepo);
        Assert.IsType<EfCoreAggregateRepository<TestOrderAggregate>>(singleAggRepo);

        // 2-Generic
        var keyedAggRepo = scope.ServiceProvider.GetService<IAggregateRepository<TestOrderAggregate, Guid>>();
        Assert.NotNull(keyedAggRepo);
        Assert.IsType<EfCoreAggregateRepository<TestOrderAggregate, Guid>>(keyedAggRepo);
    }

    [Fact]
    public void Resolve_EntityRepositories_1Generic_ShouldResolveSuccessfully()
    {
        using var scope = _serviceProvider.CreateScope();

        var readOnlySingle = scope.ServiceProvider.GetService<IReadOnlyRepository<TestItemEntity>>();
        Assert.NotNull(readOnlySingle);
        Assert.IsType<EfCoreRepository<TestItemEntity>>(readOnlySingle);

        var basicSingle = scope.ServiceProvider.GetService<IBasicRepository<TestItemEntity>>();
        Assert.NotNull(basicSingle);
        Assert.IsType<EfCoreRepository<TestItemEntity>>(basicSingle);

        var repoSingle = scope.ServiceProvider.GetService<IRepository<TestItemEntity>>();
        Assert.NotNull(repoSingle);
        Assert.IsType<EfCoreRepository<TestItemEntity>>(repoSingle);
    }

    [Fact]
    public void Resolve_EntityRepositories_2GenericKeyed_ShouldResolveSuccessfully()
    {
        using var scope = _serviceProvider.CreateScope();

        var repo = scope.ServiceProvider.GetService<IRepository<TestItemEntity, int>>();
        Assert.NotNull(repo);
        Assert.IsType<EfCoreRepository<TestItemEntity, int>>(repo);

        var basicRepo = scope.ServiceProvider.GetService<IBasicRepository<TestItemEntity, int>>();
        Assert.NotNull(basicRepo);
        Assert.IsType<EfCoreRepository<TestItemEntity, int>>(basicRepo);

        var readOnlyRepo = scope.ServiceProvider.GetService<IReadOnlyRepository<TestItemEntity, int>>();
        Assert.NotNull(readOnlyRepo);
        Assert.IsType<EfCoreRepository<TestItemEntity, int>>(readOnlyRepo);
    }

    [Fact]
    public void Resolve_ContextBoundRepositories_ShouldResolveSuccessfully()
    {
        using var scope = _serviceProvider.CreateScope();

        var contextRepo = scope.ServiceProvider.GetService<EfCoreContextRepository<TestItemEntity, TestSqlDbContext>>();
        Assert.NotNull(contextRepo);
        Assert.NotNull(contextRepo.Context);

        var keyedContextRepo = scope.ServiceProvider.GetService<EfCoreRepository<TestItemEntity, int, TestSqlDbContext>>();
        Assert.NotNull(keyedContextRepo);
        Assert.NotNull(keyedContextRepo.Context);

        var contextAggRepo = scope.ServiceProvider.GetService<EfCoreContextAggregateRepository<TestOrderAggregate, TestSqlDbContext>>();
        Assert.NotNull(contextAggRepo);
        Assert.NotNull(contextAggRepo.Context);

        var keyedContextAggRepo = scope.ServiceProvider.GetService<EfCoreAggregateRepository<TestOrderAggregate, Guid, TestSqlDbContext>>();
        Assert.NotNull(keyedContextAggRepo);
        Assert.NotNull(keyedContextAggRepo.Context);
    }

    [Fact]
    public void Resolve_MessagingRepositories_ShouldReturnConcreteEfCoreImplementations()
    {
        using var scope = _serviceProvider.CreateScope();

        var outbox = scope.ServiceProvider.GetService<IOutboxRepository>();
        Assert.NotNull(outbox);
        Assert.IsType<EfCoreOutboxRepository>(outbox);

        var typedOutbox = scope.ServiceProvider.GetService<IOutboxRepository<OutboxMessageRecord>>();
        Assert.NotNull(typedOutbox);
        Assert.IsType<EfCoreOutboxRepository<OutboxMessageRecord>>(typedOutbox);

        var contextOutbox = scope.ServiceProvider.GetService<EfCoreOutboxRepository<OutboxMessageRecord, TestSqlDbContext>>();
        Assert.NotNull(contextOutbox);
        Assert.NotNull(contextOutbox.Context);

        var inbox = scope.ServiceProvider.GetService<IInboxRepository>();
        Assert.NotNull(inbox);
        Assert.IsType<EfCoreInboxRepository>(inbox);

        var contextInbox = scope.ServiceProvider.GetService<EfCoreInboxRepository<TestSqlDbContext>>();
        Assert.NotNull(contextInbox);
        Assert.NotNull(contextInbox.Context);

        var saga = scope.ServiceProvider.GetService<ISagaRepository>();
        Assert.NotNull(saga);
        Assert.IsType<EfCoreSagaRepository>(saga);

        var contextSaga = scope.ServiceProvider.GetService<EfCoreSagaRepository<TestSqlDbContext>>();
        Assert.NotNull(contextSaga);
        Assert.NotNull(contextSaga.Context);

        var snapshot = scope.ServiceProvider.GetService<ISnapshotRepository>();
        Assert.NotNull(snapshot);
        Assert.IsType<EfCoreSnapshotRepository>(snapshot);

        var typedSnapshot = scope.ServiceProvider.GetService<ISnapshotRepository<TestOrderAggregate>>();
        Assert.NotNull(typedSnapshot);
        Assert.IsType<EfCoreSnapshotRepository<TestOrderAggregate>>(typedSnapshot);

        var contextSnapshot = scope.ServiceProvider.GetService<EfCoreSnapshotRepository<TestOrderAggregate, TestSqlDbContext>>();
        Assert.NotNull(contextSnapshot);
        Assert.NotNull(contextSnapshot.Context);
    }

    [Fact]
    public void ScopedLifetime_WithinSameScope_ShouldReturnSameInstances()
    {
        using var scope = _serviceProvider.CreateScope();

        var uow1 = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var uow2 = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        Assert.Same(uow1, uow2);
    }

    [Fact]
    public void ScopedLifetime_AcrossDifferentScopes_ShouldReturnDifferentInstances()
    {
        IUnitOfWork uow1;
        using (var scope1 = _serviceProvider.CreateScope())
        {
            uow1 = scope1.ServiceProvider.GetRequiredService<IUnitOfWork>();
        }

        IUnitOfWork uow2;
        using (var scope2 = _serviceProvider.CreateScope())
        {
            uow2 = scope2.ServiceProvider.GetRequiredService<IUnitOfWork>();
        }

        Assert.NotSame(uow1, uow2);
    }
}
