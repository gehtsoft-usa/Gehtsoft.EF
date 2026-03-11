# Gehtsoft.EF Framework - Instructions for Claude Code

## Overview

Gehtsoft.EF is a lightweight, database-agnostic ORM framework for .NET. This document provides comprehensive instructions for Claude Code when working with projects that use this framework.

## Core Concepts

### Architecture

The framework consists of multiple packages:

1. **Gehtsoft.EF.Entities** - Entity definitions and metadata
   - Contains attributes for defining entities (`EntityAttribute`, `EntityPropertyAttribute`)
   - Provides base collection types (`EntityCollection<T>`)
   - Defines entity discovery and metadata interfaces

2. **Gehtsoft.EF.Db.SqlDb** - Core database operations and query execution
   - Connection management (`SqlDbConnection`, `ISqlDbConnectionFactory`)
   - Query builders and execution (`SelectEntitiesQuery`, `ModifyEntityQuery`)
   - Schema management (`CreateEntityController`)
   - Platform-independent query builders

3. **Database-Specific Driver Packages** (add to your project as needed):
   - **Gehtsoft.EF.Db.SqliteDb** - SQLite support
   - **Gehtsoft.EF.Db.MssqlDb** - Microsoft SQL Server support
   - **Gehtsoft.EF.Db.PostgresDb** - PostgreSQL support
   - **Gehtsoft.EF.Db.MysqlDb** - MySQL/MariaDB support
   - **Gehtsoft.EF.Db.OracleDb** - Oracle support

### Database Driver Selection

**You must add the appropriate driver package for your database:**

```xml
<!-- In your .csproj file -->
<ItemGroup>
  <!-- Core packages (always required) -->
  <PackageReference Include="Gehtsoft.EF.Entities" Version="..." />
  <PackageReference Include="Gehtsoft.EF.Db.SqlDb" Version="..." />

  <!-- Add ONE OR MORE database-specific drivers -->
  <PackageReference Include="Gehtsoft.EF.Db.SqliteDb" Version="..." />
  <!-- OR -->
  <PackageReference Include="Gehtsoft.EF.Db.MssqlDb" Version="..." />
  <!-- OR -->
  <PackageReference Include="Gehtsoft.EF.Db.PostgresDb" Version="..." />
  <!-- OR -->
  <PackageReference Include="Gehtsoft.EF.Db.MysqlDb" Version="..." />
  <!-- OR -->
  <PackageReference Include="Gehtsoft.EF.Db.OracleDb" Version="..." />
</ItemGroup>
```

### Key Design Patterns

1. **Connection Factory Pattern** - Always use `ISqlDbConnectionFactory` for DI
2. **Builder Pattern** - Queries are constructed using fluent builder APIs
3. **Type-safe Property References** - Use `nameof()` for property names
4. **Disposable Resources** - All queries and connections must be disposed
5. **Connection Pooling** - ADO.NET connection pooling is automatic, keep connections short-lived

---

## Entity Definition

### Basic Entity Structure

```csharp
[Entity(Table = "table_name", Scope = "optional_scope")]
public class EntityName
{
    [EntityProperty(Field = "id", Autoincrement = true, PrimaryKey = true)]
    public int Id { get; set; }

    [EntityProperty(Field = "name", Size = 128)]
    public string Name { get; set; }

    [EntityProperty(Field = "created_at")]
    public DateTime CreatedAt { get; set; }
}
```

### EntityAttribute Properties

- **Table** - Database table name (if omitted, generated from entity name)
- **Scope** - Logical grouping of entities (optional)
- **View** - Set to `true` if entity represents a view (default: `false`)
- **NamingPolicy** - Controls field naming conventions (default: `EntityNamingPolicy.Default`)
- **Metadata** - Optional metadata type for composite indexes or view creation

### EntityPropertyAttribute Properties

**Required/Common:**
- **Field** - Database column name (if omitted, generated from property name)
- **Size** - Maximum size for string/binary fields (required for strings)
- **Autoincrement** - Mark auto-increment columns (typically with PrimaryKey)
- **PrimaryKey** - Mark primary key fields
- **ForeignKey** - Mark foreign key relationships (property type must be the referenced entity)
- **Sorted** - Create an index on this field
- **Unique** - Mark field as unique
- **Nullable** - Mark field as nullable (auto-detected for nullable value types)

**Advanced:**
- **DbType** - Explicit database type (only for non-obvious types like `DbType.Binary`, `DbType.Decimal`)
- **Precision** - Decimal places for numeric types
- **DefaultValue** - Default value for the column
- **IgnoreRead** - Exclude from automatic read operations

### Common Entity Patterns

#### Auto-increment Primary Key

