# Gehtsoft.EF Framework Skill

This project uses the Gehtsoft.EF ORM framework. Follow the patterns below when generating data-access code.

## Packages

Always include the core packages and one or more driver packages:

```xml
<ItemGroup>
  <PackageReference Include="Gehtsoft.EF.Entities" Version="..." />
  <PackageReference Include="Gehtsoft.EF.Db.SqlDb" Version="..." />
  <!-- Pick a driver -->
  <PackageReference Include="Gehtsoft.EF.Db.SqliteDb" Version="..." />
</ItemGroup>
```

Other drivers: `Gehtsoft.EF.Db.MssqlDb`, `Gehtsoft.EF.Db.PostgresDb`, `Gehtsoft.EF.Db.MysqlDb`, `Gehtsoft.EF.Db.OracleDb`.

## Entity Definition

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

### Attribute Rules

- `[AutoId]` — auto-increment integer primary key (shorthand for `PrimaryKey = true, Autoincrement = true`)
- `[PrimaryKey]` — non-autoincrement primary key
- `[ForeignKey]` — foreign key; property type must be the referenced entity, not a scalar ID
- `[EntityProperty]` — common parameters: `Field`, `Size` (required for strings/binary), `Nullable`, `DbType`, `Precision`, `Unique`, `Sorted`, `DefaultValue`
- `[Entity]` — parameters: `Table`, `Scope`, `NamingPolicy`, `View`, `Metadata`
- Use `Nullable = true` for nullable reference-type columns
- Do not over-specify `DbType` unless inference would be unclear

### Foreign Key

```csharp
[Entity(Table = "orders", Scope = "app")]
public class Order
{
    [AutoId]
    public int Id { get; set; }

    [ForeignKey(Field = "user_id")]
    public User User { get; set; }

    [ForeignKey(Field = "parent_id", Nullable = true)]
    public Order Parent { get; set; }

    [EntityProperty(Field = "total", DbType = DbType.Decimal, Precision = 2)]
    public decimal Total { get; set; }
}
```

### Nullable Properties

```csharp
// Nullable value types are auto-detected — no need for Nullable attribute
[EntityProperty(Field = "age")]
public int? Age { get; set; }

// Nullable strings require explicit Nullable = true
[EntityProperty(Field = "bio", Size = 1000, Nullable = true)]
public string Bio { get; set; }
```

## Connection Setup

### Dependency Injection (recommended)

```csharp
using Gehtsoft.EF.Db.SqlDb;

services.AddSingleton<ISqlDbConnectionFactory>(
    new SqlDbUniversalConnectionFactory(
        configuration["Database:Driver"],
        configuration["Database:ConnectionString"]
    )
);
```

Driver names: `sqlite`, `mssql`, `npgsql` (also `postgres`), `mysql`, `oracle`.

Constants are also available: `UniversalSqlDbFactory.SQLITE`, `.MSSQL`, `.POSTGRES`, `.MYSQL`, `.ORACLE`.

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

### Direct Provider Usage

```csharp
using Gehtsoft.EF.Db.SqliteDb;

using var connection = SqliteDbConnectionFactory.Create("Data Source=app.db");
```

### Lifetime Guidance

- Connection factory: **Singleton**
- Repositories/services: **Scoped**
- Connections: short-lived, always wrap in `using`

## Schema Initialization

```csharp
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

var controller = new CreateEntityController(typeof(User).Assembly, "app");

using var connection = factory.GetConnection();
controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
```

Update modes:

- `Update` — create missing tables, add/drop columns on existing tables
- `Recreate` — drop and recreate tables (data loss)
- `CreateNew` — only create tables that don't exist

Additional methods: `controller.CreateTables(connection)`, `controller.DropTables(connection)`.

Automatic update does **not** handle column renames, type changes, complex index changes, or data backfills. Use patches for those.

## CRUD Patterns

### Select One by Primary Key

```csharp
using (var query = connection.GetSelectOneEntityQuery<User>(id))
{
    return query.ReadOne<User>();
}
```

### Select One with Filter

```csharp
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Where.Property(nameof(User.Email)).Eq(email);
    return query.ReadOne<User>();
}
```

`ReadOne<T>()` and `ReadAll<T>()` execute the query automatically. Calling `Execute()` explicitly is also valid.

### Select Many

```csharp
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Where.Property(nameof(User.IsActive)).Eq(true);
    query.AddOrderBy(nameof(User.DisplayName));
    return query.ReadAll<User>();  // returns EntityCollection<User>
}
```

To return a custom collection type: `query.ReadAll<UserCollection, User>()`.

### Insert

```csharp
using (var query = connection.GetInsertEntityQuery<User>())
{
    query.Execute(user);
    // user.Id is now populated with the auto-generated value
}
```

