# Gehtsoft.EF Framework Reference Guide

## Overview

Gehtsoft.EF is a lightweight, database-agnostic Entity Framework used for database operations. This guide covers common patterns and operations.

## Required Namespaces

```csharp
using System.Data;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
```

## Entity Definition

### Basic Entity

Entities are decorated with attributes to map to database tables.

```csharp
[Entity(Table="users")]
public class User
{
    [EntityProperty(Field = "id", Autoincrement = true, PrimaryKey = true)]
    public int Id { get; set; }
    
    [EntityProperty(Field = "name", Size = 128, Sorted = true)]
    public string Name { get; set; }
    
    [EntityProperty(Field = "email", Size = 256, Sorted = true)]
    public string Email { get; set; }
    
    [EntityProperty(Field = "created_at", Sorted = true)]
    public DateTime CreatedAt { get; set; }
    
    [EntityProperty(Field = "is_active")]
    public bool IsActive { get; set; }
}
```

### Entity with Nullable Properties

```csharp
[Entity(Table="profiles")]
public class Profile
{
    [EntityProperty(Field = "id", Autoincrement = true, PrimaryKey = true)]
    public int Id { get; set; }
    
    [EntityProperty(Field = "bio", Size = 4096, Nullable = true)]
    public string Bio { get; set; }
    
    [EntityProperty(Field = "avatar", DbType = DbType.Binary, Nullable = true)]
    public byte[] Avatar { get; set; }
    
    [EntityProperty(Field = "age")]
    public int? Age { get; set; }
}
```

### Entity with Foreign Key

```csharp
[Entity(Table="orders")]
public class Order
{
    [EntityProperty(Field = "id", Autoincrement = true, PrimaryKey = true)]
    public int Id { get; set; }
    
    [EntityProperty(Field = "user", ForeignKey = true)]
    public User User { get; set; }
    
    [EntityProperty(Field = "total", DbType = DbType.Decimal)]
    public decimal Total { get; set; }
}
```

### Entity Collections

Define collection types for query results:

```csharp
public class UserCollection : EntityCollection<User>
{
}

public class OrderCollection : EntityCollection<Order>
{
}
```

### EntityProperty Attributes

- **Field**: Database column name
- **DbType**: Only specify for non-obvious types (Binary, Decimal, etc.). Not needed for int, string, DateTime, bool
- **Size**: For string/binary fields, specifies maximum length
- **Autoincrement**: True for auto-increment primary keys
- **PrimaryKey**: True for primary key fields
- **Sorted**: True to create an index on this field
- **Nullable**: Only specify when needed. Not required for nullable value types (int?, DateTime?, etc.)
- **ForeignKey**: True for foreign key relationships

## Connection Management

### Connection Factory Pattern (Recommended)

Always use `ISqlDbConnectionFactory` for connection management.

```csharp
public class UserDataBoundary : IUserDataBoundary
{
    private readonly ISqlDbConnectionFactory mFactory;
    private readonly ILogger<UserDataBoundary> mLogger;
    
    public UserDataBoundary(ISqlDbConnectionFactory factory, ILogger<UserDataBoundary> logger)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));
        
        mFactory = factory;
        mLogger = logger;
    }
    
    public User GetUser(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be positive", nameof(id));
        
        using var connection = mFactory.GetConnection();
        
        using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
        {
            query.Where.Property(nameof(User.Id)).Eq(id);
            query.Execute();
            return query.ReadOne<User>();
        }
    }
}
```

### Register Connection Factory in DI

```csharp
// Program.cs or Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    // Register connection factory
    services.AddSingleton<ISqlDbConnectionFactory>(
        new SqlDbUniversalConnectionFactory(
            Configuration["database:driver"],
            Configuration["database:connectionString"]
        )
    );
    
    // Register boundaries
    services.AddScoped<IUserDataBoundary, UserDataBoundary>();
}
```

### Configuration Example (appsettings.json)

```json
{
  "database": {
    "driver": "sqlite",
    "connectionString": "Data Source=myapp.db"
  }
}
```

## Database Schema Operations

### Create/Update Tables Using CreateEntityController

Use `CreateEntityController` to create or update database schema. This controller can handle schema changes automatically.