Use the `[AutoId]` attribute (shorthand for Autoincrement + PrimaryKey):

```csharp
[Entity(Table = "users")]
public class User
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(Field = "username", Size = 50)]
    public string Username { get; set; }
}
```

#### Foreign Key Relationships

```csharp
[Entity(Table = "orders")]
public class Order
{
    [AutoId]
    public int Id { get; set; }

    // Foreign key to User entity
    [ForeignKey(Field = "user_id")]
    public User User { get; set; }

    // Nullable foreign key
    [ForeignKey(Field = "parent_order_id", Nullable = true)]
    public Order ParentOrder { get; set; }
}
```

#### Entity Collections

Define collection types for query results:

```csharp
public class UserCollection : EntityCollection<User>
{
}
```

#### Nullable Properties

```csharp
[Entity(Table = "profiles")]
public class Profile
{
    [AutoId]
    public int Id { get; set; }

    // Nullable value type - no need to specify Nullable attribute
    [EntityProperty(Field = "age")]
    public int? Age { get; set; }

    // Nullable string - specify Nullable attribute
    [EntityProperty(Field = "bio", Size = 1000, Nullable = true)]
    public string Bio { get; set; }
}
```

---

## Connection Management

### Connection Factory Setup

**Always use `ISqlDbConnectionFactory` for dependency injection.**

#### Using UniversalSqlDbFactory (Recommended)

The `UniversalSqlDbFactory` allows database-agnostic connection creation using driver names:

```csharp
// In Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register connection factory using UniversalSqlDbFactory
    string driver = Configuration["Database:Driver"];        // e.g., "sqlite", "mssql", "npgsql", "mysql", "oracle"
    string connectionString = Configuration["Database:ConnectionString"];

    services.AddSingleton<ISqlDbConnectionFactory>(
        new SqlDbUniversalConnectionFactory(driver, connectionString)
    );

    // Register data access boundaries
    services.AddScoped<IUserRepository, UserRepository>();
}
```

**Supported driver names:**
- `UniversalSqlDbFactory.SQLITE` or `"sqlite"` - SQLite
- `UniversalSqlDbFactory.MSSQL` or `"mssql"` - Microsoft SQL Server
- `UniversalSqlDbFactory.POSTGRES` or `"npgsql"` - PostgreSQL
- `UniversalSqlDbFactory.MYSQL` or `"mysql"` - MySQL/MariaDB
- `UniversalSqlDbFactory.ORACLE` or `"oracle"` - Oracle

#### Using Database-Specific Connection Factories

Alternatively, use database-specific connection factories directly:

```csharp
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Db.MssqlDb;
using Gehtsoft.EF.Db.PostgresDb;

// SQLite
var connection = SqliteDbConnectionFactory.Create("Data Source=myapp.db");

// MS SQL
var connection = MssqlDbConnectionFactory.Create("Server=localhost;Database=mydb;...");

// PostgreSQL
var connection = PostgresDbConnectionFactory.Create("Host=localhost;Database=mydb;...");
```

#### Configuration File

```json
{
  "Database": {
    "Driver": "sqlite",
    "ConnectionString": "Data Source=myapp.db"
  }
}
```

**Example connection strings:**

```json
// SQLite
"Data Source=myapp.db"

// MS SQL
"Server=localhost;Database=mydb;User Id=sa;Password=***;TrustServerCertificate=True"

// PostgreSQL
"Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=***"

// MySQL
"Server=localhost;Database=mydb;Uid=root;Pwd=***"

// Oracle
"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=mydb)));User Id=system;Password=***"
```

### Using Connection Factory in Repositories

```csharp
public class UserRepository : IUserRepository
{
    private readonly ISqlDbConnectionFactory mFactory;
    private readonly ILogger<UserRepository> mLogger;

    public UserRepository(
        ISqlDbConnectionFactory factory,
        ILogger<UserRepository> logger)
    {
        mFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        mLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public User GetById(int id)
    {
        using var connection = mFactory.GetConnection();

        using (var query = connection.GetSelectEntitiesQuery<User>())
        {
            query.Where.Property(nameof(User.Id)).Eq(id);
            query.Execute();
            return query.ReadOne<User>();
        }
    }
}
```

### Connection Lifetime

- Connection factory is typically **Singleton**
- Repositories/Boundaries are typically **Scoped**
- Always use `using var connection = mFactory.GetConnection()` to ensure disposal

---

## Schema Management

### Creating/Updating Database Schema

Use `CreateEntityController` to create or update database tables automatically.

