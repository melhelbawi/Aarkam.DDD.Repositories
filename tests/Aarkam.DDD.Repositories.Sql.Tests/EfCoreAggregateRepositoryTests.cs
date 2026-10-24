using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Repositories;
using Aarkam.DDD.Repositories.Sql.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarkam.DDD.Repositories.Sql.Tests;

public class EfCoreAggregateRepositoryTests : IDisposable
{
    private readonly TestSqlDbContext _dbContext;
    private readonly EfCoreAggregateRepository<TestOrderAggregate, Guid> _repository;
    private readonly EfCoreAggregateRepository<TestOrderAggregate, Guid, TestSqlDbContext> _typedRepository;

    public EfCoreAggregateRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestSqlDbContext>()
            .UseInMemoryDatabase(databaseName: $"EfAggregateRepoTest_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TestSqlDbContext(options);
        _repository = new EfCoreAggregateRepository<TestOrderAggregate, Guid>(_dbContext);
        _typedRepository = new EfCoreAggregateRepository<TestOrderAggregate, Guid, TestSqlDbContext>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // =========================================================================
    // 1. SaveAsync Lifecycle Tests (DDD Consistency Boundary)
    // =========================================================================

    [Fact]
    public async Task SaveAsync_WhenNewAggregate_ShouldInsertAndPersistWithChildren()
    {
        var orderId = Guid.NewGuid();
        var order = new TestOrderAggregate(orderId, "ORD-1001", "alice@example.com");
        order.AddItem("Laptop", 1200m, 1);
        order.AddItem("Mouse", 25m, 2);

        await _repository.SaveAsync(order);

        var retrieved = await _repository.GetByIdAsync(orderId);
        Assert.NotNull(retrieved);
        Assert.Equal("ORD-1001", retrieved.OrderNumber);
        Assert.Equal(1250m, retrieved.TotalAmount);

        // Verify child items cascaded into relational store
        var items = await _dbContext.Items.Where(i => i.OrderId == orderId).ToListAsync();
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task SaveAsync_WhenAttachedAggregateModified_ShouldPersistModifications()
    {
        var orderId = Guid.NewGuid();
        var order = new TestOrderAggregate(orderId, "ORD-1002", "bob@example.com", 500m);
        await _repository.SaveAsync(order);

        order.Cancel();
        order.TotalAmount = 450m;
        await _repository.SaveAsync(order);

        var retrieved = await _repository.GetByIdAsync(orderId);
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsCancelled);
        Assert.Equal(450m, retrieved.TotalAmount);
    }

    [Fact]
    public async Task SaveAsync_WhenDetachedExistingAggregate_ShouldAttachAndUpdate()
    {
        var orderId = Guid.NewGuid();
        var order = new TestOrderAggregate(orderId, "ORD-1003", "charlie@example.com", 300m);
        await _repository.SaveAsync(order);

        // Detach the entity from EF ChangeTracker
        _dbContext.Entry(order).State = EntityState.Detached;

        // Mutate detached aggregate
        order.OrderNumber = "ORD-1003-UPDATED";
        order.TotalAmount = 350m;

        await _repository.SaveAsync(order);

        var updated = await _repository.GetByIdAsync(orderId);
        Assert.NotNull(updated);
        Assert.Equal("ORD-1003-UPDATED", updated.OrderNumber);
        Assert.Equal(350m, updated.TotalAmount);
    }

    [Fact]
    public async Task SaveAsync_WithNullAggregate_ShouldThrowArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _repository.SaveAsync(null!));
    }

    // =========================================================================
    // 2. Querying & Reading Operations
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldReturnAggregate()
    {
        var id = Guid.NewGuid();
        var order = new TestOrderAggregate(id, "ORD-1004", "dan@example.com", 150m);
        await _repository.SaveAsync(order);

        var result = await _repository.GetByIdAsync(id);
        Assert.NotNull(result);
        Assert.Equal("dan@example.com", result.CustomerEmail);
    }

    [Fact]
    public async Task GetByIdAsync_WhenDoesNotExist_ShouldReturnNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueForExistingAndFalseForMissing()
    {
        var id = Guid.NewGuid();
        var order = new TestOrderAggregate(id, "ORD-1005", "eve@example.com");
        await _repository.SaveAsync(order);

        Assert.True(await _repository.ExistsAsync(id));
        Assert.False(await _repository.ExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task FindAsync_WithSpecification_ShouldReturnMatchingAggregate()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await _repository.SaveAsync(new TestOrderAggregate(id1, "ORD-FIND-1", "frank@example.com", 100m));
        await _repository.SaveAsync(new TestOrderAggregate(id2, "ORD-FIND-2", "grace@example.com", 200m));

        var spec = new TestOrderSpecification(o => o.OrderNumber == "ORD-FIND-2");
        var found = await _repository.FindAsync(spec);

        Assert.NotNull(found);
        Assert.Equal(id2, found.Id);
    }

    [Fact]
    public async Task ListAsync_WithoutArguments_ShouldReturnAllAggregates()
    {
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-L-1", "user1@example.com"));
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-L-2", "user2@example.com"));

        var all = await _repository.ListAsync();
        Assert.True(all.Count >= 2);
    }

    [Fact]
    public async Task ListAsync_WithSpecification_ShouldFilterAggregates()
    {
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-SPEC-1", "spec@example.com", 50m));
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-SPEC-2", "spec@example.com", 150m));

        var spec = new TestOrderSpecification(o => o.CustomerEmail == "spec@example.com" && o.TotalAmount > 100m);
        var matches = await _repository.ListAsync(spec);

        Assert.Single(matches);
        Assert.Equal("ORD-SPEC-2", matches[0].OrderNumber);
    }

    [Fact]
    public async Task CountAsync_WithoutArguments_ShouldReturnTotalCount()
    {
        var initial = await _repository.CountAsync();
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-C-1", "count@example.com"));
        await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), "ORD-C-2", "count@example.com"));

        var after = await _repository.CountAsync();
        Assert.Equal(initial + 2, after);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldPaginateAggregatesCorrectly()
    {
        for (int i = 1; i <= 5; i++)
        {
            await _repository.SaveAsync(new TestOrderAggregate(Guid.NewGuid(), $"ORD-PAG-{i:D2}", "page@example.com", i * 10m));
        }

        var spec = new TestOrderSpecification(o => o.CustomerEmail == "page@example.com")
        {
            OrderBy = o => o.TotalAmount
        };

        var page1 = await _repository.GetPagedAsync(spec, new PagingOptions(1, 2));
        Assert.Equal(5, page1.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(3, page1.TotalPages);
        Assert.Equal("ORD-PAG-01", page1.Items.First().OrderNumber);
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAggregateAndCascadeChildren()
    {
        var orderId = Guid.NewGuid();
        var order = new TestOrderAggregate(orderId, "ORD-DEL", "delete@example.com");
        order.AddItem("ItemToDelete", 50m, 1);
        await _repository.SaveAsync(order);

        Assert.True(await _repository.ExistsAsync(orderId));

        var deleted = await _repository.DeleteAsync(order);
        await _dbContext.SaveChangesAsync();

        Assert.True(deleted);
        Assert.False(await _repository.ExistsAsync(orderId));

        var childItems = await _dbContext.Items.Where(i => i.OrderId == orderId).ToListAsync();
        Assert.Empty(childItems);
    }

    [Fact]
    public void StronglyTypedVariant_ContextProperty_ShouldReturnTypedDbContext()
    {
        Assert.NotNull(_typedRepository.Context);
        Assert.IsType<TestSqlDbContext>(_typedRepository.Context);
    }
}
