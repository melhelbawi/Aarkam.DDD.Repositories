using Aarkam.DDD.Domain;
using Aarkam.DDD.Domain.Entities;
using Aarkam.DDD.Repositories.Sql.Configurations;
using Aarkam.DDD.Repositories.Sql.Entities;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Aarkam.DDD.Repositories.Sql.Tests.Fakes;

public class TestOrderAggregate : AggregateRoot<Guid>
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public bool IsCancelled { get; set; }
    public List<TestItemEntity> Items { get; set; } = new();

    public TestOrderAggregate() { }

    public TestOrderAggregate(Guid id, string orderNumber, string customerEmail, decimal totalAmount = 0)
    {
        Id = id;
        OrderNumber = orderNumber;
        CustomerEmail = customerEmail;
        TotalAmount = totalAmount;
    }

    public void AddItem(string productName, decimal price, int quantity)
    {
        Items.Add(new TestItemEntity
        {
            OrderId = Id,
            ProductName = productName,
            UnitPrice = price,
            Quantity = quantity
        });
        TotalAmount += price * quantity;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }

    public override string GetAggregateType() => nameof(TestOrderAggregate);
}

public class TestItemEntity : Entity<int>
{
    public Guid OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class TestOrderSpecification : ISpecification<TestOrderAggregate>
{
    private readonly Expression<Func<TestOrderAggregate, bool>> _criteria;
    public IReadOnlyList<Expression<Func<TestOrderAggregate, object>>> Includes { get; init; } = [];
    public Expression<Func<TestOrderAggregate, object>>? OrderBy { get; init; }
    public Expression<Func<TestOrderAggregate, object>>? OrderByDescending { get; init; }
    public int? Take { get; init; }
    public int? Skip { get; init; }

    public TestOrderSpecification(Expression<Func<TestOrderAggregate, bool>> criteria)
    {
        _criteria = criteria;
    }

    public Expression<Func<TestOrderAggregate, bool>> ToExpression() => _criteria;

    public bool IsSatisfiedBy(TestOrderAggregate entity) => _criteria.Compile()(entity);
}

public class TestSqlDbContext : DbContext
{
    public DbSet<TestOrderAggregate> Orders => Set<TestOrderAggregate>();
    public DbSet<TestItemEntity> Items => Set<TestItemEntity>();

    public TestSqlDbContext(DbContextOptions<TestSqlDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestOrderAggregate>(b =>
        {
            b.HasKey(o => o.Id);
            b.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            b.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(256);
            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.Ignore(o => o.ExtraProperties);
            b.Property(o => o.CreatorId).IsRequired(false);
        });

        modelBuilder.Entity<TestItemEntity>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.ProductName).IsRequired().HasMaxLength(100);
        });

        // Apply Aarkam messaging tables configuration
        modelBuilder.ApplyAarkamSqlMessagingConfigurations();
    }
}