```csharp
public void InitializeDatabase(ISqlDbConnectionFactory factory)
{
    // Create controller with assemblies containing entities
    var controller = new CreateEntityController(
        new[] { typeof(User).Assembly },  // Assembly containing entities
        "myapp"                            // Scope (optional)
    );

    using var connection = factory.GetConnection();

    // Update tables (creates new, alters existing, preserves data)
    controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
}
```

### Update Modes

- **UpdateMode.Update** - Creates new tables and updates existing tables when columns are added or dropped (recommended)
- **UpdateMode.Recreate** - Drops and recreates tables (data loss)
- **UpdateMode.CreateNew** - Only creates new tables, doesn't alter existing ones

### Complete Initialization Example

```csharp
public void InitializeDatabase(ISqlDbConnectionFactory factory)
{
    var controller = new CreateEntityController(
        new[] { this.GetType().Assembly },
        "myapp"
    );

    using var connection = factory.GetConnection();

    controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
}
```

---

## Query Operations

### SELECT Queries

#### Generic Method Signatures

The framework provides generic and non-generic versions of query methods:

```csharp
// Generic (type-safe, recommended)
connection.GetSelectEntitiesQuery<User>()
connection.GetSelectEntitiesCountQuery<User>()
connection.GetInsertEntityQuery<User>()
connection.GetUpdateEntityQuery<User>()
connection.GetDeleteEntityQuery<User>()

// Non-generic (when type is dynamic)
connection.GetSelectEntitiesQuery(typeof(User))
connection.GetSelectEntitiesCountQuery(typeof(User))
connection.GetInsertEntityQuery(typeof(User))
connection.GetUpdateEntityQuery(typeof(User))
connection.GetDeleteEntityQuery(typeof(User))
```

#### Select Single Entity by ID

```csharp
public User GetById(int id)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Id)).Eq(id);
        query.Execute();
        return query.ReadOne<User>();
    }
}
```

#### Select Single Entity by Property

```csharp
public User FindByEmail(string email)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Email)).Eq(email);
        query.Execute();
        return query.ReadOne<User>();
    }
}
```

#### Select Multiple Entities

```csharp
public UserCollection GetActiveUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.AddOrderBy(nameof(User.Name));
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}

// Or using default EntityCollection<T>
public EntityCollection<User> GetActiveUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.AddOrderBy(nameof(User.Name));
        query.Execute();
        return query.ReadAll<User>();  // Uses EntityCollection<User>
    }
}
```

#### Select with LIKE Pattern

```csharp
public UserCollection SearchByName(string nameMask)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Name)).Like($"%{nameMask}%");
        query.AddOrderBy(nameof(User.Name));
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### Select with Pagination

```csharp
public UserCollection GetUsersPaged(int pageIndex, int pageSize)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.AddOrderBy(nameof(User.Name));
        query.Skip = pageIndex * pageSize;
        query.Limit = pageSize;
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### Select with Sorting

```csharp
public UserCollection GetUsersSorted(bool descending = false)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        if (descending)
            query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
        else
            query.AddOrderBy(nameof(User.CreatedAt), SortDir.Asc);

        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### Select with Multiple Conditions (AND)

```csharp
public UserCollection GetActiveUsersCreatedAfter(DateTime date)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        // Multiple Where calls are combined with AND
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.Where.Property(nameof(User.CreatedAt)).Gt(date);
        query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### Select with OR Conditions

```csharp
public UserCollection SearchByNameOrEmail(string searchTerm)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Name)).Like($"%{searchTerm}%");
        query.Where.Or().Property(nameof(User.Email)).Like($"%{searchTerm}%");
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### Select with Complex OR Groups

```csharp
public UserCollection SearchUsers(string searchTerm, bool activeOnly)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        // (Name LIKE searchTerm OR Email LIKE searchTerm)
        query.Where.Property(nameof(User.Name)).Like($"%{searchTerm}%");
        query.Where.Or().Property(nameof(User.Email)).Like($"%{searchTerm}%");

        // AND IsActive = true
        if (activeOnly)
            query.Where.Property(nameof(User.IsActive)).Eq(true);

        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Comparison Operators

Available operators for Where conditions:

- **Eq** - Equal to (`=`)
- **NotEq** - Not equal to (`<>`)
- **Gt** - Greater than (`>`)
- **Gte** - Greater than or equal to (`>=`)
- **Lt** - Less than (`<`)
- **Lte** - Less than or equal to (`<=`)
- **Like** - SQL LIKE pattern matching
- **In** - Value in list or subquery
- **NotIn** - Value not in list or subquery

### COUNT Queries

#### Count All Entities

```csharp
public int CountUsers()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesCountQuery<User>())
    {
        query.Execute();
        return query.RowCount;
    }
}
```

#### Count with Conditions

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

### Subqueries

#### IN Subquery