### Update

```csharp
using (var query = connection.GetUpdateEntityQuery<User>())
{
    query.Execute(user);
}
```

### Delete

```csharp
using (var query = connection.GetDeleteEntityQuery<User>())
{
    query.Execute(user);
}
```

### Bulk Delete with Condition

```csharp
using (var query = connection.GetMultiDeleteEntityQuery<User>())
{
    query.Where.Property(nameof(User.IsActive)).Eq(false);
    query.Execute();
}
```

`GetMultiUpdateEntityQuery<T>()` is also available for conditional bulk updates.

### Count

```csharp
using (var query = connection.GetSelectEntitiesCountQuery<User>())
{
    query.Where.Property(nameof(User.IsActive)).Eq(true);
    query.Execute();
    return query.RowCount;
}
```

## Filtering

### Condition Shorthand

Value is bound automatically:

```csharp
query.Where.Property(nameof(User.Email)).Eq(email);
query.Where.Property(nameof(User.DisplayName)).Like($"%{search}%");
query.Where.Property(nameof(User.CreatedAt)).Gt(date);
```

### Explicit Value/Parameter Binding

More flexible — needed for references and subqueries:

```csharp
query.Where.Property(nameof(User.Id)).Eq().Value(id);
query.Where.Property(nameof(User.Email)).Eq().Parameter("email");
```

### Comparison Operators

`Eq`, `Neq`, `Gt`, `Ge`, `Ls`, `Le`, `Like`, `In`, `NotIn`

IsNull/IsNotNull via `CmpOp`:

```csharp
query.Where.Property(nameof(User.Bio)).Is(CmpOp.IsNull);
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
query.Limit = pageSize;   // .Take is also accepted as an alias
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

## Aggregate / Custom Resultset Queries

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
using var connection = mFactory.GetConnection();
using var transaction = connection.BeginTransaction();

try
{
    using (var query = connection.GetUpdateEntityQuery<User>())
        query.Execute(source);

    using (var query = connection.GetUpdateEntityQuery<User>())
        query.Execute(target);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

## Async Usage

```csharp
using var connection = await mFactory.GetConnectionAsync(token);

using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Where.Property(nameof(User.Id)).Eq(id);
    return await query.ReadOneAsync<User>(token);
}
```

Other async methods:

- `await query.ExecuteAsync()` / `await query.ExecuteNoDataAsync()`
- `await query.ReadAllAsync<T>(token)`
- `await transaction.CommitAsync()` / `await transaction.RollbackAsync()`

## Patches for Schema Evolution

Use patches when automatic update cannot handle the change (renames, type changes, data backfills).

### Apply Patches

```csharp
using Gehtsoft.EF.Db.SqlDb.EntityQueries.CreateEntity.Patch;

var patches = EfPatchProcessor.FindAllPatches(new[] { typeof(User).Assembly }, "app");
connection.ApplyPatches(patches, "app");
// or: await connection.ApplyPatchesAsync(patches, "app");
```

### Declare a Patch

```csharp
[EfPatch("app", 1, 0, 1)]
public class Patch1001 : IEfPatch
{
    public void Apply(SqlDbConnection connection)
    {
        // migration logic
    }
}
```

For async logic, implement `IEfPatchAsync`:

```csharp
[EfPatch("app", 1, 0, 2)]
public class Patch1002 : IEfPatchAsync
{
    public void Apply(SqlDbConnection connection) => ApplyAsync(connection).GetAwaiter().GetResult();

    public async Task ApplyAsync(SqlDbConnection connection)
    {
        // async migration logic
    }
}
```

## Raw SQL

```csharp
using (var query = connection.GetQuery("SELECT id, name FROM users WHERE name LIKE @mask"))
{
    query.BindParam("mask", "J%");
    query.ExecuteReader();

    while (query.ReadNext())
    {
        int id = query.GetValue<int>(0);
        string name = query.GetValue<string>("name");
    }
}
```

For writes:

```csharp
using (var query = connection.GetQuery("UPDATE users SET is_active = @active WHERE id = @id"))
{
    query.BindParam("active", true);
    query.BindParam("id", id);
    query.ExecuteNoData();
}
```

Always use parameter binding — never string interpolation.

## Rules

- Always use `nameof(...)` for property references
- Always dispose connections, queries, and transactions (`using`)
- Always set `Size` on string properties
- Use `[ForeignKey]` with entity-typed properties, not scalar IDs
- Handle `null` from `ReadOne<T>()` — it returns null when not found
- Keep connections short-lived
- Prefer generic query methods (`GetSelectEntitiesQuery<T>()`) over `typeof()` overloads
- Use explicit transactions for multi-step write operations
- Use patches for schema changes that `UpdateTables` cannot handle
