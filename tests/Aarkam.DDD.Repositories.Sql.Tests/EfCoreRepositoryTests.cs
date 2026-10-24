using Aarkam.DDD.Domain;
using Aarkam.DDD.Repositories.Sql.Repositories;
using Aarkam.DDD.Repositories.Sql.Tests.Fakes;
using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarkam.DDD.Repositories.Sql.Tests;

public class EfCoreRepositoryTests : IDisposable
{
    private readonly TestSqlDbContext _dbContext;
    private readonly EfCoreRepository<TestItemEntity, int> _repository;

    public EfCoreRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestSqlDbContext>()
            .UseInMemoryDatabase(databaseName: $"EfRepoTest_{Guid.NewGuid():N}")
            .Options;

        _dbContext = new TestSqlDbContext(options);
        _repository = new EfCoreRepository<TestItemEntity, int>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    // =========================================================================
    // 1. Basic CRUD Operations
    // =========================================================================

    [Fact]
    public async Task AddAsync_ShouldAddEntityToDatabase()
    {
        var item = new TestItemEntity { ProductName = "Monitor", UnitPrice = 250m, Quantity = 2 };

        var added = await _repository.AddAsync(item);
        await _dbContext.SaveChangesAsync();

        Assert.True(added.Id > 0);
        Assert.Equal("Monitor", added.ProductName);

        var retrieved = await _repository.GetByIdAsync(added.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Monitor", retrieved.ProductName);
    }

    [Fact]
    public async Task AddRangeAsync_ShouldAddMultipleEntities()
    {
        var items = new[]
        {
            new TestItemEntity { ProductName = "Item 1", UnitPrice = 10m, Quantity = 1 },
            new TestItemEntity { ProductName = "Item 2", UnitPrice = 20m, Quantity = 2 },
            new TestItemEntity { ProductName = "Item 3", UnitPrice = 30m, Quantity = 3 }
        };

        await _repository.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var count = await _repository.CountAsync();
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyEntityValues()
    {
        var item = new TestItemEntity { ProductName = "Original", UnitPrice = 100m, Quantity = 1 };
        await _repository.AddAsync(item);
        await _dbContext.SaveChangesAsync();

        item.ProductName = "Updated";
        item.UnitPrice = 150m;
        await _repository.UpdateAsync(item);
        await _dbContext.SaveChangesAsync();

        var reloaded = await _repository.GetByIdAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Updated", reloaded.ProductName);
        Assert.Equal(150m, reloaded.UnitPrice);
    }

    [Fact]
    public async Task UpdateRangeAsync_ShouldModifyMultipleEntities()
    {
        var items = new[]
        {
            new TestItemEntity { ProductName = "A", UnitPrice = 10m, Quantity = 1 },
            new TestItemEntity { ProductName = "B", UnitPrice = 20m, Quantity = 2 }
        };
        await _repository.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        foreach (var i in items) i.UnitPrice += 5m;
        await _repository.UpdateRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var list = await _repository.ListAsync();
        Assert.All(list, i => Assert.True(i.UnitPrice >= 15m));
    }

    [Fact]
    public async Task DeleteAsync_ByEntity_ShouldRemoveFromDatabase()
    {
        var item = new TestItemEntity { ProductName = "To Delete", UnitPrice = 50m, Quantity = 1 };
        await _repository.AddAsync(item);
        await _dbContext.SaveChangesAsync();

        var deleted = await _repository.DeleteAsync(item);
        await _dbContext.SaveChangesAsync();

        Assert.True(deleted);
        var exists = await _repository.ExistsAsync(item.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteByIdAsync_WhenExists_ShouldDeleteAndReturnTrue()
    {
        var item = new TestItemEntity { ProductName = "Delete Me", UnitPrice = 50m, Quantity = 1 };
        await _repository.AddAsync(item);
        await _dbContext.SaveChangesAsync();

        var deleted = await _repository.DeleteByIdAsync(item.Id);
        await _dbContext.SaveChangesAsync();

        Assert.True(deleted);
        var check = await _repository.GetByIdAsync(item.Id);
        Assert.Null(check);
    }

    [Fact]
    public async Task DeleteByIdAsync_WhenNotExists_ShouldReturnFalse()
    {
        var deleted = await _repository.DeleteByIdAsync(9999);
        Assert.False(deleted);
    }

    [Fact]
    public async Task DeleteRangeAsync_ByPredicate_ShouldDeleteMatchingEntities()
    {
        var items = new[]
        {
            new TestItemEntity { ProductName = "Match 1", UnitPrice = 10m, Quantity = 1 },
            new TestItemEntity { ProductName = "Match 2", UnitPrice = 10m, Quantity = 2 },
            new TestItemEntity { ProductName = "Keep", UnitPrice = 50m, Quantity = 5 }
        };
        await _repository.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var deletedCount = await _repository.DeleteRangeAsync(x => x.UnitPrice == 10m);
        await _dbContext.SaveChangesAsync();

        Assert.Equal(2, deletedCount);
        var remaining = await _repository.ListAsync();
        Assert.Single(remaining);
        Assert.Equal("Keep", remaining[0].ProductName);
    }

    // =========================================================================
    // 2. Querying, Exists, & Specification Tests
    // =========================================================================

    [Theory]
    [InlineData("Monitor", true)]
    [InlineData("NonExistent", false)]
    public async Task FindAsync_WithPredicate_ShouldReturnMatchingOrNull(string productName, bool expectedFound)
    {
        var item = new TestItemEntity { ProductName = "Monitor", UnitPrice = 200m, Quantity = 1 };
        await _repository.AddAsync(item);
        await _dbContext.SaveChangesAsync();

        var result = await _repository.FindAsync(x => x.ProductName == productName);

        if (expectedFound)
        {
            Assert.NotNull(result);
            Assert.Equal(productName, result.ProductName);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public async Task CountAsync_WithAndWithoutPredicate_ShouldReturnAccurateCounts()
    {
        var items = new[]
        {
            new TestItemEntity { ProductName = "Expensive", UnitPrice = 500m, Quantity = 1 },
            new TestItemEntity { ProductName = "Cheap 1", UnitPrice = 20m, Quantity = 1 },
            new TestItemEntity { ProductName = "Cheap 2", UnitPrice = 30m, Quantity = 1 }
        };
        await _repository.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var totalCount = await _repository.CountAsync();
        var cheapCount = await _repository.CountAsync(x => x.UnitPrice < 100m);

        Assert.Equal(3, totalCount);
        Assert.Equal(2, cheapCount);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPaginatedSubsets()
    {
        var orderRepo = new EfCoreRepository<TestOrderAggregate, Guid>(_dbContext);
        for (int i = 1; i <= 15; i++)
        {
            await orderRepo.AddAsync(new TestOrderAggregate(Guid.NewGuid(), $"ORD-{i:D3}", $"user{i}@test.com", i * 10m));
        }
        await _dbContext.SaveChangesAsync();

        var spec = new TestOrderSpecification(x => x.TotalAmount > 0)
        {
            OrderBy = x => x.OrderNumber
        };

        var page1 = await orderRepo.GetPagedAsync(spec, new PagingOptions(1, 5));
        var page2 = await orderRepo.GetPagedAsync(spec, new PagingOptions(2, 5));

        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(5, page1.Items.Count);
        Assert.Equal(3, page1.TotalPages);
        Assert.False(page1.HasPreviousPage);
        Assert.True(page1.HasNextPage);

        Assert.Equal(5, page2.Items.Count);
        Assert.True(page2.HasPreviousPage);
        Assert.True(page2.HasNextPage);
    }

    [Fact]
    public async Task WhereIf_ShouldConditionallyApplyFilter()
    {
        var items = new[]
        {
            new TestItemEntity { ProductName = "Alpha", UnitPrice = 100m, Quantity = 1 },
            new TestItemEntity { ProductName = "Beta", UnitPrice = 200m, Quantity = 1 }
        };
        await _repository.AddRangeAsync(items);
        await _dbContext.SaveChangesAsync();

        var filtered = await _repository.WhereIf(true, x => x.UnitPrice > 150m).ToListAsync();
        var unfiltered = await _repository.WhereIf(false, x => x.UnitPrice > 150m).ToListAsync();

        Assert.Single(filtered);
        Assert.Equal("Beta", filtered[0].ProductName);

        Assert.Equal(2, unfiltered.Count);
    }

    [Fact]
    public async Task WithIncludes_ShouldEagerLoadNavigationProperties()
    {
        var orderRepo = new EfCoreRepository<TestOrderAggregate, Guid>(_dbContext);
        var order = new TestOrderAggregate(Guid.NewGuid(), "ORD-INCL", "customer@test.com");
        order.AddItem("Laptop", 1200m, 1);
        order.AddItem("Mouse", 30m, 2);

        await orderRepo.AddAsync(order);
        await _dbContext.SaveChangesAsync();

        // Clear tracking to verify eager loading
        _dbContext.ChangeTracker.Clear();

        var loadedWithItems = await orderRepo
            .WithIncludes(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == order.Id);

        Assert.NotNull(loadedWithItems);
        Assert.Equal(2, loadedWithItems.Items.Count);
    }
}