```csharp
public UserCollection GetUsersWithOrders()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        // Subquery: SELECT DISTINCT user_id FROM orders
        using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
        {
            subquery.Distinct = true;
            subquery.AddToResultset(nameof(Order.User));

            query.Where.Property(nameof(User.Id)).In(subquery);
        }

        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

#### NOT IN Subquery

```csharp
public UserCollection GetUsersWithoutOrders()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
        {
            subquery.Distinct = true;
            subquery.AddToResultset(nameof(Order.User));

            query.Where.Property(nameof(User.Id)).NotIn(subquery);
        }

        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### INSERT Operations

#### Insert New Entity

```csharp
public void Create(User user)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetInsertEntityQuery<User>())
    {
        query.Execute(user);
        // After execution, user.Id is populated with auto-generated value
    }
}
```

### UPDATE Operations

#### Update Existing Entity

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

### DELETE Operations

#### Delete Single Entity

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

#### Delete by ID

```csharp
public void DeleteById(int id)
{
    var user = GetById(id);
    if (user != null)
    {
        Delete(user);
    }
}
```

#### Delete Multiple Entities with Conditions

```csharp
public void DeleteOrdersForUser(int userId)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetMultiDeleteEntityQuery<Order>())
    {
        query.Where.Property(nameof(Order.User)).Eq(userId);
        query.Execute();
    }
}
```

---

## Transactions

### Basic Transaction Pattern

```csharp
public void TransferData(User fromUser, User toUser)
{
    using var connection = mFactory.GetConnection();

    using (var transaction = connection.BeginTransaction())
    {
        try
        {
            using (var query = connection.GetUpdateEntityQuery<User>())
            {
                query.Execute(fromUser);
            }

            using (var query = connection.GetUpdateEntityQuery<User>())
            {
                query.Execute(toUser);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

### Transaction Best Practices

1. Always wrap transaction in `using` statement
2. Always call `Commit()` on success
3. Always call `Rollback()` in catch block (or let Dispose handle it)
4. Keep transactions as short as possible
5. Avoid external I/O operations within transactions

---

## Common Patterns

### Repository Pattern

```csharp
public interface IUserRepository
{
    User GetById(int id);
    UserCollection GetAll();
    UserCollection Search(string searchTerm, int pageIndex, int pageSize);
    int Count(string searchTerm);
    void Create(User user);
    void Update(User user);
    void Delete(int id);
}

public class UserRepository : IUserRepository
{
    private readonly ISqlDbConnectionFactory mFactory;
    private readonly ILogger<UserRepository> mLogger;

    public UserRepository(
        ISqlDbConnectionFactory factory,
        ILogger<UserRepository> logger)
    {
        mFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        mLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public User GetById(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be positive", nameof(id));

        try
        {
            using var connection = mFactory.GetConnection();

            using (var query = connection.GetSelectEntitiesQuery<User>())
            {
                query.Where.Property(nameof(User.Id)).Eq(id);
                query.Execute();
                return query.ReadOne<User>();
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to get user by id: {Id}", id);
            throw;
        }
    }

    public UserCollection Search(string searchTerm, int pageIndex, int pageSize)
    {
        if (pageIndex < 0)
            throw new ArgumentException("Page index must be non-negative", nameof(pageIndex));
        if (pageSize <= 0)
            throw new ArgumentException("Page size must be positive", nameof(pageSize));

        try
        {
            using var connection = mFactory.GetConnection();

            using (var query = connection.GetSelectEntitiesQuery<User>())
            {
                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query.Where.Property(nameof(User.Name)).Like($"%{searchTerm}%");
                    query.Where.Or().Property(nameof(User.Email)).Like($"%{searchTerm}%");
                }

                query.AddOrderBy(nameof(User.Name));
                query.Skip = pageIndex * pageSize;
                query.Limit = pageSize;
                query.Execute();
                return query.ReadAll<UserCollection, User>();
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to search users");
            throw;
        }
    }

    public void Create(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));

        try
        {
            using var connection = mFactory.GetConnection();

            using (var query = connection.GetInsertEntityQuery<User>())
            {
                query.Execute(user);
                mLogger.LogInformation("Created user with id: {Id}", user.Id);
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to create user");
            throw;
        }
    }

    public void Update(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));
        if (user.Id <= 0)
            throw new ArgumentException("User must have valid Id", nameof(user));

        try
        {
            using var connection = mFactory.GetConnection();

            using (var query = connection.GetUpdateEntityQuery<User>())
            {
                query.Execute(user);
                mLogger.LogInformation("Updated user with id: {Id}", user.Id);
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to update user");
            throw;
        }
    }