```csharp
public void InitializeDatabase(ISqlDbConnectionFactory factory)
{
    if (factory == null)
        throw new ArgumentNullException(nameof(factory));
    
    // Create controller with assemblies containing entity definitions
    CreateEntityController controller = new CreateEntityController(
        new[] { this.GetType().Assembly }, 
        "myapp"
    );
    
    using var connection = factory.GetConnection();
    
    if (connection == null)
        throw new ArgumentException("Cannot create a connection to the database", nameof(factory));
    
    // Enable truncation rules
    UpdateQueryTruncationRules.Instance.EnableTruncation(
        connection.Connection.ConnectionString, 
        new TruncactionManager()
    );
    
    // Temporarily disable SQL injection protection for schema operations
    bool oldProtection = SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries;
    SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;
    
    try
    {
        // Update tables (creates new, updates existing, preserves data)
        controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update);
    }
    finally
    {
        // Restore SQL injection protection
        SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = oldProtection;
    }
}
```

### Update Modes

- **Update**: Creates new tables, updates existing tables, preserves data
- **Create**: Drops and recreates all tables (data loss)
- **Alter**: Only alters existing tables, doesn't create new ones

## SELECT Queries

### Select Single Entity by ID

```csharp
public User GetUser(int id)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        query.Where.Property(nameof(User.Id)).Eq(id);
        query.Execute();
        return query.ReadOne<User>();
    }
}
```

### Select Single Entity by Property

```csharp
public User FindUserByEmail(string email)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        query.Where.Property(nameof(User.Email)).Eq(email);
        query.Execute();
        return query.ReadOne<User>();
    }
}
```

### Select Multiple Entities

```csharp
public UserCollection GetActiveUsers()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.AddOrderBy(nameof(User.Name));
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with LIKE Pattern

```csharp
public UserCollection SearchUsers(string nameMask)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        if (nameMask != null)
            query.Where.Property(nameof(User.Name)).Like(nameMask);
        
        query.AddOrderBy(nameof(User.Name));
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with Pagination

```csharp
public UserCollection GetUsersPaged(int pageIndex, int pageSize)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        query.AddOrderBy(nameof(User.Name));
        query.Skip = pageIndex * pageSize;
        query.Limit = pageSize;
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with Sorting

```csharp
public UserCollection GetUsersSorted(bool descending = false)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        if (descending)
            query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
        else
            query.AddOrderBy(nameof(User.CreatedAt));
        
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with Multiple Conditions

```csharp
public UserCollection GetActiveUsersCreatedAfter(DateTime date)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        query.Where.Property(nameof(User.CreatedAt)).Gt(date);
        query.AddOrderBy(nameof(User.CreatedAt), SortDir.Desc);
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with OR Conditions

```csharp
public UserCollection SearchUsersByNameOrEmail(string searchTerm)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        // Use AddGroup for OR conditions
        using (var group = query.Where.AddGroup())
        {
            query.Where.Property(nameof(User.Name)).Like(searchTerm);
            query.Where.Or().Property(nameof(User.Email)).Like(searchTerm);
        }
        
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Select with Foreign Key Navigation

```csharp
public OrderCollection GetOrdersByUser(User user)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Order)))
    {
        query.Where.Property(nameof(Order.User)).Eq(user.Id);
        query.AddOrderBy(nameof(Order.CreatedAt), SortDir.Desc);
        query.Execute();
        return query.ReadAll<OrderCollection, Order>();
    }
}
```

### Ordering by Related Entity Property

```csharp
public OrderCollection GetOrdersOrderedByUserName()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(Order)))
    {
        // Order by property of related entity
        query.AddOrderBy(typeof(User), nameof(User.Name));
        query.Execute();
        return query.ReadAll<OrderCollection, Order>();
    }
}
```

## COUNT Queries

### Count All Entities

```csharp
public int CountUsers()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(typeof(User)))
    {
        query.Execute();
        return query.RowCount;
    }
}
```

### Count with Conditions

```csharp
public int CountActiveUsers()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(typeof(User)))
    {
        query.Where.Property(nameof(User.IsActive)).Eq(true);
        return query.RowCount;
    }
}
```

### Count with Search Pattern

```csharp
public int CountUsersMatching(string nameMask)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesCountQuery query = connection.GetSelectEntitiesCountQuery(typeof(User)))
    {
        if (nameMask != null)
            query.Where.Property(nameof(User.Name)).Like(nameMask);
        
        return query.RowCount;
    }
}
```

