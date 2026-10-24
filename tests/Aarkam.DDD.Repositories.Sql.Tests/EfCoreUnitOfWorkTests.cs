using Aarkam.DDD.Repositories.Sql.Tests.Fakes;
using Aarkam.DDD.Repositories.Sql.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aarkam.DDD.Repositories.Sql.Tests;

public class EfCoreUnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestSqlDbContext _dbContext;
    private readonly EfCoreUnitOfWork _unitOfWork;
    private readonly EfCoreUnitOfWork<TestSqlDbContext> _typedUnitOfWork;

    public EfCoreUnitOfWorkTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestSqlDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new TestSqlDbContext(options);
        _dbContext.Database.EnsureCreated();

        _unitOfWork = new EfCoreUnitOfWork(_dbContext);
        _typedUnitOfWork = new EfCoreUnitOfWork<TestSqlDbContext>(_dbContext);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        _typedUnitOfWork.Dispose();
        _dbContext.Dispose();
        _connection.Dispose();
    }

    // =========================================================================
    // 1. Transaction Boundaries & Lifecycle
    // =========================================================================

    [Fact]
    public void Constructor_WithNullDbContext_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EfCoreUnitOfWork(null!));
    }

    [Fact]
    public void HasActiveTransaction_Initially_ShouldBeFalse()
    {
        Assert.False(_unitOfWork.HasActiveTransaction);
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldOpenActiveTransaction()
    {
        await _unitOfWork.BeginTransactionAsync();

        Assert.True(_unitOfWork.HasActiveTransaction);

        await _unitOfWork.RollbackTransactionAsync();
        Assert.False(_unitOfWork.HasActiveTransaction);
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenAlreadyActive_ShouldThrowInvalidOperationException()
    {
        await _unitOfWork.BeginTransactionAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.BeginTransactionAsync());
        Assert.Contains("already in progress", ex.Message);

        await _unitOfWork.RollbackTransactionAsync();
    }

    [Fact]
    public async Task CommitTransactionAsync_WithoutActiveTransaction_ShouldThrowInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.CommitTransactionAsync());
        Assert.Contains("no active transaction is currently open", ex.Message);
    }

    [Fact]
    public async Task CommitTransactionAsync_WithActiveTransaction_ShouldPersistChangesAndCloseTransaction()
    {
        await _unitOfWork.BeginTransactionAsync();

        var order = new TestOrderAggregate(Guid.NewGuid(), "ORD-TX-1", "tx1@example.com", 250m);
        await _dbContext.Orders.AddAsync(order);

        await _unitOfWork.CommitTransactionAsync();

        Assert.False(_unitOfWork.HasActiveTransaction);

        // Verify entity was committed to the database
        var retrieved = await _dbContext.Orders.FindAsync(order.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("ORD-TX-1", retrieved.OrderNumber);
    }

    [Fact]
    public async Task RollbackTransactionAsync_ShouldRevertUncommittedChanges()
    {
        var orderId = Guid.NewGuid();
        var initialOrder = new TestOrderAggregate(orderId, "ORD-INIT", "init@example.com", 100m);
        await _dbContext.Orders.AddAsync(initialOrder);
        await _dbContext.SaveChangesAsync();

        // Start transaction and mutate
        await _unitOfWork.BeginTransactionAsync();
        initialOrder.TotalAmount = 999m;

        await _unitOfWork.RollbackTransactionAsync();

        Assert.False(_unitOfWork.HasActiveTransaction);

        // Reload from database to ensure changes were rolled back
        await _dbContext.Entry(initialOrder).ReloadAsync();
        Assert.Equal(100m, initialOrder.TotalAmount);
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenNoTransactionActive_ShouldBeSafeNoOp()
    {
        // Calling rollback when no transaction is active should not throw
        await _unitOfWork.RollbackTransactionAsync();
        Assert.False(_unitOfWork.HasActiveTransaction);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldPersistEntitiesDirectly()
    {
        var order = new TestOrderAggregate(Guid.NewGuid(), "ORD-UOW", "uow@example.com", 50m);
        await _dbContext.Orders.AddAsync(order);

        var affected = await _unitOfWork.SaveChangesAsync();

        Assert.True(affected > 0);
    }

    [Fact]
    public async Task DisposeAsync_WithOpenTransaction_ShouldSafelyDisposeTransaction()
    {
        var localUow = new EfCoreUnitOfWork(_dbContext);
        await localUow.BeginTransactionAsync();
        Assert.True(localUow.HasActiveTransaction);

        await localUow.DisposeAsync();
        Assert.False(localUow.HasActiveTransaction);
    }

    [Fact]
    public async Task Dispose_WithOpenTransaction_ShouldSafelyDisposeTransaction()
    {
        var localUow = new EfCoreUnitOfWork(_dbContext);
        await localUow.BeginTransactionAsync();
        Assert.True(localUow.HasActiveTransaction);

        localUow.Dispose();
        Assert.False(localUow.HasActiveTransaction);
    }

    [Fact]
    public void StronglyTypedVariant_Context_ShouldReturnTypedDbContext()
    {
        Assert.NotNull(_typedUnitOfWork.Context);
        Assert.IsType<TestSqlDbContext>(_typedUnitOfWork.Context);
    }
}