    public void Delete(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be positive", nameof(id));

        try
        {
            var user = GetById(id);
            if (user == null)
                return;

            using var connection = mFactory.GetConnection();

            using (var query = connection.GetDeleteEntityQuery<User>())
            {
                query.Execute(user);
                mLogger.LogInformation("Deleted user with id: {Id}", id);
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to delete user");
            throw;
        }
    }
}
```

### Find or Create Pattern

```csharp
public User FindOrCreate(string email, string name)
{
    var user = FindByEmail(email);

    if (user == null)
    {
        user = new User
        {
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        Create(user);
    }

    return user;
}
```

### Upsert Pattern

```csharp
public void Upsert(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));

    using var connection = mFactory.GetConnection();

    bool isUpdate = user.Id > 0;

    using (var query = isUpdate
        ? connection.GetUpdateEntityQuery<User>()
        : connection.GetInsertEntityQuery<User>())
    {
        query.Execute(user);
    }
}
```

### Cascade Delete Pattern

```csharp
public void DeleteUserWithOrders(int userId)
{
    using var connection = mFactory.GetConnection();

    using (var transaction = connection.BeginTransaction())
    {
        try
        {
            // Delete related orders first
            using (var query = connection.GetMultiDeleteEntityQuery<Order>())
            {
                query.Where.Property(nameof(Order.User)).Eq(userId);
                query.Execute();
            }

            // Then delete user
            var user = GetById(userId);
            if (user != null)
            {
                using (var query = connection.GetDeleteEntityQuery<User>())
                {
                    query.Execute(user);
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
```

---

## Best Practices

### 1. Always Dispose Resources

**Connections and queries MUST be disposed:**

```csharp
// ✅ CORRECT
using var connection = mFactory.GetConnection();

using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Execute();
    return query.ReadOne<User>();
}

// ❌ INCORRECT - Memory leak
var connection = mFactory.GetConnection();
var query = connection.GetSelectEntitiesQuery<User>();
query.Execute();
return query.ReadOne<User>();
```

### 2. Use nameof() for Property References

**Always use `nameof()` instead of string literals:**

```csharp
// ✅ CORRECT - Type-safe, refactor-friendly
query.Where.Property(nameof(User.Email)).Eq(email);

// ❌ INCORRECT - Brittle, error-prone
query.Where.Property("Email").Eq(email);
```

### 3. Validate Arguments

**Always validate public method arguments:**

```csharp
public User GetById(int id)
{
    if (id <= 0)
        throw new ArgumentException("Id must be positive", nameof(id));

    // Implementation...
}

public void Create(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));

    // Implementation...
}
```

### 4. Handle Null Returns

**SELECT queries return null if no results found:**

```csharp
public User GetById(int id)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Id)).Eq(id);
        query.Execute();
        return query.ReadOne<User>();  // Returns null if not found
    }
}

// Caller should check for null
var user = repository.GetById(123);
if (user == null)
{
    // Handle not found case
}
```

### 5. Use Logging

**Log important operations and errors:**

```csharp
try
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetInsertEntityQuery<User>())
    {
        query.Execute(user);
        mLogger.LogInformation("Created user: {Id}", user.Id);
    }
}
catch (Exception ex)
{
    mLogger.LogError(ex, "Failed to create user");
    throw;
}
```

### 6. Don't Over-Specify Entity Attributes

**Omit obvious types:**

```csharp
// ✅ GOOD - Framework infers types
[EntityProperty(Field = "id", Autoincrement = true, PrimaryKey = true)]
public int Id { get; set; }

[EntityProperty(Field = "name", Size = 128)]
public string Name { get; set; }

[EntityProperty(Field = "age")]
public int? Age { get; set; }

// ❌ UNNECESSARY - Over-specification
[EntityProperty(Field = "id", DbType = DbType.Int32, Autoincrement = true, PrimaryKey = true)]
public int Id { get; set; }

[EntityProperty(Field = "name", DbType = DbType.String, Size = 128)]
public string Name { get; set; }

[EntityProperty(Field = "age", DbType = DbType.Int32, Nullable = true)]
public int? Age { get; set; }
```

### 7. Subquery Disposal

**Subqueries also implement IDisposable:**

```csharp
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
    {
        subquery.Distinct = true;
        subquery.AddToResultset(nameof(Order.User));

        query.Where.Property(nameof(User.Id)).In(subquery);
    }

    query.Execute();
    return query.ReadAll<User>();
}
```

### 8. Use Generic Methods When Possible

**Generic methods are type-safe and preferred:**

```csharp
// ✅ PREFERRED - Type-safe
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    // ...
}

