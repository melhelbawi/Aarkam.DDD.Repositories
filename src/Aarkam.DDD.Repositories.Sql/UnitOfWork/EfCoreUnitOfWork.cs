using Aarkam.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Aarkam.DDD.Repositories.Sql.UnitOfWork;

/// <summary>
/// Entity Framework Core implementation of the lean <see cref="IUnitOfWork"/> transaction boundary.
/// Coordinates transactional commits and rollbacks without acting as an anti-pattern Service Locator.
/// </summary>
public class EfCoreUnitOfWork : IUnitOfWork, IAsyncDisposable, IDisposable
{
    protected readonly DbContext _dbContext;
    protected IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public EfCoreUnitOfWork(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _currentTransaction != null;

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("An active database transaction is already in progress within this Unit of Work context.");
        }

        _currentTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("Cannot commit transaction because no active transaction is currently open.");
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _currentTransaction?.Dispose();
        _currentTransaction = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (_currentTransaction != null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Strongly-typed generic variant of <see cref="EfCoreUnitOfWork"/> binding to a specific <typeparamref name="TDbContext"/>.
/// </summary>
/// <typeparam name="TDbContext">The application DbContext type.</typeparam>
public class EfCoreUnitOfWork<TDbContext> : EfCoreUnitOfWork, IUnitOfWork<TDbContext>
    where TDbContext : DbContext
{
    public EfCoreUnitOfWork(TDbContext dbContext) : base(dbContext)
    {
    }

    public TDbContext Context => (TDbContext)_dbContext;
}
