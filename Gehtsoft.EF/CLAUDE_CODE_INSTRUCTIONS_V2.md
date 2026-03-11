# Gehtsoft.EF Framework - Instructions for Assisted Coding (V2)

## Purpose

This document is for assisted coding in projects that **use** Gehtsoft.EF as a dependency.

It is not a contributor guide for the Gehtsoft.EF repository itself. Prefer public APIs, stable usage patterns, and integration-focused examples.

## What Gehtsoft.EF Provides

Gehtsoft.EF is a lightweight ORM/data-access framework for .NET with:

- Entity mapping via attributes
- SQL database access through provider-specific packages
- Fluent query APIs for CRUD and filtering
- Automatic schema creation/update for supported changes
- Optional async APIs
- Optional patch-based schema evolution for changes automatic updates cannot handle

Main package areas:

- `Gehtsoft.EF.Entities`
- `Gehtsoft.EF.Db.SqlDb`
- One or more SQL provider packages such as:
  - `Gehtsoft.EF.Db.SqliteDb`
  - `Gehtsoft.EF.Db.MssqlDb`
  - `Gehtsoft.EF.Db.PostgresDb`
  - `Gehtsoft.EF.Db.MysqlDb`
  - `Gehtsoft.EF.Db.OracleDb`

## Recommended Defaults

When generating code for a consumer project:

- Prefer `ISqlDbConnectionFactory` for dependency injection
- Prefer generic APIs such as `GetSelectEntitiesQuery<T>()`
- Use `nameof(...)` for property references
- Dispose connections, queries, and transactions
- Specify `Size` for string properties
- Use `CreateEntityController.UpdateMode.Update` for routine schema sync
- Use patches for renames, type changes, and non-trivial data migrations

## Installation

Typical package set:

```xml
<ItemGroup>
  <PackageReference Include="Gehtsoft.EF.Entities" Version="..." />
  <PackageReference Include="Gehtsoft.EF.Db.SqlDb" Version="..." />
  <PackageReference Include="Gehtsoft.EF.Db.SqliteDb" Version="..." />
</ItemGroup>
```

Swap the provider package as needed:

- SQL Server: `Gehtsoft.EF.Db.MssqlDb`
- PostgreSQL: `Gehtsoft.EF.Db.PostgresDb`
- MySQL/MariaDB: `Gehtsoft.EF.Db.MysqlDb`
- Oracle: `Gehtsoft.EF.Db.OracleDb`

## Entity Definition

Basic entity:

```csharp
using Gehtsoft.EF.Entities;

[Entity(Table = "users", Scope = "app")]
public class User
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(Field = "email", Size = 200, Unique = true)]
    public string Email { get; set; }

    [EntityProperty(Field = "display_name", Size = 100)]
    public string DisplayName { get; set; }

    [EntityProperty(Field = "created_at")]
    public DateTime CreatedAt { get; set; }

    [EntityProperty(Field = "is_active")]
    public bool IsActive { get; set; }
}
```

Important rules:

- Use `[AutoId]` for the common integer identity primary key case
- Use `[PrimaryKey]` for a non-autoincrement primary key
- Use `[ForeignKey]` on entity-typed navigation/reference properties
- Nullable value types (`int?`, `DateTime?`) are auto-detected — no attribute needed
- Use `Nullable = true` for nullable string or entity-reference columns
- Set `Size` for strings and binary payloads
- Do not over-specify `DbType` unless inference would be unclear

Foreign key example:

```csharp
[Entity(Table = "orders", Scope = "app")]
public class Order
{
    [AutoId]
    public int Id { get; set; }

    [ForeignKey(Field = "user_id")]
    public User User { get; set; }

    [EntityProperty(Field = "total", DbType = DbType.Decimal, Precision = 2)]
    public decimal Total { get; set; }
}
```

## Connection Setup

### Dependency Injection

Recommended DI registration:

```csharp
using Gehtsoft.EF.Db.SqlDb;

services.AddSingleton<ISqlDbConnectionFactory>(
    new SqlDbUniversalConnectionFactory(
        configuration["Database:Driver"],
        configuration["Database:ConnectionString"]
    )
);
```

Supported driver names:

- `sqlite`
- `mssql`
- `npgsql` (also accepted as `postgres`)
- `mysql`
- `oracle`

Driver constants are also available: `UniversalSqlDbFactory.SQLITE`, `.MSSQL`, `.POSTGRES`, `.MYSQL`, `.ORACLE`.

Configuration example:

```json
{
  "Database": {
    "Driver": "sqlite",
    "ConnectionString": "Data Source=app.db"
  }
}
```

Example connection strings:

- SQLite: `Data Source=app.db`
- SQL Server: `Server=localhost;Database=mydb;User Id=sa;Password=***;TrustServerCertificate=True`
- PostgreSQL: `Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=***`
- MySQL: `Server=localhost;Database=mydb;Uid=root;Pwd=***`

### Connection Lifetime

- Connection factory: register as **Singleton**
- Repositories/services: register as **Scoped**
- Connections: short-lived, always wrap in `using`

