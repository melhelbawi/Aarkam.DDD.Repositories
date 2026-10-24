using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.DependencyInjection;
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
    }

    [Fact]
    public void Resolve_AggregateRepository_ShouldReturnEfCoreAggregateRepository()
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetService<IAggregateRepository<TestOrderAggregate, Guid>>();

        Assert.NotNull(repo);
        Assert.IsType<EfCoreAggregateRepository<TestOrderAggregate, Guid>>(repo);
    }

    [Fact]
    public void Resolve_EntityRepositories_ShouldReturnEfCoreRepository()
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
    public void Resolve_MessagingRepositories_ShouldReturnConcreteEfCoreImplementations()
    {
        using var scope = _serviceProvider.CreateScope();

        var outbox = scope.ServiceProvider.GetService<IOutboxRepository>();
        Assert.NotNull(outbox);
        Assert.IsType<EfCoreOutboxRepository>(outbox);

        var inbox = scope.ServiceProvider.GetService<IInboxRepository>();
        Assert.NotNull(inbox);
        Assert.IsType<EfCoreInboxRepository>(inbox);

        var saga = scope.ServiceProvider.GetService<ISagaRepository>();
        Assert.NotNull(saga);
        Assert.IsType<EfCoreSagaRepository>(saga);

        var snapshot = scope.ServiceProvider.GetService<ISnapshotRepository>();
        Assert.NotNull(snapshot);
        Assert.IsType<EfCoreSnapshotRepository>(snapshot);

        var typedSnapshot = scope.ServiceProvider.GetService<ISnapshotRepository<TestOrderAggregate>>();
        Assert.NotNull(typedSnapshot);
        Assert.IsType<EfCoreSnapshotRepository<TestOrderAggregate>>(typedSnapshot);
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