## Subqueries

### IN Subquery

```csharp
public UserCollection GetUsersWithOrders()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        // Subquery to find users who have orders
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

### NOT IN Subquery

```csharp
public UserCollection GetUsersWithoutOrders()
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
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

### Subquery with Parameters

```csharp
public UserCollection GetUsersWithOrdersAfter(DateTime date)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
        {
            subquery.Distinct = true;
            subquery.AddToResultset(nameof(Order.User));
            subquery.Where.Add(LogOp.And).Property(nameof(Order.CreatedAt)).Is(CmpOp.Gt).Parameter("date");
            
            query.Where.Property(nameof(User.Id)).In(subquery);
            query.BindParam("date", date);
        }
        
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

### Nested Subqueries

```csharp
public UserCollection GetUsersWithLargeOrders(decimal minTotal)
{
    using var connection = mFactory.GetConnection();
    
    using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
    {
        // Inner subquery: find orders over threshold
        using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
        {
            subquery.Distinct = true;
            subquery.AddToResultset(nameof(Order.User));
            subquery.Where.Add(LogOp.And).Property(nameof(Order.Total)).Is(CmpOp.Gt).Parameter("minTotal");
            
            query.Where.Property(nameof(User.Id)).In(subquery);
            query.BindParam("minTotal", minTotal);
        }
        
        query.Execute();
        return query.ReadAll<UserCollection, User>();
    }
}
```

## INSERT Operations

### Insert New Entity

```csharp
public void SaveUser(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    
    using var connection = mFactory.GetConnection();
    
    // Determine if insert or update based on primary key
    bool isNew = user.Id <= 0;
    
    using (ModifyEntityQuery query = isNew 
        ? connection.GetInsertEntityQuery(typeof(User))
        : connection.GetUpdateEntityQuery(typeof(User)))
    {
        query.Execute(user);
        // After insert, user.Id will be populated with the auto-generated value
    }
}
```

### Insert with Explicit Insert Query

```csharp
public void CreateUser(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    
    using var connection = mFactory.GetConnection();
    
    using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(User)))
    {
        query.Execute(user);
    }
}
```

## UPDATE Operations

### Update Existing Entity

```csharp
public void UpdateUser(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    
    if (user.Id <= 0)
        throw new ArgumentException("User must have valid Id for update", nameof(user));
    
    using var connection = mFactory.GetConnection();
    
    using (ModifyEntityQuery query = connection.GetUpdateEntityQuery(typeof(User)))
    {
        query.Execute(user);
    }
}
```

## DELETE Operations

### Delete Single Entity

```csharp
public void DeleteUser(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    
    using var connection = mFactory.GetConnection();
    
    using (ModifyEntityQuery query = connection.GetDeleteEntityQuery(typeof(User)))
    {
        query.Execute(user);
    }
}
```

### Delete Multiple Entities with Conditions

```csharp
public void DeleteOrdersForUser(int userId)
{
    using var connection = mFactory.GetConnection();
    
    using (var query = connection.GetMultiDeleteEntityQuery<Order>())
    {
        query.Where.Add(LogOp.And).Property(nameof(Order.User)).Is(CmpOp.Eq).Parameter("userId");
        query.BindParam("userId", userId);
        query.Execute();
    }
}
```

### Delete with Multiple Conditions

```csharp
public void DeleteOldInactiveUsers(DateTime beforeDate)
{
    using var connection = mFactory.GetConnection();
    
    using (var query = connection.GetMultiDeleteEntityQuery<User>())
    {
        query.Where.Add(LogOp.And).Property(nameof(User.IsActive)).Is(CmpOp.Eq).Parameter("isActive");
        query.Where.Add(LogOp.And).Property(nameof(User.CreatedAt)).Is(CmpOp.Lt).Parameter("date");
        query.BindParam("isActive", false);
        query.BindParam("date", beforeDate);
        query.Execute();
    }
}
```

## Transactions

### Basic Transaction

```csharp
public void TransferUserData(User fromUser, User toUser)
{
    using var connection = mFactory.GetConnection();
    
    using (SqlDbTransaction transaction = connection.BeginTransaction())
    {
        try
        {
            // Multiple operations in transaction
            using (ModifyEntityQuery query = connection.GetUpdateEntityQuery(typeof(User)))
            {
                query.Execute(fromUser);
            }
            
            using (ModifyEntityQuery query = connection.GetUpdateEntityQuery(typeof(User)))
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

### Transaction Wrapper Pattern

```csharp
public sealed class Transaction : IDisposable
{
    private SqlDbTransaction mTransaction;
    
    internal Transaction(SqlDbTransaction transaction)
    {
        mTransaction = transaction;
    }
    
    public void Commit()
    {
        if (mTransaction != null)
            mTransaction.Commit();
    }
    
    public void Rollback()
    {
        if (mTransaction != null)
            mTransaction.Rollback();
    }
    
    public void Dispose()
    {
        mTransaction?.Dispose();
        mTransaction = null;
    }
}

// Usage
public void SaveUserWithProfile(User user, Profile profile)
{
    using var connection = mFactory.GetConnection();
    
    using (Transaction transaction = new Transaction(connection.BeginTransaction()))
    {
        using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(User)))
        {
            query.Execute(user);
        }
        
        profile.UserId = user.Id;
        
        using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(Profile)))
        {
            query.Execute(profile);
        }
        
        transaction.Commit();
    }
}
```

## Comparison Operators

Available comparison operators for Where clauses:

- **Eq**: Equal to
- **NotEq**: Not equal to
- **Gt**: Greater than
- **Gte**: Greater than or equal to
- **Lt**: Less than
- **Lte**: Less than or equal to
- **Like**: SQL LIKE pattern matching
- **In**: Value in subquery or list
- **NotIn**: Value not in subquery or list

## Logical Operators

- **And**: Logical AND (default, can be omitted)
- **Or**: Logical OR

```csharp
// AND is implicit
query.Where.Property(nameof(User.IsActive)).Eq(true);
query.Where.Property(nameof(User.Age)).Gt(18);

// Explicit OR
query.Where.Property(nameof(User.Name)).Like("John%");
query.Where.Or().Property(nameof(User.Email)).Like("%@example.com");
```

## Common Patterns

### Find or Create Pattern

```csharp
public User FindOrCreateUser(string email, string name)
{
    User user = FindUserByEmail(email);
    
    if (user == null)
    {
        user = new User
        {
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        
        using var connection = mFactory.GetConnection();
        
        using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(User)))
        {
            query.Execute(user);
        }
    }
    
    return user;
}
```

### Upsert Pattern (Update or Insert)

```csharp
public void UpsertUser(User user)
{
    if (user == null)
        throw new ArgumentNullException(nameof(user));
    
    using var connection = mFactory.GetConnection();
    
    bool isUpdate = user.Id > 0;
    
    using (ModifyEntityQuery query = isUpdate
        ? connection.GetUpdateEntityQuery(typeof(User))
        : connection.GetInsertEntityQuery(typeof(User)))
    {
        query.Execute(user);
    }
}
```

### Cascade Delete Pattern

```csharp
public void DeleteUserWithOrders(User user)
{
    using var connection = mFactory.GetConnection();
    
    using (SqlDbTransaction transaction = connection.BeginTransaction())
    {
        try
        {
            // Delete related orders first
            using (var query = connection.GetMultiDeleteEntityQuery<Order>())
            {
                query.Where.Add(LogOp.And).Property(nameof(Order.User)).Is(CmpOp.Eq).Parameter("userId");
                query.BindParam("userId", user.Id);
                query.Execute();
            }
            
            // Then delete user
            using (ModifyEntityQuery query = connection.GetDeleteEntityQuery(typeof(User)))
            {
                query.Execute(user);
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

## Best Practices

### 1. Always Use `using var` for Connections

```csharp
// ✅ CORRECT
using var connection = mFactory.GetConnection();

// ❌ INCORRECT - Memory leak
var connection = mFactory.GetConnection();
```

### 2. Always Use `using` Statements for Queries

Queries implement `IDisposable` and must be disposed:

```csharp
// ✅ CORRECT
using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
{
    query.Where.Property(nameof(User.Id)).Eq(id);
    query.Execute();
    return query.ReadOne<User>();
}

// ❌ INCORRECT - Memory leak
SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User));
query.Execute();
return query.ReadOne<User>();
```

### 3. Use `nameof()` for Property References

Always use `nameof()` instead of string literals:

```csharp
// ✅ CORRECT - Type-safe, refactor-friendly
query.Where.Property(nameof(User.Email)).Eq(email);

// ❌ INCORRECT - Brittle, error-prone
query.Where.Property("Email").Eq(email);
```

### 4. Parameter Binding for Security

Always use parameter binding to prevent SQL injection:

```csharp
// ✅ CORRECT - SQL injection safe
query.Where.Add(LogOp.And).Property(nameof(User.Id)).Is(CmpOp.Eq).Parameter("userId");
query.BindParam("userId", userId);

// ❌ NEVER construct SQL from user input
```

### 5. Validate Arguments

Always validate public method arguments:

```csharp
public User GetUser(int id)
{
    if (id <= 0)
        throw new ArgumentException("Id must be positive", nameof(id));
    
    // Query implementation...
}
```

### 6. Use Transactions for Multiple Operations

```csharp
public void SaveComplexEntity(ComplexEntity entity)
{
    using var connection = mFactory.GetConnection();
    
    using (SqlDbTransaction transaction = connection.BeginTransaction())
    {
        try
        {
            // Multiple related operations
            SaveMainEntity(connection, entity);
            SaveRelatedEntities(connection, entity);
            
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

### 7. Subquery Disposal

Subqueries also implement `IDisposable`:

```csharp
using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
{
    using (var subquery = connection.GetGenericSelectEntityQuery<Order>())
    {
        subquery.Distinct = true;
        subquery.AddToResultset(nameof(Order.User));
        
        query.Where.Property(nameof(User.Id)).In(subquery);
    }
    
    query.Execute();
    return query.ReadAll<UserCollection, User>();
}
```

### 8. Don't Specify Obvious Types

Omit DbType and Nullable for obvious cases:

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

## Complete Boundary Example

```csharp
public class UserDataBoundary : IUserDataBoundary
{
    private readonly ISqlDbConnectionFactory mFactory;
    private readonly ILogger<UserDataBoundary> mLogger;
    
    public UserDataBoundary(ISqlDbConnectionFactory factory, ILogger<UserDataBoundary> logger)
    {
        if (factory == null)
            throw new ArgumentNullException(nameof(factory));
        if (logger == null)
            throw new ArgumentNullException(nameof(logger));
        
        mFactory = factory;
        mLogger = logger;
    }
    
    public User GetUser(int id)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be positive", nameof(id));
        
        try
        {
            using var connection = mFactory.GetConnection();
            
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
            {
                query.Where.Property(nameof(User.Id)).Eq(id);
                query.Execute();
                return query.ReadOne<User>();
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to get user with id: {UserId}", id);
            throw;
        }
    }
    
    public UserCollection SearchUsers(string nameMask, int pageIndex, int pageSize)
    {
        if (pageIndex < 0)
            throw new ArgumentException("Page index must be non-negative", nameof(pageIndex));
        if (pageSize <= 0)
            throw new ArgumentException("Page size must be positive", nameof(pageSize));
        
        try
        {
            using var connection = mFactory.GetConnection();
            
            using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(User)))
            {
                if (!string.IsNullOrEmpty(nameMask))
                    query.Where.Property(nameof(User.Name)).Like(nameMask);
                
                query.AddOrderBy(nameof(User.Name));
                query.Skip = pageIndex * pageSize;
                query.Limit = pageSize;
                query.Execute();
                return query.ReadAll<UserCollection, User>();
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to search users with mask: {Mask}", nameMask);
            throw;
        }
    }
    
    public void SaveUser(User user)
    {
        if (user == null)
            throw new ArgumentNullException(nameof(user));
        
        try
        {
            using var connection = mFactory.GetConnection();
            
            bool isUpdate = user.Id > 0;
            
            using (ModifyEntityQuery query = isUpdate
                ? connection.GetUpdateEntityQuery(typeof(User))
                : connection.GetInsertEntityQuery(typeof(User)))
            {
                query.Execute(user);
                
                mLogger.LogInformation(
                    isUpdate ? "Updated user {UserId}" : "Created user {UserId}", 
                    user.Id);
            }
        }
        catch (Exception ex)
        {
            mLogger.LogError(ex, "Failed to save user");
            throw;
        }
    }
}
```

## Documentation Reference

For more detailed information, see the official documentation:
https://docs.gehtsoftusa.com/Gehtsoft.EF/ef/#main.html