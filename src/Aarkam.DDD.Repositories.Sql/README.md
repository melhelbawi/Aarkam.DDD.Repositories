# Aarkam.DDD.Repositories.Sql

[![NuGet Version](https://img.shields.io/nuget/v/Aarkam.DDD.Repositories.Sql.svg?style=flat-square&color=007ACC)](https://www.nuget.org/packages/Aarkam.DDD.Repositories.Sql/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Aarkam.DDD.Repositories.Sql.svg?style=flat-square&color=28a745)](https://www.nuget.org/packages/Aarkam.DDD.Repositories.Sql/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/melhelbawi/Aarkam.DDD.Repositories/blob/main/LICENSE)
[![.NET Multi-Target](https://img.shields.io/badge/.NET-6.0_%7C_7.0_%7C_8.0_%7C_9.0_%7C_10.0_%7C_11.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![AOT Ready](https://img.shields.io/badge/Native_AOT-Supported-success?style=flat-square&logo=speedtest)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)

> **Comprehensive, production-ready SQL and Relational Repository implementations for Domain-Driven Design (DDD) in .NET powered by Entity Framework Core.**

`Aarkam.DDD.Repositories.Sql` provides clean, fully-featured Entity Framework Core implementations of the abstractions defined in `Aarkam.DDD.Repositories`:
- `IAggregateRepository<TAggregate, TKey>`
- `IRepository<TEntity, TKey>`
- `IBasicRepository<TEntity, TKey>`
- `IReadOnlyRepository<TEntity, TKey>`
- `IUnitOfWork` (and `IUnitOfWork<TDbContext>`)
- Transactional Outbox (`IOutboxRepository`)
- Idempotent Consumer Inbox (`IInboxRepository`)
- Distributed Saga Persistence (`ISagaRepository`)
- Event Sourcing Aggregate Snapshot Persistence (`ISnapshotRepository`)

Compatible with any relational database supported by Entity Framework Core:
- **Microsoft SQL Server** (`Microsoft.EntityFrameworkCore.SqlServer`)
- **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **MySQL / MariaDB** (`Pomelo.EntityFrameworkCore.MySql`)
- **SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Oracle** (`Oracle.EntityFrameworkCore`)

---

## 📦 Installation

```bash
dotnet add package Aarkam.DDD.Repositories.Sql --version 1.0.1
```

Or using the Package Manager Console:

```powershell
Install-Package Aarkam.DDD.Repositories.Sql -Version 1.0.1
```

---

## 🚀 Quick Setup

### 1. Register with Dependency Injection

```csharp
using Aarkam.DDD.Repositories.Sql.DependencyInjection;

// In Program.cs:
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Registers UnitOfWork, AggregateRepositories, and Messaging Repositories:
builder.Services.AddAarkamSqlRepositories<ApplicationDbContext>();
```

### 2. Configure Model Builder

```csharp
using Aarkam.DDD.Repositories.Sql.Configurations;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configures Outbox, Inbox, Saga, and Snapshot tables with optimized indexes
        modelBuilder.ApplyAarkamSqlMessagingConfigurations();
    }
}
```

---

## 👥 Authors & Maintainers

- **Eng. Mohamed ElHelbawi** ([@melhelbawi](https://github.com/melhelbawi))
- **Aarkam Team** ([aarkam.io](https://aarkam.io/))

Repository: [https://github.com/melhelbawi/Aarkam.DDD.Repositories](https://github.com/melhelbawi/Aarkam.DDD.Repositories)  
NuGet: [https://www.nuget.org/packages/Aarkam.DDD.Repositories.Sql/](https://www.nuget.org/packages/Aarkam.DDD.Repositories.Sql/)

## 📄 License

This project is licensed under the **[MIT License](https://github.com/melhelbawi/Aarkam.DDD.Repositories/blob/main/LICENSE)**.