### Direct Provider Usage

If DI is not needed:

```csharp
using Gehtsoft.EF.Db.SqliteDb;

using var connection = SqliteDbConnectionFactory.Create("Data Source=app.db");
```

## Schema Initialization

Use `CreateEntityController` to create/update tables for assemblies that contain your entities.

```csharp
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

public static void InitializeDatabase(ISqlDbConnectionFactory factory)
{
    var controller = new CreateEntityController(typeof(User).Assembly, "app");

    using var connection = factory.GetConnection();
    controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
}
```

Update modes:

- `Update`: create missing tables and apply supported column add/drop changes
- `Recreate`: drop and recreate tables
- `CreateNew`: create only missing tables

Additional methods:

- `controller.CreateTables(connection)`: create all tables without update logic
- `controller.DropTables(connection)`: drop all managed tables

Important limitation:

Automatic update does **not** safely cover every schema change. For example:

- Column rename
- Type change
- Complex index changes
- Data backfills tied to schema changes

For those, prefer patches.

## CRUD Patterns

### Select One by Primary Key

Shorthand when selecting by primary key:

```csharp
public User GetById(int id)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectOneEntityQuery<User>(id))
    {
        return query.ReadOne<User>();
    }
}
```

### Select One with Filter

```csharp
public User GetByEmail(string email)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Email)).Eq(email);
        return query.ReadOne<User>();
    }
}
```

Note:

- `ReadOne()` and `ReadAll()` will execute the query automatically if needed
- Calling `Execute()` explicitly is also valid when it improves readability

### Select Many

```csharp
public EntityCollection<User> GetActiveUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.AddOrderBy(nameof(User.DisplayName));
        return query.ReadAll<User>();
    }
}
```

### Insert

```csharp
public void Create(User user)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetInsertEntityQuery<User>())
    {
        query.Execute(user);
        // After execution, user.Id is populated with the auto-generated value
    }
}
```

### Update

```csharp
public void Update(User user)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetUpdateEntityQuery<User>())
    {
        query.Execute(user);
    }
}
```

### Delete

```csharp
public void Delete(User user)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetDeleteEntityQuery<User>())
    {
        query.Execute(user);
    }
}
```

### Bulk Delete with Condition

```csharp
public void DeleteInactiveUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetMultiDeleteEntityQuery<User>())
    {
        query.Where.Property(nameof(User.IsActive)).Eq(false);
        query.Execute();
    }
}
```

A `GetMultiUpdateEntityQuery<T>()` is also available for conditional bulk updates.

### Count

```csharp
public int CountActiveUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesCountQuery<User>())
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.Execute();
        return query.RowCount;
    }
}
```

## Filtering and Query Tips

Prefer:

- `nameof(User.Email)` over `"Email"`
- Generic query methods over `typeof(User)` overloads when the type is known at compile time
- Simple query APIs first, then generic/custom resultset APIs only when needed

### Condition Shorthand

Value is bound automatically:

```csharp
query.Where.Property(nameof(User.Email)).Eq(email);
query.Where.Property(nameof(User.DisplayName)).Like($"%{search}%");
query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
query.Skip = page * pageSize;
query.Limit = pageSize;   // .Take is also accepted as an alias for .Limit
```

### Comparison Operators

`Eq`, `Neq`, `Gt`, `Ge`, `Ls`, `Le`, `Like`, `In`, `NotIn`

IsNull/IsNotNull via `CmpOp`:

```csharp
query.Where.Property(nameof(User.Bio)).Is(CmpOp.IsNull);
```

### Explicit Value/Parameter Binding

More flexible — needed for references and subqueries:

```csharp
query.Where.Property(nameof(User.Id)).Eq().Value(id);
query.Where.Property(nameof(User.Email)).Eq().Parameter("email");
```

### OR Conditions

```csharp
query.Where.Property(nameof(User.DisplayName)).Like($"%{term}%");
query.Where.Or().Property(nameof(User.Email)).Like($"%{term}%");
```

Multiple `Where.Property(...)` calls without `.Or()` are combined with AND.

### Related-Entity Filtering

Use `PropertyOf<T>` when the property belongs to a joined entity type:

```csharp
query.Where.PropertyOf<User>(nameof(User.Email)).Like("%@example.com");
```

### Ordering and Pagination

```csharp
query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
query.Skip = page * pageSize;
query.Limit = pageSize;
```

## Subqueries

### IN Subquery

```csharp
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
    {
        subquery.Distinct = true;
        subquery.AddToResultset(nameof(Order.User));
        query.Where.Property(nameof(User.Id)).In(subquery);
    }
    return query.ReadAll<User>();
}
```

### NOT EXISTS with Cross-Reference

```csharp
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
    {
        var userIdRef = query.GetReference(nameof(User.Id));
        subquery.Where.Property(nameof(Order.User)).Eq().Reference(userIdRef);
        query.Where.Add().Is(CmpOp.NotExists).Query(subquery);
    }
    return query.ReadAll<User>();
}
```

