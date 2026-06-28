# Aarkam.DDD.Repositories

[![Nuget](https://img.shields.io/nuget/v/Aarkam.DDD.Repositories?style=flat-square)](https://www.nuget.org/packages/Aarkam.DDD.Repositories)
[![GitHub License](https://img.shields.io/github/license/melhelbawi/Aarkam.DDD.Repositories?style=flat-square)](https://github.com/melhelbawi/Aarkam.DDD.Repositories/blob/main/LICENSE)
[![Platform](https://img.shields.io/badge/.NET-%3E%3D%20Standard%202.1%20%7C%206.0%20%7C%208.0%20%7C%2010.0-blue?style=flat-square)](#)
[![Depends On](https://img.shields.io/badge/Depends%20On-Aarkam.DDD.Domain-blue?style=flat-square)](https://github.com/melhelbawi/Aarkam.DDD.Domain)
[![Build Status](https://img.shields.io/github/actions/workflow/status/melhelbawi/Aarkam.DDD.Repositories/ci.yml?branch=main&style=flat-square)](https://github.com/melhelbawi/Aarkam.DDD.Repositories/actions)

A high-performance, enterprise-grade **persistence abstraction layer** for Domain-Driven Design (DDD) applications. Provides **repository interfaces**, **unit of work coordination**, and **distributed patterns** (inbox, outbox, sagas, snapshots) for building resilient, event-sourced, multi-tenant systems.

Built on top of **Aarkam.DDD.Domain**, this package offers ORM-agnostic contracts that work seamlessly with **Entity Framework Core**, **Dapper**, **MongoDB**, or any data access framework of your choice.

---

## 📖 Table of Contents

- [Quick Start](#quick-start-60-seconds)
- [Installation](#installation)
- [Core Concepts](#core-concepts)
- [Repository Patterns](#repository-patterns)
- [Unit of Work Pattern](#unit-of-work-pattern)
- [Advanced Features](#advanced-features)
- [Distributed Patterns](#distributed-patterns)
- [Usage Examples](#usage-examples)
- [Security Considerations](#security-considerations)
- [Performance Notes](#performance-notes)
- [Contributing](#contributing)
- [License](#license)

---

## ⚡ Quick Start (60 Seconds)

```bash
dotnet add package Aarkam.DDD.Repositories
```

```csharp
// Define your domain aggregate
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; } = null!;
    public OrderStatus Status { get; private set; }
    
    // ... domain logic
}

// Implement repository (using EF Core)
public class OrderRepository : IRepository<Order>
{
    private readonly DbContext _context;
    
    public async Task<Order> AddAsync(Order entity, CancellationToken ct = default)
    {
        _context.Orders.Add(entity);
        return entity;
    }
    
    // ... implement other methods
}

// Use in application service
public class CreateOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task ExecuteAsync(CreateOrderRequest request)
    {
        var repository = _unitOfWork.Repository<Order>();
        
        var order = new Order(Guid.NewGuid(), request.CustomerId);
        await repository.AddAsync(order);
        
        // Save all changes within transaction boundary
        await _unitOfWork.SaveChangesAsync();
        
        // Dispatch domain events here
        await DispatchDomainEventsAsync(order.GetChanges());
    }
}
```

---

## 📦 Installation

### Via .NET CLI

```bash
dotnet add package Aarkam.DDD.Repositories
```

### Via Package Manager

```powershell
Install-Package Aarkam.DDD.Repositories
```

### Via NuGet Package Explorer

Search for `Aarkam.DDD.Repositories` on [nuget.org](https://www.nuget.org/packages/Aarkam.DDD.Repositories)

### Dependencies

- ✅ **Aarkam.DDD.Domain** (1.0.0-preview01+) — Core DDD types
- ✅ No other external dependencies — Pure abstractions

### Supported Platforms

- `.NET Standard 2.1` (Xamarin, legacy .NET Framework)
- `.NET 6.0` (LTS)
- `.NET 8.0` (LTS)
- `.NET 10.0` (Current)

---

## 🏛️ Core Concepts

### 1. **Repository Pattern** — Abstraction Over Data Access

```csharp
public interface IRepository<TEntity> where TEntity : class
{
    // CRUD Operations
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
    
    Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<int> DeleteRangeAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    
    // Querying
    IQueryable<TEntity> AsQueryable();
    IQueryable<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    IQueryable<TEntity> WhereIf(bool condition, Expression<Func<TEntity, bool>> predicate);
    IQueryable<TEntity> WithIncludes(params Expression<Func<TEntity, object>>[] includes);
    IQueryable<TEntity> ApplySpecification(ISpecification<TEntity> specification);
    
    // Materialization
    Task<List<TEntity>> ToListAsync(Expression<Func<TEntity, bool>> predicate = null);
    List<TEntity> ToList(Expression<Func<TEntity, bool>> predicate = null);
    Task<PagedResult<TEntity>> ToPagedResultAsync(PagingOptions pagingOptions = null);
}
```

**Key Responsibilities:**
- ✅ Isolate domain logic from persistence implementation
- ✅ Support multiple data sources (EF Core, Dapper, MongoDB, etc.)
- ✅ Provide composable, type-safe querying
- ✅ Handle pagination and filtering at the data layer
- ✅ Enable unit testing via mock repositories

**Why This Matters:**
- Your domain model doesn't depend on `DbContext` or ORM specifics
- Easy to swap implementations (e.g., EF Core → Cosmos DB)
- Testability: inject mock repositories without database
- Performance: leave query optimization to the ORM layer

---

### 2. **Unit of Work Pattern** — Transactional Boundary

```csharp
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
```

**Key Responsibilities:**
- ✅ Coordinate changes across multiple repositories
- ✅ Single commit point for atomic transactions
- ✅ Automatic disposal of resources (IAsyncDisposable)
- ✅ Optional explicit transaction management

**Usage Pattern:**
```csharp
public async Task TransferFundsAsync(Guid fromAccountId, Guid toAccountId, decimal amount)
{
    using var uow = _unitOfWorkFactory.Create();
    
    try
    {
        var fromRepo = uow.Repository<Account>();
        var toRepo = uow.Repository<Account>();
        
        var fromAccount = await fromRepo.GetAsync(fromAccountId);
        var toAccount = await toRepo.GetAsync(toAccountId);
        
        fromAccount.Withdraw(amount);
        toAccount.Deposit(amount);
        
        await uow.SaveChangesAsync();  // Atomic: both or neither
    }
    catch
    {
        await uow.RollbackTransactionAsync();
        throw;
    }
}
```

---

### 3. **Specification Pattern** — Complex Query Composition

The Specification pattern encapsulates business logic queries into reusable, composable objects:

```csharp
public interface ISpecification<TEntity>
{
    Expression<Func<TEntity, bool>> Criteria { get; }
    List<Expression<Func<TEntity, object>>> Includes { get; }
    List<string> IncludeStrings { get; }
    Expression<Func<TEntity, object>> OrderBy { get; }
    Expression<Func<TEntity, object>> OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
    bool IsPagingEnabled { get; }
}

// Usage
public sealed class ActiveOrdersSpecification : SpecificationBase<Order>
{
    public ActiveOrdersSpecification(string customerId, int pageIndex = 0, int pageSize = 10)
    {
        AddCriteria(o => o.CustomerId == customerId && o.Status != OrderStatus.Completed);
        AddInclude(o => o.Items);
        AddInclude(o => o.Payments);
        OrderByDescending = o => o.CreationTime;
        Skip = pageIndex * pageSize;
        Take = pageSize;
    }
}

// In repository
var spec = new ActiveOrdersSpecification(customerId);
var orders = await repository.ApplySpecification(spec).ToListAsync();
```

---

## 📚 Repository Patterns

### Standard CRUD Repository

Handles basic Create, Read, Update, Delete operations:

```csharp
public class CustomerRepository : IRepository<Customer>
{
    private readonly DbContext _context;

    public async Task<Customer> AddAsync(Customer entity, CancellationToken ct = default)
    {
        _context.Customers.Add(entity);
        return entity;
    }

    public async Task<Customer> UpdateAsync(Customer entity, CancellationToken ct = default)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return entity;
    }

    public async Task<bool> DeleteAsync(Customer entity, CancellationToken ct = default)
    {
        _context.Customers.Remove(entity);
        return true;
    }

    public IQueryable<Customer> AsQueryable()
    {
        return _context.Customers.AsQueryable();
    }

    // ... implement other methods
}
```

### Query Filters & Extensions

Apply common filters without polluting domain code:

```csharp
public async Task<List<Order>> GetActiveOrdersAsync(string customerId)
{
    var repo = _unitOfWork.Repository<Order>();
    
    return await repo
        .Where(o => o.CustomerId == customerId)
        .WhereIf(!string.IsNullOrEmpty(statusFilter), 
            o => o.Status.ToString() == statusFilter)
        .WithIncludes(o => o.Items, o => o.Payments)
        .ToListAsync();
}
```

### Pagination

Efficiently paginate large result sets:

```csharp
public async Task<PagedResult<Order>> GetOrdersPagedAsync(
    string customerId, 
    int pageNumber = 1, 
    int pageSize = 20)
{
    var repo = _unitOfWork.Repository<Order>();
    var pagingOptions = new PagingOptions(pageNumber, pageSize);
    
    return await repo
        .Where(o => o.CustomerId == customerId)
        .ToPagedResultAsync(pagingOptions);
}

// Results include: Items, TotalCount, PageNumber, PageSize, TotalPages
```

---

## 🔄 Unit of Work Pattern

### EF Core Implementation Example

```csharp
public class EFCoreUnitOfWork : IUnitOfWork
{
    private readonly DbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var type = typeof(TEntity);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(EFCoreRepository<>).MakeGenericType(type);
            var repository = Activator.CreateInstance(repositoryType, _context);
            _repositories[type] = repository!;
        }
        return (IRepository<TEntity>)_repositories[type];
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitTransactionAsync(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync();
            await _transaction?.CommitAsync();
        }
        catch
        {
            await _transaction?.RollbackAsync();
            throw;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken ct = default)
    {
        await _transaction?.RollbackAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
            await _transaction.DisposeAsync();
        if (_context is not null)
            await _context.DisposeAsync();
    }
}
```

---

## 🚀 Advanced Features

### 1. Conditional Filtering (WhereIf)

```csharp
// No need for if/else branches
var query = repo
    .AsQueryable()
    .WhereIf(filterByStatus, o => o.Status == OrderStatus.Pending)
    .WhereIf(filterByDate, o => o.CreationTime > cutoffDate)
    .WhereIf(!string.IsNullOrEmpty(searchText), 
        o => o.OrderNumber.Contains(searchText));
```

### 2. Eager Loading (WithIncludes)

```csharp
var order = await repo
    .Where(o => o.Id == orderId)
    .WithIncludes(
        o => o.Items,
        o => o.Items.Select(i => i.Product),
        o => o.Payments)
    .FirstOrDefaultAsync();
```

### 3. Search with Security

```csharp
// Secure search: whitelist allowed properties
var results = query.AddSearch(
    "ProductName=Widget,Amount=100",
    allowedProperties: new[] { "ProductName", "Amount" }  // ✅ Whitelist
);

// ❌ Blocked: Attacker cannot search on PasswordHash or Secret
```

### 4. Query Composition

```csharp
public async Task<List<Order>> ComplexQueryAsync(OrderFilterCriteria criteria)
{
    var query = _repository.AsQueryable();
    
    // Compose filters fluently
    if (criteria.Status.HasValue)
        query = query.Where(o => o.Status == criteria.Status.Value);
    
    if (criteria.MinAmount.HasValue)
        query = query.Where(o => o.TotalAmount >= criteria.MinAmount.Value);
    
    if (!string.IsNullOrEmpty(criteria.CustomerSearch))
        query = query.AddSearch($"CustomerName={criteria.CustomerSearch}");
    
    return await query
        .WithIncludes(o => o.Items)
        .OrderByDescending(o => o.CreationTime)
        .ToListAsync();
}
```

---

## 🔁 Distributed Patterns

### 1. Inbox Pattern (Idempotent Event Processing)

**Problem:** Message broker delivers event twice → duplicate processing

**Solution:** Record processed events in database (inbox) within transaction:

```csharp
public interface IInboxRepository
{
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName);
    Task RecordAsync(IInboxMessage message, CancellationToken ct = default);
    Task<int> CleanupAsync(DateTimeOffset olderThan);
}

// Usage in event handler
public class OrderCreatedEventHandler
{
    private readonly IInboxRepository _inbox;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;

    public async Task HandleAsync(OrderCreatedEvent @event)
    {
        // ✅ Guard: If already processed, exit early (idempotence)
        if (await _inbox.HasBeenProcessedAsync(@event.Id, "OrderCreatedHandler"))
        {
            _logger.LogInformation("Event {EventId} already processed", @event.Id);
            return;
        }

        // Process the event
        var order = await _orders.GetAsync(@event.AggregateId);
        order.MarkAsNotified();

        // Record that we processed it (atomic with SaveChanges)
        await _inbox.RecordAsync(new InboxMessage 
        { 
            EventId = @event.Id, 
            ConsumerName = "OrderCreatedHandler"
        });
        
        await _uow.SaveChangesAsync();
    }
}
```

**Benefits:**
- ✅ Guaranteed exactly-once processing
- ✅ No duplicate side effects
- ✅ Atomic with domain changes

---

### 2. Outbox Pattern (Transactional Messaging)

**Problem:** Domain event raised, but message broker unreachable → event lost

**Solution:** Persist messages in database (outbox) within transaction, then publish asynchronously:

```csharp
public interface IOutboxRepository
{
    Task AddAsync(IOutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<IOutboxMessage>> GetPendingAsync(int batchSize);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task RecordFailureAsync(Guid messageId, string error);
    Task<int> CleanupAsync(DateTimeOffset olderThan);
}

// In application service: Add to outbox atomically
public async Task CreateOrderAsync(CreateOrderRequest request)
{
    var order = new Order(Guid.NewGuid(), request.CustomerId);
    await _orders.AddAsync(order);
    
    // Add outbox message (atomic with SaveChanges)
    var outboxMessage = new OutboxMessage
    {
        AggregateId = order.Id,
        EventType = "OrderCreated",
        Payload = JsonSerializer.Serialize(order.GetChanges().First())
    };
    await _outbox.AddAsync(outboxMessage);
    
    await _uow.SaveChangesAsync();  // Both persisted or both rolled back
}

// Background processor: Publish pending messages
public class OutboxPublisher : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pending = await _outbox.GetPendingAsync(batchSize: 100);
            
            foreach (var message in pending)
            {
                try
                {
                    await _messageBroker.PublishAsync(message.Payload);
                    await _outbox.MarkAsProcessedAsync(message.Id);
                }
                catch (Exception ex)
                {
                    await _outbox.RecordFailureAsync(message.Id, ex.Message);
                }
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
}
```

**Benefits:**
- ✅ Messages guaranteed to be published (durability)
- ✅ No loss on application crash
- ✅ Decouples domain logic from message broker availability

---

### 3. Saga Pattern (Long-Running Processes)

**Problem:** Multi-step business process needs coordination across multiple aggregates/services

**Solution:** Persist saga state, replay events to resume:

```csharp
public interface ISagaRepository
{
    Task<TSaga> LoadAsync<TSaga, TSagaData>(Guid sagaId)
        where TSaga : ISaga<TSagaData>
        where TSagaData : class, new();
    
    Task SaveAsync<TSaga, TSagaData>(TSaga saga)
        where TSaga : ISaga<TSagaData>
        where TSagaData : class, new();
    
    Task<IReadOnlyList<Guid>> FindTimedOutSagasAsync(TimeSpan timeout);
    Task<int> CleanupAsync(DateTimeOffset olderThan);
}

// Example: Order fulfillment saga
public class OrderFulfillmentSaga : ISaga<OrderFulfillmentSagaData>
{
    public Guid OrderId { get; set; }
    public OrderFulfillmentSagaData Data { get; set; }

    public async Task HandleOrderCreatedAsync(OrderCreatedEvent @event)
    {
        Data.OrderId = @event.AggregateId;
        Data.Status = SagaStatus.PaymentPending;
        
        // Emit command to payment service
        await _commandBus.SendAsync(new ProcessPaymentCommand(OrderId, @event.Amount));
    }

    public async Task HandlePaymentCompletedAsync(PaymentCompletedEvent @event)
    {
        Data.Status = SagaStatus.ShippingPending;
        
        // Emit command to shipping service
        await _commandBus.SendAsync(new ShipOrderCommand(OrderId));
    }

    public async Task HandleOrderShippedAsync(OrderShippedEvent @event)
    {
        Data.Status = SagaStatus.Completed;
        // Saga complete
    }
}

// Usage in saga coordinator
public class OrderFulfillmentSagaCoordinator
{
    public async Task StartSagaAsync(OrderCreatedEvent @event)
    {
        var saga = new OrderFulfillmentSaga 
        { 
            OrderId = @event.AggregateId,
            Data = new OrderFulfillmentSagaData()
        };
        
        await saga.HandleOrderCreatedAsync(@event);
        await _sagaRepository.SaveAsync(saga);
    }

    public async Task ResumeSagaAsync(PaymentCompletedEvent @event)
    {
        var saga = await _sagaRepository.LoadAsync<OrderFulfillmentSaga, OrderFulfillmentSagaData>(@event.OrderId);
        await saga.HandlePaymentCompletedAsync(@event);
        await _sagaRepository.SaveAsync(saga);
    }
}
```

**Benefits:**
- ✅ Survives application restarts
- ✅ Enables timeout/retry logic
- ✅ Explicit process flow

---

### 4. Snapshot Pattern (Event Sourcing Optimization)

**Problem:** Replaying 10,000 events to load an aggregate is slow

**Solution:** Periodically snapshot aggregate state:

```csharp
public interface ISnapshotRepository
{
    Task<ISnapshot> GetLatestAsync(Guid aggregateId);
    Task<ISnapshot> GetAtVersionAsync(Guid aggregateId, long maxVersion);
    Task SaveAsync(ISnapshot snapshot);
    Task<int> PruneAsync(Guid aggregateId, int keepCount = 3);
}

// In event store
public async Task<Order> LoadAsync(Guid aggregateId)
{
    Order order;
    
    // ✅ Try to load from snapshot first
    var snapshot = await _snapshotRepository.GetLatestAsync(aggregateId);
    if (snapshot != null)
    {
        order = JsonSerializer.Deserialize<Order>(snapshot.Data)!;
        var fromVersion = snapshot.Version;
    }
    else
    {
        order = new Order(aggregateId);
        fromVersion = 0;
    }

    // Replay only events after snapshot
    var events = await _eventStore.GetEventsAfterVersionAsync(aggregateId, fromVersion);
    foreach (var @event in events)
    {
        order.ApplyEvent(@event);
    }

    return order;
}

// Periodic snapshot creation (background job)
public async Task CreateSnapshotsAsync()
{
    var orderIds = await _eventStore.GetAggregateIdsWithManyEventsAsync(minEventCount: 500);
    
    foreach (var orderId in orderIds)
    {
        var order = await LoadAsync(orderId);
        var snapshot = new Snapshot
        {
            AggregateId = orderId,
            Version = order.Version,
            Data = JsonSerializer.Serialize(order)
        };
        
        await _snapshotRepository.SaveAsync(snapshot);
    }
}
```

**Benefits:**
- ✅ Sub-millisecond aggregate load times (vs. seconds for 10k events)
- ✅ Scales event sourcing to millions of aggregates
- ✅ Automatic prune removes old snapshots

---

## 💻 Usage Examples

### Complete Order Management Flow

```csharp
// 1. Create order
public class CreateOrderService
{
    public async Task ExecuteAsync(CreateOrderRequest request)
    {
        var repository = _unitOfWork.Repository<Order>();
        
        var order = new Order(Guid.NewGuid(), request.CustomerId);
        order.AddItem(request.ProductSku, request.Quantity, request.Price);
        
        await repository.AddAsync(order);
        await _unitOfWork.SaveChangesAsync();
        
        // Dispatch events to event bus
        await _eventBus.PublishAsync(order.GetChanges());
    }
}

// 2. Query orders with pagination
public class GetOrdersService
{
    public async Task<PagedResult<OrderDto>> ExecuteAsync(GetOrdersQuery query)
    {
        var spec = new OrdersForCustomerSpecification(
            customerId: query.CustomerId,
            pageIndex: query.PageIndex,
            pageSize: query.PageSize);
        
        var repository = _unitOfWork.Repository<Order>();
        
        var result = await repository
            .ApplySpecification(spec)
            .ProjectTo<OrderDto>(_mapper.ConfigurationProvider)
            .ToPagedResultAsync(
                new PagingOptions(query.PageIndex + 1, query.PageSize));
        
        return result;
    }
}

// 3. Update order with concurrency handling
public class ConfirmOrderService
{
    public async Task ExecuteAsync(Guid orderId, ConfirmOrderCommand command)
    {
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            var repository = _unitOfWork.Repository<Order>();
            var order = await repository.Where(o => o.Id == orderId).FirstOrDefaultAsync();
            
            if (order == null)
                throw new OrderNotFoundException(orderId);
            
            order.Confirm(command.ConfirmedBy);
            
            await repository.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            
            // ✅ Transaction committed, events safe
            await _eventBus.PublishAsync(order.GetChanges());
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
```

---

## 🔒 Security Considerations

### 1. Property Whitelisting in Search

```csharp
// ❌ DANGEROUS: User can search on any property
var results = query.AddSearch("PasswordHash=abc123");

// ✅ SECURE: Whitelist allowed properties
var results = query.AddSearch(
    "CustomerName=John",
    allowedProperties: new[] { "CustomerName", "Email", "Phone" }
);
```

### 2. Tenant Isolation

```csharp
// ✅ CORRECT: Filter by current tenant automatically
public abstract class TenantRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, IHasTenant
{
    public IQueryable<TEntity> AsQueryable()
    {
        return _context.Set<TEntity>()
            .Where(e => e.TenantId == _currentTenant.Id);  // Automatic filtering
    }
}
```

### 3. Soft Deletes

```csharp
// ✅ CORRECT: Deleted entities are never returned
public abstract class SoftDeleteRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, ISoftDelete
{
    public IQueryable<TEntity> AsQueryable()
    {
        return _context.Set<TEntity>()
            .Where(e => !e.IsDeleted);  // Exclude soft-deleted
    }
}
```

---

## ⚡ Performance Notes

### Query Materialization

**Best Practice:** Use `AsQueryable()` + LINQ to delay materialization:

```csharp
// ✅ GOOD: Filter applied at database (thousands of rows become tens)
var expensiveOrders = repository
    .Where(o => o.TotalAmount > 10000)  // ← Pushed to DB
    .ToListAsync();

// ❌ BAD: Load all orders, filter in memory (millions → no cache benefit)
var expensiveOrders = repository
    .AsQueryable()
    .ToList()                            // ← Materialized too early
    .Where(o => o.TotalAmount > 10000)
    .ToList();
```

### Pagination for Large Datasets

```csharp
// ✅ CORRECT: Paginate at database
var result = await repository
    .Where(o => o.Status == OrderStatus.Active)
    .ToPagedResultAsync(new PagingOptions(pageNumber: 50, pageSize: 20));

// ❌ WRONG: Load all 1M records, paginate in memory
var allOrders = await repository.ToListAsync();  // OOM!
var page = allOrders.Skip(49 * 20).Take(20);
```

### Eager Loading (N+1 Problem)

```csharp
// ✅ CORRECT: Load related data in single query
var orders = await repository
    .Where(o => o.CustomerId == customerId)
    .WithIncludes(o => o.Items, o => o.Payments)  // ← Eager load
    .ToListAsync();

// ❌ WRONG: 1 query for orders + N queries for each order's items (N+1)
var orders = await repository
    .Where(o => o.CustomerId == customerId)
    .ToListAsync();
foreach (var order in orders)
{
    var items = order.Items;  // ← Lazy load triggers query per order
}
```

---

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. **Report Issues:** GitHub Issues with reproduction steps
2. **Fork & Branch:** From `develop` branch
3. **Write Tests:** xUnit for all new features
4. **Code Style:** Nullable annotations, XML docs, 120-char lines
5. **Commit Messages:** Conventional format `feat: add...`
6. **Submit PR:** With linked issue and description

**Development Setup:**

```bash
git clone https://github.com/melhelbawi/Aarkam.DDD.Repositories.git
cd Aarkam.DDD.Repositories
dotnet restore
dotnet test
dotnet build
```

---

## 📋 Versioning

Follows **Semantic Versioning 2.0.0**:
- `1.0.0+`: Production-ready
- `0.x.x`: Preview (API may change)

---

## 📄 License

Distributed under **MIT License**. See [LICENSE](https://github.com/melhelbawi/Aarkam.DDD.Repositories/blob/main/LICENSE) for details.

```
Copyright (c) 2026 Mohamed Elhelbawi / Aarkam

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software...
```

---

## 🙏 Acknowledgments

Built with inspiration from:

- **Repository Pattern** — Martin Fowler, *Patterns of Enterprise Application Architecture*
- **Unit of Work Pattern** — EF Core DbContext design
- **Inbox/Outbox Patterns** — Enterprise Integration Patterns (Hohpe & Woolf)
- **Saga Pattern** — Clemens Vasters, *Sagas & Choreographed Workflows*
- **Snapshot Pattern** — Event Sourcing Patterns

---

## 📞 Support

- **Documentation:** [GitHub Wiki](https://github.com/melhelbawi/Aarkam.DDD.Repositories/wiki)
- **Issues:** [GitHub Issues](https://github.com/melhelbawi/Aarkam.DDD.Repositories/issues)
- **Discussions:** [GitHub Discussions](https://github.com/melhelbawi/Aarkam.DDD.Repositories/discussions)
- **Depends On:** [Aarkam.DDD.Domain](https://github.com/melhelbawi/Aarkam.DDD.Domain)

---

## 🧪 Testing with Mock Repositories

```csharp
// Unit test with mock repository
public class CreateOrderServiceTests
{
    private readonly Mock<IRepository<Order>> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly CreateOrderService _service;

    [Fact]
    public async Task Execute_WithValidOrder_AddsOrderAndSavesChanges()
    {
        // Arrange
        var request = new CreateOrderRequest { CustomerId = "customer-1", ... };
        
        _mockUnitOfWork.Setup(uow => uow.Repository<Order>())
            .Returns(_mockRepository.Object);

        // Act
        await _service.ExecuteAsync(request);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Order>(), default), Times.Once);
        _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(default), Times.Once);
    }
}
```

---

**Made with ❤️ by Mohamed Elhelbawi**

**Depends on:** [Aarkam.DDD.Domain](https://github.com/melhelbawi/Aarkam.DDD.Domain) v1.0.0-preview01+