// ⚠️ USE ONLY WHEN TYPE IS DYNAMIC
using (var query = connection.GetSelectEntitiesQuery(typeof(User)))
{
    // ...
}
```

---

## Async Operations

### Async Query Execution

Most query operations have async equivalents:

```csharp
public async Task<User> GetByIdAsync(int id, CancellationToken cancellationToken = default)
{
    using var connection = await mFactory.GetConnectionAsync(cancellationToken);

    using (var query = connection.GetSelectEntitiesQuery<User>())
    {
        query.Where.Property(nameof(User.Id)).Eq(id);
        await query.ExecuteAsync(cancellationToken);
        return await query.ReadOneAsync<User>(cancellationToken);
    }
}

public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
{
    using var connection = await mFactory.GetConnectionAsync(cancellationToken);

    using (var query = connection.GetInsertEntityQuery<User>())
    {
        await query.ExecuteAsync(user, cancellationToken);
    }
}
```

---

## Common Mistakes to Avoid

### ❌ Mistake 1: Not Disposing Queries

```csharp
// WRONG - Memory leak
var query = connection.GetSelectEntitiesQuery<User>();
query.Execute();
return query.ReadOne<User>();

// CORRECT
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Execute();
    return query.ReadOne<User>();
}
```

### ❌ Mistake 2: Using String Literals for Properties

```csharp
// WRONG - Not refactor-safe
query.Where.Property("Email").Eq(email);

// CORRECT
query.Where.Property(nameof(User.Email)).Eq(email);
```

### ❌ Mistake 3: Assuming Read Methods Require Explicit Execute

```csharp
// This is valid: ReadOne() will execute the query if needed
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Where.Property(nameof(User.Id)).Eq(id);
    return query.ReadOne<User>();
}

// Also valid, and often clearer when reading the code
using (var query = connection.GetSelectEntitiesQuery<User>())
{
    query.Where.Property(nameof(User.Id)).Eq(id);
    query.Execute();
    return query.ReadOne<User>();
}
```

### ❌ Mistake 4: Not Specifying Size for Strings

```csharp
// WRONG - May cause table bloat
[EntityProperty(Field = "name")]
public string Name { get; set; }

// CORRECT
[EntityProperty(Field = "name", Size = 128)]
public string Name { get; set; }
```

### ❌ Mistake 5: Incorrect Foreign Key Definition

```csharp
// WRONG - Foreign key property should be entity type, not ID
[EntityProperty(Field = "user_id", ForeignKey = true)]
public int UserId { get; set; }

// CORRECT
[ForeignKey(Field = "user_id")]
public User User { get; set; }
```

---

## Quick Reference

### Entity Attributes

```csharp
[Entity(Table = "table_name", Scope = "scope")]
public class Entity { }

[AutoId]                                          // Primary key with auto-increment
[PrimaryKey]                                      // Primary key without auto-increment
[EntityProperty(Field = "col", Size = 100)]       // String column
[ForeignKey(Field = "fk_col")]                    // Foreign key
[EntityProperty(Field = "col", Nullable = true)]  // Nullable column
[EntityProperty(Field = "col", Sorted = true)]    // Indexed column
[EntityProperty(Field = "col", Unique = true)]    // Unique column
[EntityProperty(Field = "col", DbType = DbType.Decimal, Precision = 2)] // Decimal
```

### Query Patterns

```csharp
// SELECT one
using (var q = conn.GetSelectEntitiesQuery<T>())
{
    q.Where.Property(nameof(T.Id)).Eq(id);
    q.Execute();
    return q.ReadOne<T>();
}

// SELECT many
using (var q = conn.GetSelectEntitiesQuery<T>())
{
    q.Where.Property(nameof(T.IsActive)).Eq(true);
    q.AddOrderBy(nameof(T.Name));
    q.Execute();
    return q.ReadAll<EntityCollection<T>, T>();
}

// COUNT
using (var q = conn.GetSelectEntitiesCountQuery<T>())
{
    q.Where.Property(nameof(T.IsActive)).Eq(true);
    q.Execute();
    return q.RowCount;
}

// INSERT
using (var q = conn.GetInsertEntityQuery<T>())
{
    q.Execute(entity);
}

// UPDATE
using (var q = conn.GetUpdateEntityQuery<T>())
{
    q.Execute(entity);
}

// DELETE
using (var q = conn.GetDeleteEntityQuery<T>())
{
    q.Execute(entity);
}