## Aggregate and Custom Resultset Queries

Use `GetGenericSelectEntityQuery<T>()` for projections, joins, and aggregates:

```csharp
using (var query = connection.GetGenericSelectEntityQuery<Order>())
{
    query.AddEntity<User>();

    query.AddToResultset(typeof(User), nameof(User.DisplayName), "user_name");
    query.AddToResultset(AggFn.Count, null, "order_count");
    query.AddToResultset(AggFn.Sum, nameof(Order.Total), "total_amount");

    query.AddGroupBy(typeof(User), nameof(User.DisplayName));
    query.Execute();
    return query.ReadAllDynamic();
}
```

## Transactions

```csharp
public void Transfer(User source, User target)
{
    using var connection = mFactory.GetConnection();
    using var transaction = connection.BeginTransaction();

    try
    {
        using (var query = connection.GetUpdateEntityQuery<User>())
        {
            query.Execute(source);
        }

        using (var query = connection.GetUpdateEntityQuery<User>())
        {
            query.Execute(target);
        }

        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

## Async Usage

Use async variants in ASP.NET Core services or other async flows:

```csharp
public async Task<User> GetByIdAsync(int id, CancellationToken token = default)
{
    using var connection = await mFactory.GetConnectionAsync(token);

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Id)).Eq(id);
        return await query.ReadOneAsync<User>(token);
    }
}
```

Other common async methods:

- `await query.ExecuteAsync()` / `await query.ExecuteNoDataAsync()`
- `await query.ReadAllAsync<T>(token)`
- `await transaction.CommitAsync()` / `await transaction.RollbackAsync()`

## Patches for Schema Evolution

Use patches when automatic schema update is not enough.

Common cases:

- Renaming columns
- Splitting/merging data
- Backfilling values
- Multi-step schema transitions

Patch discovery and application:

```csharp
using Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch;

var patches = EfPatchProcessor.FindAllPatches(new[] { typeof(User).Assembly }, "app");

using var connection = factory.GetConnection();
connection.ApplyPatches(patches, "app");
```

Async variant:

```csharp
await connection.ApplyPatchesAsync(patches, "app");
```

Patch declaration:

```csharp
[EfPatch("app", 1, 0, 1)]
public class Patch1001 : IEfPatch
{
    public void Apply(SqlDbConnection connection)
    {
        // Execute schema/data migration logic here
    }
}
```

For async patch logic, implement `IEfPatchAsync` instead:

```csharp
[EfPatch("app", 1, 0, 2)]
public class Patch1002 : IEfPatchAsync
{
    public void Apply(SqlDbConnection connection) => ApplyAsync(connection).GetAwaiter().GetResult();

    public async Task ApplyAsync(SqlDbConnection connection)
    {
        // Async schema/data migration logic here
    }
}
```

## Raw SQL

Use raw SQL when the high-level API is not the best fit.

### Writing

```csharp
using (var query = connection.GetQuery("UPDATE users SET is_active = @active WHERE id = @id"))
{
    query.BindParam("active", true);
    query.BindParam("id", id);
    query.ExecuteNoData();
}
```

### Reading

```csharp
using (var query = connection.GetQuery("SELECT id, name FROM users WHERE name LIKE @mask"))
{
    query.BindParam("mask", "J%");
    query.ExecuteReader();

    while (query.ReadNext())
    {
        int id = query.GetValue<int>(0);            // by column index
        string name = query.GetValue<string>("name"); // by column name
    }
}
```

Prefer parameter binding over string interpolation.

## Consumer-Focused Best Practices

- Keep connections short-lived
- Dispose every connection/query/transaction
- Validate public inputs at your service boundary
- Handle `null` from `ReadOne<T>()`
- Use patches instead of forcing risky auto-update scenarios
- Keep entity classes simple and persistence-focused
- Use explicit transactions for multi-step write operations
- Prefer public framework APIs over internal implementation assumptions

## Common Mistakes

### Missing `Size` on strings

```csharp
// Avoid
[EntityProperty(Field = "name")]
public string Name { get; set; }

// Prefer
[EntityProperty(Field = "name", Size = 100)]
public string Name { get; set; }
```

### Using scalar ID instead of entity reference for foreign keys

```csharp
// Avoid
[EntityProperty(Field = "user_id", ForeignKey = true)]
public int UserId { get; set; }

// Prefer
[ForeignKey(Field = "user_id")]
public User User { get; set; }
```

### Forgetting disposal

```csharp
using var connection = mFactory.GetConnection();
using var query = connection.GetSelectEntitiesQuery<User>();
```

## Suggested Assistant Behavior

When helping in a project that consumes Gehtsoft.EF:

- Generate application code, not framework-internal code
- Prefer stable public APIs and simple examples
- Ask which database provider is being used if it changes package/configuration choices
- Default to `ISqlDbConnectionFactory` for DI-based apps
- Suggest patches when the requested schema change exceeds `CreateEntityController.UpdateTables` capabilities
- Keep examples production-usable rather than test-oriented