// TRANSACTION
using (var tx = conn.BeginTransaction())
{
    try
    {
        // operations...
        tx.Commit();
    }
    catch
    {
        tx.Rollback();
        throw;
    }
}
```

---

## Plain SQL Operations

While the entity framework provides high-level abstractions, you can also execute raw SQL queries when needed.

### Executing Raw SQL Queries

```csharp
public void ExecuteRawInsert()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetQuery("INSERT INTO users (name, email) VALUES (@name, @email)"))
    {
        query.BindParam("name", "John Doe");
        query.BindParam("email", "john@example.com");
        query.ExecuteNoData();
    }
}
```

### Reading Results from Raw SQL

```csharp
public List<(int Id, string Name)> ExecuteRawSelect()
{
    using var connection = mFactory.GetConnection();
    var results = new List<(int, string)>();

    using (var query = connection.GetQuery("SELECT id, name FROM users WHERE name LIKE @mask"))
    {
        query.BindParam("mask", "J%");
        query.ExecuteReader();

        while (query.ReadNext())
        {
            int id = query.GetValue<int>(0);            // By index
            string name = query.GetValue<string>("name"); // By name
            results.Add((id, name));
        }
    }

    return results;
}
```

### Using Query Builders with Entities

For complex queries beyond entity framework capabilities, combine query builders with entities:

```csharp
public void UpdateStatisticsUsingQueryBuilder()
{
    using var connection = mFactory.GetConnection();

    // Get table descriptors from entities
    var entityTable = AllEntities.Inst[typeof(Entity)].TableDescriptor;
    var subentityTable = AllEntities.Inst[typeof(Subentity)].TableDescriptor;

    // Build complex update query
    var updateBuilder = new UpdateQueryBuilder(connection.GetLanguageSpecifics(), entityTable);

    // Create subquery for calculation
    var selectBuilder = new SelectQueryBuilder(connection.GetLanguageSpecifics(), subentityTable);
    selectBuilder.AddToResultset(AggFn.Count);
    selectBuilder.Where.Property(subentityTable[nameof(Subentity.Entity)])
                       .Is(CmpOp.Eq)
                       .Raw(updateBuilder.GetAlias(entityTable[nameof(Entity.ID)]));

    // Add subquery to update
    updateBuilder.AddUpdateColumnSubquery(entityTable[nameof(Entity.Statistics)], selectBuilder);
    updateBuilder.Where.Property(entityTable[nameof(Entity.Statistics)]).Is(CmpOp.Eq).Parameter("param1");

    using (var query = connection.GetQuery(updateBuilder))
    {
        query.BindParam("param1", 0);
        query.ExecuteNoData();
    }
}
```

---

## Advanced Entity Patterns

### Filtering Related Entity Properties

Query entities based on properties of related entities:

```csharp
public EntityCollection<Order> GetOrdersForUsersByName(string namePattern)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<Order>())
    {
        // Filter by property of related entity
        query.Where.PropertyOf<User>(nameof(User.Name)).Like($"%{namePattern}%");
        query.Execute();
        return query.ReadAll<Order>();
    }
}
```

### Generic Select Queries with Custom Resultsets

Use `GetGenericSelectEntityQuery` for custom projections:

```csharp
public dynamic GetOrderSummary(int orderId)
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetGenericSelectEntityQuery<Order>())
    {
        // Add specific columns to resultset
        query.AddToResultset(nameof(Order.Id), "id");
        query.AddToResultset(nameof(Order.Total), "total");
        query.AddEntity<User>();
        query.AddToResultset(typeof(User), nameof(User.Name), "user_name");

        query.Where.Property(nameof(Order.Id)).Eq(orderId);
        query.Execute();

        return query.ReadOneDynamic();
    }
}
```

### Aggregate Queries

```csharp
public dynamic GetOrderStatsByUser()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetGenericSelectEntityQuery<Order>())
    {
        query.AddEntity<User>();

        // Add aggregates
        query.AddToResultset(typeof(User), nameof(User.Name), "user_name");
        query.AddToResultset(AggFn.Count, null, "order_count");
        query.AddToResultset(AggFn.Sum, nameof(Order.Total), "total_amount");
        query.AddToResultset(AggFn.Avg, nameof(Order.Total), "avg_amount");

        query.AddGroupBy(typeof(User), nameof(User.Name));
        query.Execute();

        return query.ReadAllDynamic();
    }
}
```

### Cross-Referenced Subqueries

Find entities that don't have related records:

```csharp
public EntityCollection<Dictionary> GetUnusedDictionaries()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<Dictionary>())
    {
        using (var subquery = connection.GetGenericSelectEntityQuery<Entity>())
        {
            // Create cross-reference between queries
            var dictionaryIdRef = query.GetReference(nameof(Dictionary.ID));
            subquery.Where.Property(nameof(Entity.Reference))
                          .Is(CmpOp.Eq)
                          .Reference(dictionaryIdRef);

            // Use NOT EXISTS
            query.Where.Add().Is(CmpOp.NotExists).Query(subquery);
        }

        query.Execute();
        return query.ReadAll<Dictionary>();
    }
}
```

### Selecting with Column Filters

Optimize queries by excluding large columns:

```csharp
public EntityCollection<Document> GetDocumentList()
{
    using var connection = mFactory.GetConnection();

    // Define columns to exclude
    var filter = new SelectEntityQueryFilter[]
    {
        new SelectEntityQueryFilter() { Property = nameof(Document.Content) },  // Exclude large CLOB
        new SelectEntityQueryFilter() { Property = nameof(Document.Attachment) }  // Exclude large BLOB
    };

    using (var query = connection.GetSelectEntitiesQuery<Document>(filter))
    {
        query.Execute();
        return query.ReadAll<Document>();
        // Content and Attachment will be null, but other properties are loaded
    }
}
```

### IsNull/IsNotNull Conditions

```csharp
public EntityCollection<Entity> GetEntitiesWithNullStatistics()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<Entity>())
    {
        query.Where.Property(nameof(Entity.Statistics)).Is(CmpOp.IsNull);
        query.Execute();
        return query.ReadAll<Entity>();
    }
}
```

### Grouped OR Conditions

```csharp
public EntityCollection<Entity> GetComplexFilteredEntities()
{
    using var connection = mFactory.GetConnection();

    using (var query = connection.GetSelectEntitiesQuery<Entity>())
    {
        // WHERE (Statistics IS NULL OR (Statistics >= 20 AND Statistics <= 180))
        using (var bracket = query.Where.AddGroup())
        {
            query.Where.Or().Property(nameof(Entity.Statistics)).Is(CmpOp.IsNull);
            using (var innerBracket = query.Where.AddGroup(LogOp.Or))
            {
                query.Where.Property(nameof(Entity.Statistics)).Is(CmpOp.Ge).Value(20);
                query.Where.And().Property(nameof(Entity.Statistics)).Is(CmpOp.Le).Value(180);
            }
        }

        query.Execute();
        return query.ReadAll<Entity>();
    }
}
```

---

## Schema Management Advanced

### Automatic Schema Updates

The framework supports automatic schema updates with obsolete markers:

```csharp
[Entity(Table = "entity_table")]
[OnEntityCreate(typeof(MyClass), nameof(MyClass.OnEntityCreate))]
public class Entity
{
    [AutoId]
    public int ID { get; set; }

    [EntityProperty(Size = 64)]
    public string Name { get; set; }

    // New property being added (must be nullable)
    [EntityProperty(Size = 100, Nullable = true)]
    [OnEntityPropertyCreate(typeof(MyClass), nameof(MyClass.OnPropertyCreate))]
    public string NewProperty { get; set; }

    // Old property marked for removal
    [ObsoleteEntityProperty]
    [OnEntityPropertyDrop(typeof(MyClass), nameof(MyClass.OnPropertyDrop))]
    public string OldProperty { get; set; }
}

public class MyClass
{
    public static void OnEntityCreate(SqlDbConnection connection)
    {
        // Custom logic after entity created
    }

    public static void OnPropertyCreate(SqlDbConnection connection)
    {
        // Custom logic after property created (e.g., populate default values)
    }

    public static void OnPropertyDrop(SqlDbConnection connection)
    {
        // Custom logic before property dropped (e.g., migration)
    }
}
```

**Schema Update Limitations:**
- Cannot change type, name, or attributes of existing properties
- Cannot add/remove primary keys from existing entities
- New properties must be nullable
- Mark obsolete entities with `[ObsoleteEntity]` attribute
- Mark obsolete properties with `[ObsoleteEntityProperty]` attribute

---

## Documentation References

- Official Documentation: https://docs.gehtsoftusa.com/Gehtsoft.EF/ef/#main.html
- Test Suite: `Gehtsoft.EF.Test` project
- Example Application: `TestWebApp` project

---

## Summary for Claude Code

When working with Gehtsoft.EF:

1. **Entities** are defined with `[Entity]` and `[EntityProperty]` attributes
2. **Always use** `ISqlDbConnectionFactory` for DI
3. **Always dispose** connections and queries using `using` statements
4. **Always use** `nameof()` for property references
5. **Always validate** method arguments
6. `Execute()` before reading is optional for `SelectEntitiesQuery`; `ReadOne`/`ReadAll` will execute automatically if needed
7. Use **generic methods** (`GetSelectEntitiesQuery<T>()`) when possible
8. Specify **Size** for string properties
9. Foreign keys are **entity-typed properties**, not ID properties
10. Handle **null returns** from SELECT queries appropriately

This framework emphasizes explicit control, type safety, and proper resource management.
