---
name: gehtsoft-ef
description: |
  Guide for working with the Gehtsoft.EF .NET ORM library — defining entities, querying data, managing schemas, and writing tests. Use this skill whenever the project references Gehtsoft.EF packages, or the user works with classes annotated with [Entity], [AutoId], [EntityProperty], [ForeignKey], [JsonEntityProperty], [JsonIndex], [DynamicProperties], or mentions SqlDbConnection, EntityQuery, CatalogEntityController, CreateEntityController, the schema catalogue (ef_catalog), JSON properties/columns, dynamic (per-row) properties, DynamicPropertyBag, GenericEntityAccessor, SelectEntitiesQuery, or any Gehtsoft.EF API. Also trigger when the user asks about data access code or schema migration in a project that already uses Gehtsoft.EF, even if they don't name the library explicitly.
packages: Gehtsoft.EF.Db.SqlDb, Gehtsoft.EF.Db.SqlDb.*, Gehtsoft.EF.Db.MssqlDb, Gehtsoft.EF.Db.MysqlDb, Gehtsoft.EF.Entities
---

# Gehtsoft.EF Skill

Gehtsoft.EF is a lightweight .NET ORM that maps C# classes to database tables using attributes. It is NOT Entity Framework / EF Core — it has its own API, conventions, and query model. Do not confuse the two or suggest EF Core patterns.

Key differences from EF Core:
- No DbContext — uses `SqlDbConnection` directly
- No LINQ-to-SQL query pipeline — uses a fluent query builder API
- No change tracking — explicit insert/update/delete calls
- No migrations framework — uses the **schema catalogue** (`CatalogEntityController.UpdateTables()`) for
  schema evolution. The older introspection-based `CreateEntityController` is now `[Obsolete]` — see
  Schema Migration.
- Supports: SQLite, SQL Server, PostgreSQL, MySQL, Oracle

## How to use this skill

This skill is delivered as a single bundle: the overview below is followed by a series of
reference sections (Setup, Entity Model, CRUD, Advanced SELECT, Generic Accessor, Migration,
Raw SQL, Patterns). Everything is already in context — there are no separate files to open.
Jump to the section that matches the task using the map below.

| Section | Use it for |
|---------|------------|
| Setup | Adding packages, creating connections, initial table creation |
| Entity Model | Defining entities, attributes, relationships, JSON properties, dynamic (per-row) properties, schema evolution |
| Entity CRUD Operations | Insert, update, delete, basic select, transactions |
| Advanced SELECT | Joins, aggregation, WHERE/HAVING, ordering, subqueries, hierarchical queries |
| GenericEntityAccessor and Filters | Simplified CRUD via GenericEntityAccessor, filter pattern |
| Schema Migration | UpdateTables, obsolete entities/columns, patches, SQLite workarounds |
| Raw SQL, QueryBuilder | Raw SQL via SqlQuery, low-level QueryBuilder |
| Real-World Patterns | DAO layer structure, DI integration, testing patterns, common mistakes |

### Work modes
- **Creating EF code** — read Setup → Entity Model → Entity CRUD Operations → Real-World Patterns.
- **Modifying EF code** — read the existing entity definitions first, then Entity Model (especially
  schema evolution with `[ObsoleteEntityProperty]`) and the relevant query section.
- **Testing EF code** — see Real-World Patterns Section 6 (SQLite in-memory pattern); use
  `CatalogEntityController.EnsureCatalogInfrastructure()` + `CreateTables(conn, version)` in test fixtures.
- **Understanding/describing EF code** — use Entity Model to decode attributes and the SELECT
  sections to explain query logic; pay attention to FK relationships, they drive automatic joins.

## Quick Reference

### Entity definition
```csharp
[Entity(Scope = "myapp", Table = "products")]
public class Product
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 256)]
    public string Name { get; set; }

    [ForeignKey]
    public Category Category { get; set; }

    [EntityProperty(DbType = DbType.Double)]
    public double Price { get; set; }
}
```

### JSON property (SQLite / PostgreSQL / Oracle only)
```csharp
[JsonEntityProperty(Nullable = true)]
[JsonIndex("$.Address.State", DbType.String)]   // repeatable; indexes one primitive path
public Profile Profile { get; set; }            // whole document (de)serialized automatically
// Query a value inside it:
q.Where.JsonPropertyOf<Person>(e => e.Profile.Age).Ge().Value(30);
```

### Dynamic (per-row) properties — schema-free named values in a `_props` side table
```csharp
[Entity(Table = "document")]
[DynamicProperties]
public class Document : IDynamicPropertiesOwner
{
    [AutoId] public int Id { get; set; }
    public DynamicPropertyBag DynamicProperties { get; private set; }  // read-only, private setter
}
// Set + save (auto-persisted with the entity):
var bag = document.InitializeDynamicProperties();
bag.Set("color", "red");
// Load (opt-in) + query:
q.PreloadProperties = true;                          // on a SelectEntitiesQuery
q.Where.DynamicPropertyOf<Document>("color").Eq("red");
```

### Connection
```csharp
using SqlDbConnection connection = UniversalSqlDbFactory.Create("sqlite", connectionString);
```

### Table creation / migration (schema catalogue — current)
```csharp
var controller = new CatalogEntityController(typeof(Product), "myapp");
controller.EnsureCatalogInfrastructure(connection);              // once — creates ef_catalog + ef_patch_history
controller.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
```
Every schema change requires bumping the version string on the next `UpdateTables` call.
The obsolete introspection controller (`CreateEntityController`, no version, self-bootstrapping)
still compiles but emits a deprecation warning. See the Schema Migration section for the full model
and for how to switch an existing database over (`AdoptExistingScope`).

### CRUD
```csharp
// Insert
using (var q = connection.GetInsertEntityQuery<Product>())
    q.Execute(product);

// Update
using (var q = connection.GetUpdateEntityQuery<Product>())
    q.Execute(product);

// Delete
using (var q = connection.GetDeleteEntityQuery<Product>())
    q.Execute(product);

// Select
using var q = connection.GetSelectEntitiesQuery<Product>();
q.Where.Property(nameof(Product.Price)).Gt(100.0);
var results = q.ReadAll<Product>();

// Count
using var cq = connection.GetSelectEntitiesCountQuery<Product>();
cq.Execute();
int count = cq.RowCount;
```

### Aggregation
```csharp
using var q = connection.GetGenericSelectEntityQuery<Product>();
q.AddToResultset(AggFn.Avg, nameof(Product.Price), "avg");
q.AddGroupBy(nameof(Product.Category));
var rows = q.ReadAllDynamic();
```

### Subquery
```csharp
using var sub = connection.GetGenericSelectEntityQuery<OrderItem>();
sub.AddToResultset(nameof(OrderItem.Product));
sub.Where.Property(nameof(OrderItem.Quantity)).Gt(10);

using var q = connection.GetSelectEntitiesQuery<Product>();
q.Where.Property(nameof(Product.Id)).In().Query(sub);
```

### Raw SQL
```csharp
using var q = connection.GetQuery("SELECT COUNT(*) FROM products WHERE price > @p");
q.BindParam("p", 100.0);
q.ExecuteReader();
q.ReadNext();
int count = q.GetValue<int>(0);
```

## Key Namespaces

```
Gehtsoft.EF.Entities              — [Entity], [EntityProperty], [AutoId], [ForeignKey], enums
Gehtsoft.EF.Db.SqlDb              — SqlDbConnection, UniversalSqlDbFactory, SqlDbQuery
Gehtsoft.EF.Db.SqlDb.EntityQueries — EntityQuery, SelectEntitiesQuery, ModifyEntityQuery, CatalogEntityController, EntityUpdateMode, CreateEntityController (obsolete)
Gehtsoft.EF.Db.SqlDb.QueryBuilder  — SelectQueryBuilder, TableDescriptor, ConditionBuilder
Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor — GenericEntityAccessor, FilterPropertyAttribute
```

## Key Enums

```
AggFn:    None, Count, Sum, Avg, Min, Max
CmpOp:    Eq, Neq, Ls, Le, Gt, Ge, Like, In, NotIn, IsNull, NotNull, Exists, NotExists
LogOp:    None, Not, And, Or
SortDir:  Asc, Desc
TableJoinType: None, Inner, Left, Right, Outer
```


# Setup Reference

## Adding Packages

Before adding packages, look up the latest version on NuGet:
```bash
dotnet package search Gehtsoft.EF.Db.SqlDb --take 1
```

Always needed:
```xml
<PackageReference Include="Gehtsoft.EF.Db.SqlDb" Version="LATEST" />
<PackageReference Include="Gehtsoft.EF.Entities" Version="LATEST" />
```

Per-database driver (pick one):
```xml
<PackageReference Include="Gehtsoft.EF.Db.SqliteDb" Version="LATEST" />
<PackageReference Include="Gehtsoft.EF.Db.MssqlDb" Version="LATEST" />
<PackageReference Include="Gehtsoft.EF.Db.PostgresDb" Version="LATEST" />
<PackageReference Include="Gehtsoft.EF.Db.MysqlDb" Version="LATEST" />
<PackageReference Include="Gehtsoft.EF.Db.OracleDb" Version="LATEST" />
```

Optional:
```xml
<PackageReference Include="Gehtsoft.EF.Utils" Version="LATEST" />
```

Replace `LATEST` with the actual version number from the NuGet search above.
All Gehtsoft.EF packages should use the same version.

## Connection Initialization

### Direct creation
```csharp
using Gehtsoft.EF.Db.SqlDb;

// Driver constants: "mssql", "mysql", "npgsql", "sqlite", "oracle"
using SqlDbConnection connection = UniversalSqlDbFactory.Create("sqlite", "Data Source=mydb.db");
```

### DI-friendly factory
```csharp
var factory = new SqlDbUniversalConnectionFactory("sqlite", "Data Source=mydb.db");
using SqlDbConnection connection = factory.GetConnection();

// Or async
using SqlDbConnection connection = await factory.GetConnectionAsync();
```

### ISqlDbConnectionFactory interface
```csharp
public interface ISqlDbConnectionFactory
{
    bool NeedDispose { get; }
    SqlDbConnection GetConnection();
    Task<SqlDbConnection> GetConnectionAsync(CancellationToken? token = null);
}
```

### Wrapping existing connection
```csharp
var factory = new ExistingConnectionFactory(existingConnection);
// NeedDispose = false -- won't dispose the underlying connection
```

### Driver name constants
```csharp
UniversalSqlDbFactory.MSSQL    // "mssql"
UniversalSqlDbFactory.MYSQL    // "mysql"
UniversalSqlDbFactory.POSTGRES // "npgsql"
UniversalSqlDbFactory.SQLITE   // "sqlite"
UniversalSqlDbFactory.ORACLE   // "oracle"
```

### Thread safety
SqlDbConnection is NOT thread-safe. Use Lock/LockAsync for concurrent access:
```csharp
using (await connection.LockAsync())
{
    // safe to use connection here
}
```
Best practice: create one connection per operation or per request.

### Database-specific notes
SQLite -- set PRAGMA for case-sensitive LIKE if needed:
```csharp
using var query = connection.GetQuery("PRAGMA case_sensitive_like=true;");
query.ExecuteNoData();
```

## Creating and Dropping Tables

### Using CatalogEntityController (recommended)

```csharp
// Discover all entities in the assembly containing MyEntity, filtered by scope
var controller = new CatalogEntityController(typeof(MyEntity), "myapp");

// Once per database — create the catalogue bookkeeping tables (ef_catalog + ef_patch_history)
controller.EnsureCatalogInfrastructure(connection);

// Create every table in its current model state, stamped at this schema version
controller.CreateTables(connection, "1.0.0");

// Drop all discovered tables (and tombstone them in the catalogue)
controller.DropTables(connection);
```

`CatalogEntityController` records the schema it applied in an EF-owned `ef_catalog` table and diffs
against that record on the next run — it no longer introspects the live database. The version string
is mandatory and is the shared ordering key for both structural changes and coded patches. See the
Schema Migration section for `UpdateTables`, the version guard, patch replay, and switching an existing
database from the obsolete `CreateEntityController`.

### Manual table operations
```csharp
// Create single entity table
using (var query = connection.GetCreateEntityQuery<Customer>())
    query.Execute();

// Drop single entity table
using (var query = connection.GetDropEntityQuery<Customer>())
    query.Execute();
```

## Schema Migration

For full migration coverage (the schema catalogue, `UpdateTables`, obsolete entities/properties,
patches, switching from the obsolete `CreateEntityController`), read the Schema Migration section.



# Entity Model Definition

Gehtsoft.EF maps .NET classes to database tables using attributes.

## Entity Class Definition

Apply `[Entity]` (from `Gehtsoft.EF.Entities`) to a class to mark it as a database entity.

| Property | Type | Description |
|----------|------|-------------|
| `Table` | string | Database table name. If omitted, derived from class name per NamingPolicy (pluralized by default). |
| `Scope` | string | Groups entities for the schema controller (e.g., `"myapp"`). |
| `NamingPolicy` | `EntityNamingPolicy` | Name generation for table/columns. Default = pluralize class name. |
| `View` | bool | If `true`, maps to a database view instead of a table. |
| `Metadata` | Type | Type implementing `ICompositeIndexMetadata` or `IViewCreationMetadata`. |

`EntityNamingPolicy` values: `Default`, `BackwardCompatibility`, `AsIs`, `LowerCase`, `UpperCase`, `LowerFirstCharacter`, `UpperFirstCharacter`, `LowerCaseWithUnderscores`, `UpperCaseWithUnderscopes`.

```csharp
using Gehtsoft.EF.Entities;
using System.Data;

[Entity(Scope = "myapp", Table = "customers")]
public class Customer
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
    public string Name { get; set; }
}
```

## Property Attributes

### `[EntityProperty]`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Field` | string | (derived) | Column name. If omitted, derived from property name per NamingPolicy. |
| `DbType` | `System.Data.DbType` | `DbType.Object` | Column type. If `Object`, inferred from .NET type. |
| `Size` | int | 0 | Column size. Required for `String`. |
| `Precision` | int | 0 | Decimal places for numeric types. |
| `PrimaryKey` | bool | false | Marks column as primary key. |
| `Autoincrement` | bool | false | Auto-increment column. |
| `AutoId` | bool | false | Shorthand for PrimaryKey + Autoincrement. |
| `ForeignKey` | bool | false | Marks column as a foreign key reference. |
| `Sorted` | bool | false | Creates an index on the column. |
| `Unique` | bool | false | Adds a unique constraint. |
| `Nullable` | bool | false | Allows NULL. Also set automatically for nullable .NET types. |
| `DefaultValue` | object | null | Default value (primitive types only). |
| `IgnoreRead` | bool | false | Excludes property from "read all" queries. |

### Shorthand Attributes

- **`[AutoId]`** -- equivalent to `[EntityProperty(AutoId = true, DbType = DbType.Int32)]`. Sets PrimaryKey and Autoincrement.
- **`[ForeignKey]`** -- equivalent to `[EntityProperty(ForeignKey = true)]`.

### DbType to .NET Type Mapping

| DbType | .NET Type | Notes |
|--------|-----------|-------|
| `Int32` | `int` | |
| `Int64` | `long` | |
| `Double` | `double` | |
| `Decimal` | `decimal` | Use `Size` + `Precision` |
| `String` | `string` | Requires `Size` |
| `DateTime` | `DateTime` | Date + time |
| `Date` | `DateTime` | Date only |
| `Boolean` | `bool` | |
| `Guid` | `Guid` | |
| `Binary` | `byte[]` | |

When `DbType` is omitted (`DbType.Object`), the type is inferred from the .NET property type.

### Primary Key Variants

```csharp
// Auto-increment int (most common)
[AutoId]
public int Id { get; set; }

// Guid primary key (assigned manually or via GenericEntityAccessor.NewGuidKey)
[EntityProperty(PrimaryKey = true)]
public Guid Id { get; set; }

// String primary key
[EntityProperty(PrimaryKey = true, DbType = DbType.String, Size = 64)]
public string Code { get; set; }
```

### Non-Mapped Properties

Properties without `[EntityProperty]`, `[AutoId]`, or `[ForeignKey]` are not mapped to the
database. Use them for computed values:

```csharp
[Entity(Table = "deliveries")]
public class Delivery
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.Double)]
    public double GrossWeight { get; set; }

    [EntityProperty(DbType = DbType.Double)]
    public double TareWeight { get; set; }

    // Not mapped — computed from mapped properties
    public double NetWeight => GrossWeight - TareWeight;
}
```

## Relationships (Foreign Keys)

A foreign key is a property whose .NET type is another entity class. The framework stores the referenced entity's primary key in the column.

```csharp
[Entity(Scope = "myapp", Table = "orders")]
public class Order
{
    [AutoId]
    public int Id { get; set; }

    [ForeignKey]
    public Customer Customer { get; set; }

    [EntityProperty(DbType = DbType.DateTime)]
    public DateTime OrderDate { get; set; }
}
```

### Self-Referencing Foreign Keys

For trees, an entity can reference itself. Mark as `Nullable` since root nodes have no parent.

```csharp
[Entity(Table = "categories")]
public class Category
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 128)]
    public string Name { get; set; }

    [EntityProperty(ForeignKey = true, Nullable = true)]
    public Category Parent { get; set; }
}
```

**Creation order:** Referenced entities must exist before referencing ones.
The schema controller resolves this automatically.

## Schema Evolution

`[ObsoleteEntityProperty]` marks a column for drop. Props: `Field` (column name), `ForeignKey` (bool), `Sorted` (bool).

```csharp
[ObsoleteEntityProperty(Field = "old_column")]
public string OldColumn { get; set; }
```

`[ObsoleteEntity]` marks an entire entity for table drop. Same properties as `[Entity]`.

```csharp
[ObsoleteEntity(Table = "old_table", Scope = "myapp")]
public class OldEntity { }
```

## Runtime Metadata

### AllEntities Registry

`AllEntities` is a global registry of all entity types discovered at runtime. Use it to get metadata about entities programmatically:

```csharp
using Gehtsoft.EF.Entities;

// Get the descriptor for an entity type
EntityDescriptor descriptor = AllEntities.Get<Product>();

// Check if a type is a registered entity
bool isEntity = AllEntities.Contains(typeof(Product));
```

### EntityDescriptor

`EntityDescriptor` provides runtime access to an entity's table name, columns, and relationships:

```csharp
EntityDescriptor descriptor = AllEntities.Get<Product>();

string tableName = descriptor.TableDescriptor.Name;

// Iterate columns
foreach (var column in descriptor.TableDescriptor)
{
    string columnName = column.Name;
    DbType dbType = column.DbType;
    bool isPK = column.PrimaryKey;
    bool isFK = column.ForeignKey;
    bool isNullable = column.Nullable;
}

// Access a specific column by entity property name
var priceColumn = descriptor[nameof(Product.Price)];
```

### TableDescriptor

`TableDescriptor` is the lower-level table schema used by `QueryBuilder`. Entity queries use `EntityDescriptor` (which wraps a `TableDescriptor`). See `references/raw-sql.md` Section 2 for `TableDescriptor` usage with `QueryBuilder`.

### EntityCollection<T>

`EntityCollection<T>` is the typed list returned by `ReadAll<T>()`. It extends `List<T>` with no additional members — use standard LINQ or list operations on it.

## Composite Indexes

Implement `ICompositeIndexMetadata` and reference via `Metadata` on `[Entity]`.
`CompositeIndex` supports plain columns, functions (`SqlFunctionId.Upper`), and sort direction.
Set `FailIfUnsupported = true` to throw if the database lacks function-index support.

```csharp
[Entity(Scope = "myapp", Table = "clients", Metadata = typeof(ClientMetadata))]
public class Client
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 256, Unique = true)]
    public string Name { get; set; }
}

public class ClientMetadata : ICompositeIndexMetadata
{
    public IEnumerable<CompositeIndex> Indexes
    {
        get
        {
            var index = new CompositeIndex("name_no_case");
            index.FailIfUnsupported = true;
            index.Add(SqlFunctionId.Upper, nameof(Client.Name));
            yield return index;
        }
    }
}
```

## JSON Properties

A property can be stored as a **JSON document in a single column**. Mark it with `[JsonEntityProperty]`
(from `Gehtsoft.EF.Entities`) instead of `[EntityProperty]`. The value — a primitive, an array of
primitives, or a `System.Text.Json`-annotated object — is serialized to a JSON string on save and
deserialized on load. The whole document is read/written automatically with the entity.

**Driver support:** SQLite, PostgreSQL, and Oracle 12.2+ **only**. Creating a JSON column on SQL Server
or MySQL throws an "unsupported" error.

| `[JsonEntityProperty]` property | Type | Description |
|---------|------|-------------|
| `Field` | string | Column name. Derived from the property name via NamingPolicy if omitted. |
| `Nullable` | bool | Allows SQL `NULL` (a `null` CLR value is stored as `NULL`, not the JSON text `"null"`). |

### Indexing values inside the document

`[JsonIndex]` is a **repeatable** attribute that indexes a single primitive value at a JSON path (which
may reach into a nested object, e.g. `"$.Address.Zip"`). Supported value types: string, integer types,
real/decimal, boolean, date/time. Arrays, `byte[]`, and whole nested objects are stored but cannot be
indexed.

| `[JsonIndex]` member | Type | Description |
|--------|------|-------------|
| `Path` (ctor arg) | string | JSON path to the value, e.g. `"$.Age"` or `"$.Address.State"`. |
| `DbType` | `DbType` | Primitive type at the path (drives extraction/cast). Default `DbType.String`. |
| `Unique` | bool | Marks the indexed value unique. |

```csharp
using Gehtsoft.EF.Entities;
using System.Data;

public class Address { public string City { get; set; } public string State { get; set; } }

public class Profile
{
    public string Name { get; set; }
    public int Age { get; set; }
    public decimal Income { get; set; }
    public DateTime DoB { get; set; }
    public int[] Scores { get; set; }
    public Address Address { get; set; }
}

[Entity(Table = "person")]
public class Person
{
    [AutoId]
    public int Id { get; set; }

    [JsonEntityProperty(Nullable = true)]
    [JsonIndex("$.Age", DbType.Int32)]
    [JsonIndex("$.Address.State", DbType.String)]
    public Profile Profile { get; set; }
}
```

### Querying by a JSON value

Beyond reading/writing the whole document, individual values can be filtered, projected, sorted, grouped,
and aggregated. Address a value by property name + JSON path + type, or (equivalently) by a member
expression. A row whose value is absent or `null` simply does not match a predicate.

```csharp
// Filter — path form
using var q = connection.GetSelectEntitiesQuery<Person>();
q.Where.JsonPropertyOf<Person>("Profile", "$.Address.State", DbType.String).Eq().Value("CA")
       .And().JsonPropertyOf<Person>("Profile", "$.Age", DbType.Int32).Ge().Value(30);
var adults = q.ReadAll<Person>();

// Filter — expression form (type inferred from the member; may descend objects / index an array element)
q.Where.JsonPropertyOf<Person>(e => e.Profile.Address.State).Eq().Value("CA")
       .And().JsonPropertyOf<Person>(e => e.Profile.Age).Ge().Value(30);
```

Project / order / group / aggregate JSON values via `GetSelectEntitiesQueryBase<T>()` and read them back
through the dynamic reader:

```csharp
using var q = connection.GetSelectEntitiesQueryBase<Person>();
q.AddJsonValueToResultset<Person>(e => e.Profile.Address.State, "state");
q.AddJsonValueToResultset<Person>(AggFn.Sum, e => e.Profile.Income, "total");
q.AddJsonValueToGroupBy<Person>(e => e.Profile.Address.State);
q.Having.JsonPropertyOf<Person>(e => e.Profile.Income).Sum().Gt().Value(1000000m);
q.AddJsonValueToOrderBy<Person>(e => e.Profile.Address.State);
var groups = q.ReadAllDynamic();
```

Notes:
- The extracted value type must be a primitive. A single array element (`"$.Scores[0]"` /
  `e => e.Profile.Scores[0]`) can be extracted; whole arrays/objects cannot.
- `DateTime` is stored/indexed as an ISO-8601 string on all three databases — filter/sort it as
  `DbType.String` and compare against ISO-8601 text (which sorts chronologically). In expression form,
  override the inferred type: `JsonPropertyOf<Person>(e => e.Profile.DoB, DbType.String)`.
- Extracting/filtering an indexed path with the **same** type lets the database use the `[JsonIndex]`.
- JSON values are **not** usable inside entity-LINQ lambdas (only the query surface above).

## Dynamic (Per-Row) Properties

Dynamic properties let an individual row carry additional **named values that are not part of the class**
— a flat, per-row bag of name/value pairs that may differ from row to row, with **no schema change**.
Use them for sparse/optional metadata, user-defined fields, or feature flags. If a value is present on
nearly every row or is central to the model, use a regular column instead (it always outperforms the
EAV lookup). This is unrelated to *dynamic entities* (which define a whole table's schema at runtime).

**How stored:** in a side table named `<entity_table>_props` (one value per row, alongside the owner's
PK and the property name). It is created/dropped **automatically** with the entity table — you never
create it yourself. Works on all supported drivers.

### Declaring an owner entity

Mark the class with `[DynamicProperties]` and implement `IDynamicPropertiesOwner`, exposing the bag as a
read-only property with a **private setter** (the framework fills it via reflection on load).

```csharp
using Gehtsoft.EF.Entities;

[Entity(Table = "document")]
[DynamicProperties]
public class Document : IDynamicPropertiesOwner
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(Size = 64, Nullable = true)]
    public string Name { get; set; }

    public DynamicPropertyBag DynamicProperties { get; private set; }
}
```

`[DynamicProperties]` optional tuning: `NameSize` (default 64), `StringValueSize` (default 256),
`RealValueSize` / `RealValuePrecision` (default -1 = driver native), and `TableSuffix` (constant `_props`).

**Supported value types:** `string`, `int`, `long`, `double`, `bool`, `DateTime` (and their nullable
forms). Any other type is rejected.

### Setting, saving, and loading values

The bag (`DynamicPropertyBag`) is saved **automatically** when the entity is inserted/updated — no
separate save call. It tracks its own changes, so an update writes only added/changed/removed values.
Deleting the entity removes its property rows too.

```csharp
// New entity — create an empty bag first
var document = new Document { Name = "contract" };
var bag = document.InitializeDynamicProperties();
bag.Set("color", "red");
bag.Set("pages", 12);
bag.Set("archived", false);
using (var q = connection.GetInsertEntityQuery<Document>())
    q.Execute(document);          // bag persisted automatically
```

Dynamic properties are **not** loaded automatically on read (that would cost an extra query per row).
Load them either by setting `PreloadProperties = true` on the select query (batched for the whole result
set) or via `connection.LoadPropertiesFor(entity)` for a single entity:

```csharp
using var q = connection.GetSelectEntitiesQuery<Document>();
q.PreloadProperties = true;
foreach (var doc in q.ReadAll<Document>())
{
    string color = doc.DynamicProperties.Get<string>("color");   // typed Get<T>; Get/Contains/Remove/Count also available
}
```

### Querying by a dynamic property

Filter with `DynamicPropertyOf<T>(name)` in any query's `Where` (value column/type inferred from the
compared value). Composes with `And`/`Or` and normal column conditions; works with
`SelectEntitiesCountQuery`. A row whose property is absent (or stored under a different type) does not
match.

```csharp
using var q = connection.GetSelectEntitiesQuery<Document>();
q.Where.Property(nameof(Document.Name)).Is(CmpOp.Like).Value("contract%")
       .And().DynamicPropertyOf<Document>("pages").Ge(10);
var found = q.ReadAll<Document>();
```

Project / order / group / aggregate via `GetSelectEntitiesQueryBase<T>()`. Because these clauses have no
value to infer from, pass the type explicitly with the `DynamicPropertyValueType` enum (`String`,
`Integer`, `Long`, `Real`, `Boolean`, `DateTime`). A property must be **added to the result set before**
it can be used in ORDER BY / GROUP BY / HAVING (they reuse that projection).

```csharp
using var q = connection.GetSelectEntitiesQueryBase<Document>();
q.AddDynamicPropertyToResultset<Document>("color", DynamicPropertyValueType.String, "color");
q.AddToResultset(AggFn.Count, typeof(Document), nameof(Document.Id), "count");
q.AddDynamicPropertyToGroupBy<Document>("color", DynamicPropertyValueType.String);
q.HavingDynamicPropertyOf<Document>("pages", DynamicPropertyValueType.Integer).Sum().Gt(100);
q.AddDynamicPropertyToOrderBy<Document>("color", DynamicPropertyValueType.String);
var groups = q.ReadAllDynamic();
```

Dynamic properties also work inside **entity-LINQ** lambdas via `e.DynamicProperties.Get<T>("name")` in
`Where`, `Select`, aggregates, `OrderBy`, and `GroupBy`:

```csharp
var red = connection.GetCollectionOf<Document>()
    .Where(e => e.DynamicProperties.Get<string>("color") == "red")
    .ToList();
```

**Schema note:** adding/removing `[DynamicProperties]` on an existing entity is reconciled by
`CatalogEntityController.UpdateTables` (it creates/drops the side table). Dropping the side table when the
owner stops being an owner is a data-losing change, so it is refused by default — see the Schema
Migration section's data-loss safety.

## Entity Lifecycle Callbacks

### `[OnEntityCreate]`

Called after table creation by the schema controller. Use for seed data.
Callback must match `delegate void EntityActionDelegate(SqlDbConnection connection)`.

```csharp
[OnEntityCreate(typeof(SeedData), nameof(SeedData.InsertDefaults))]
[Entity(Scope = "myapp", Table = "roles")]
public class Role
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 64)]
    public string Name { get; set; }
}

internal static class SeedData
{
    public static void InsertDefaults(SqlDbConnection connection)
    {
        using var query = connection.GetInsertEntityQuery<Role>();
        query.Execute(new Role { Name = "Admin" });
        query.Execute(new Role { Name = "User" });
    }
}
```

### `[OnEntityDrop]`

Called before table drop by the schema controller. Same delegate signature.

## Complete Entity Model Example

```csharp
using Gehtsoft.EF.Entities;
using System.Data;

[Entity(Scope = "myapp", Table = "statuses")]
public class Status
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 32, Unique = true)]
    public string Name { get; set; }
}

[Entity(Scope = "myapp", Table = "categories")]
public class Category
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 128)]
    public string Name { get; set; }

    [EntityProperty(ForeignKey = true, Nullable = true)]
    public Category Parent { get; set; }  // self-referencing tree
}

[Entity(Scope = "myapp", Table = "tickets")]
public class Ticket
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 256)]
    public string Title { get; set; }

    [ForeignKey]
    public Status Status { get; set; }

    [EntityProperty(ForeignKey = true, Nullable = true)]
    public Category Category { get; set; }

    [EntityProperty(DbType = DbType.DateTime)]
    public DateTime CreatedAt { get; set; }
}
```



# Entity CRUD Operations

All entity query extension methods live on `SqlDbConnection` via `EntityConnectionExtension`.
Every query object is `IDisposable` -- always wrap in `using`.

Assumed entity model: `Category` (AutoId, Name) and `Product` (AutoId, Name, FK to Category, Price, Stock).

## Insert

```csharp
var category = new Category { Name = "Electronics" };
using (var query = connection.GetInsertEntityQuery<Category>())
    query.Execute(category);
// category.Id is now populated with the auto-generated value

var product = new Product { Name = "Laptop", Category = category, Price = 999.99, Stock = 10 };
using (var query = connection.GetInsertEntityQuery<Product>())
    query.Execute(product);
```

Non-generic form: `connection.GetInsertEntityQuery(typeof(Category))`

The `ignoreAutoIncrement` parameter -- insert with an explicit ID value:

```csharp
using var query = connection.GetInsertEntityQuery<Category>(ignoreAutoIncrement: true);
```

## Update (Single Entity)

Updates by primary key:

```csharp
product.Price = 899.99;
using (var query = connection.GetUpdateEntityQuery<Product>())
    query.Execute(product);
```

Common save pattern (insert or update based on ID):

```csharp
public void SaveProduct(SqlDbConnection connection, Product product)
{
    using var query = product.Id < 1
        ? connection.GetInsertEntityQuery<Product>()
        : connection.GetUpdateEntityQuery<Product>();
    query.Execute(product);
}
```

Auto-detect insert vs update (requires auto-increment int PK; inserts when PK == 0):

```csharp
using var query = connection.GetModifyEntityQueryFor(product);
query.Execute(product);
```

## Update (Mass/Bulk)

Update multiple rows matching a condition:

```csharp
using var query = connection.GetMultiUpdateEntityQuery<Product>();
query.AddUpdateColumn(nameof(Product.Stock), 0);
query.Where.Property(nameof(Product.Price)).Ls(10.0);
query.Execute();
```

`AddUpdateColumnByExpression` for raw SQL expressions in the SET clause:

```csharp
query.AddUpdateColumnByExpression(nameof(Product.Stock), "stock - 1");
```

## Delete (Single Entity)

Delete by primary key:

```csharp
using (var query = connection.GetDeleteEntityQuery<Product>())
    query.Execute(product);
```

Check if safe to delete (no FK references exist):

```csharp
bool canDelete = connection.CanDelete(category);
if (canDelete)
{
    using var query = connection.GetDeleteEntityQuery<Category>();
    query.Execute(category);
}
```

Async variant: `await connection.CanDeleteAsync(category)`

Exclude specific types from the FK check:

```csharp
bool canDelete = connection.CanDelete(category, except: new[] { typeof(ArchivedProduct) });
```

## Delete (Mass/Bulk)

Delete multiple rows matching a condition:

```csharp
using var query = connection.GetMultiDeleteEntityQuery<Product>();
query.Where.Property(nameof(Product.Stock)).Eq(0);
query.Execute();
```

FK dependency order: delete children before parents. Delete Products referencing a Category before deleting the Category.

## Complete End-to-End Workflow

This shows the full lifecycle: create tables, insert parent, insert child with FK, select
with auto-populated FK, and clean up.

```csharp
// 1. Create tables (order is handled automatically)
var controller = new CatalogEntityController(typeof(Product), "myapp");
controller.EnsureCatalogInfrastructure(connection);
controller.CreateTables(connection, "1.0.0");

// 2. Insert parent
var category = new Category { Name = "Electronics" };
using (var q = connection.GetInsertEntityQuery<Category>())
    q.Execute(category);
// category.Id is now set (e.g., 1)

// 3. Insert child with FK reference
var product = new Product
{
    Name = "Laptop",
    Category = category,   // pass the whole object, not just the ID
    Price = 999.99,
    Stock = 10
};
using (var q = connection.GetInsertEntityQuery<Product>())
    q.Execute(product);

// 4. Select — FK is auto-populated
using (var q = connection.GetSelectOneEntityQuery<Product>(product.Id))
{
    Product loaded = q.ReadOne<Product>();
    // loaded.Category is a fully populated Category object
    // loaded.Category.Id == 1
    // loaded.Category.Name == "Electronics"
}

// 5. Clean up (children before parents)
using (var q = connection.GetDeleteEntityQuery<Product>())
    q.Execute(product);
using (var q = connection.GetDeleteEntityQuery<Category>())
    q.Execute(category);
```

## Basic Select

### Select all entities

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
EntityCollection<Product> products = query.ReadAll<Product>();
// ReadAll calls Execute() automatically
```

### Select one entity by PK

```csharp
using var query = connection.GetSelectOneEntityQuery<Product>(productId);
Product product = query.ReadOne<Product>();
```

### Select with simple where

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
query.Where.Property(nameof(Product.Price)).Gt(100.0);
var expensive = query.ReadAll<Product>();
```

### Count

```csharp
using var query = connection.GetSelectEntitiesCountQuery<Product>();
query.Where.Property(nameof(Product.Stock)).Gt(0);
query.Execute();
int count = query.RowCount;
```

### Row-by-row iteration (lower memory)

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
query.Execute();
while (query.ReadNext())
{
    Product p = query.ReadOne<Product>();
    // process one at a time
}
```

## Async Versions

All operations have async counterparts:

```csharp
using var query = connection.GetInsertEntityQuery<Product>();
await query.ExecuteAsync(product);

using var selectQuery = connection.GetSelectEntitiesQuery<Product>();
var products = await selectQuery.ReadAllAsync<Product>();

using var oneQuery = connection.GetSelectOneEntityQuery<Product>(productId);
Product p = await oneQuery.ReadOneAsync<Product>();
```

## Transactions

```csharp
using (var transaction = connection.BeginTransaction())
{
    try
    {
        using (var q1 = connection.GetInsertEntityQuery<Category>())
            q1.Execute(category);
        using (var q2 = connection.GetInsertEntityQuery<Product>())
            q2.Execute(product);
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

With isolation level:

```csharp
using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
```

## Query Lifecycle Notes

- All query objects are `IDisposable` -- always use `using`. Some databases require disposal before the next query can execute.
- `Execute()` prepares and runs the query.
- For select queries, `ReadAll`/`ReadOne` call `Execute()` automatically.
- `ReadNext()` advances the cursor one row (returns `false` when exhausted).
- Non-generic forms accept `Type` parameter: `GetInsertEntityQuery(typeof(T))`, etc.


# Advanced SELECT Features

Assumes the entity definitions from entity-queries.md (Category, Product, OrderItem).

## Section 1: Where Conditions

### Fluent condition API

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();

// Single condition
query.Where.Property(nameof(Product.Price)).Gt(100.0);

// Multiple conditions (AND is default)
query.Where.Property(nameof(Product.Price)).Gt(100.0);
query.Where.And().Property(nameof(Product.Stock)).Gt(0);

// OR
query.Where.Property(nameof(Product.Price)).Ls(10.0);
query.Where.Or().Property(nameof(Product.Price)).Gt(1000.0);
```

### Comparison operators (all as extension methods on SingleEntityQueryConditionBuilder)

- `Eq(value)` / `Neq(value)` — equal / not equal
- `Gt(value)` / `Ge(value)` — greater than / greater or equal
- `Ls(value)` / `Le(value)` — less than / less or equal
- `Like(pattern)` — SQL LIKE, use % and _ wildcards
- `IsNull()` / `NotNull()` — NULL checks
- `In()` / `NotIn()` — followed by `.Values(...)` or `.Query(...)`

### Operator-only form (for parameter binding later)

```csharp
query.Where.Property(nameof(Product.Price)).Gt().Parameter("minPrice");
query.BindParam("minPrice", 100.0);
```

### Grouping conditions with brackets

```csharp
query.Where.And(group =>
{
    group.Property(nameof(Product.Price)).Ls(10.0);
    group.Or().Property(nameof(Product.Price)).Gt(1000.0);
});
// Result: ... AND (price < 10 OR price > 1000)
```

### SQL functions in conditions

```csharp
// Case-insensitive comparison
query.Where.Property(nameof(Product.Name)).ToUpper().Like("LAPTOP%").ToUpper();

// Date parts
query.Where.Property(nameof(Order.OrderDate)).Year().Eq(2024);

// Aggregate in HAVING (see Section 6)
query.Having.Add().Property(nameof(Product.Id)).Count().Gt(5);
```

Full list of function extensions: `ToUpper()`, `ToLower()`, `Trim()`, `Abs()`, `Year()`, `Month()`, `Day()`, `Hour()`, `Minute()`, `Second()`, `Round(digits)`, `Left(chars)`, `Length()`, `ToString()`, `ToInteger()`, `ToDouble()`, `ToDate()`, `ToTimestamp()`, `Sum()`, `Min()`, `Max()`, `Avg()`, `Count()`.

## Section 2: Auto-Join (via Foreign Keys)

When an entity has FK properties, SELECT automatically joins the referenced tables.

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
var products = query.ReadAll<Product>();
// Each product.Category is fully populated
```

Filter by FK entity's properties using `PropertyOf<T>`:

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
query.Where.PropertyOf<Category>(nameof(Category.Name)).Eq("Electronics");
var products = query.ReadAll<Product>();
```

Order by FK entity's properties:

```csharp
query.AddOrderBy<Category>(c => c.Name);
// or
query.AddOrderBy(typeof(Category), nameof(Category.Name));
```

### PropertyOf with occurrence (same type joined multiple times)

When an entity has two FK properties pointing to the same type, use the `occurrence` parameter
to distinguish which join to filter on:

```csharp
[Entity(Table = "transfers")]
public class Transfer
{
    [AutoId] public int Id { get; set; }
    [ForeignKey] public Account FromAccount { get; set; }   // occurrence 0
    [ForeignKey] public Account ToAccount { get; set; }     // occurrence 1
    [EntityProperty(DbType = DbType.Double)] public double Amount { get; set; }
}

using var query = connection.GetSelectEntitiesQuery<Transfer>();
// Filter on the first Account join (FromAccount)
query.Where.PropertyOf(nameof(Account.Name), typeof(Account), occurrence: 0).Eq("Checking");
// Filter on the second Account join (ToAccount)
query.Where.And().PropertyOf(nameof(Account.Name), typeof(Account), occurrence: 1).Eq("Savings");
```

### Deeply nested bracket conditions

```csharp
query.Where.And(outer =>
{
    outer.Property(nameof(Product.Stock)).Gt(0);
    outer.Or(inner =>
    {
        inner.Property(nameof(Product.Price)).Ls(10.0);
        inner.And().Property(nameof(Product.Name)).Like("Sale%");
    });
});
// Result: ... AND (stock > 0 OR (price < 10 AND name LIKE 'Sale%'))
```

## Section 3: Manual Join

For explicit join control:

```csharp
using var query = connection.GetSelectEntitiesQueryBase<OrderItem>();
query.AddEntity<Product>(connectToProperty: nameof(OrderItem.Product));
query.AddEntity<Category>(); // auto-connects via Product.Category FK chain
```

With explicit join type:

```csharp
query.AddEntity(typeof(Category), TableJoinType.Left);
```

Join types: `TableJoinType.Inner`, `TableJoinType.Left`, `TableJoinType.Right`, `TableJoinType.Outer`

With explicit join condition:

```csharp
query.AddEntity(typeof(Category), TableJoinType.Left,
    typeof(Product), nameof(Product.Category), CmpOp.Eq,
    typeof(Category), nameof(Category.Id));
```

## Section 4: Result Set Customization

### Choosing specific fields (use GetSelectEntitiesQueryBase)

```csharp
using var query = connection.GetSelectEntitiesQueryBase<Product>();
query.AddToResultset(nameof(Product.Id));
query.AddToResultset(nameof(Product.Name));
query.AddToResultset(nameof(Product.Price));
query.Execute();
while (query.ReadNext())
{
    int id = query.GetValue<int>(0);
    string name = query.GetValue<string>(1);
    double price = query.GetValue<double>(2);
}
```

### Generic select with dynamic results

```csharp
using var query = connection.GetGenericSelectEntityQuery<Product>();
query.AddToResultset(nameof(Product.Name));
query.AddToResultset(AggFn.Sum, nameof(Product.Stock), "totalStock");
query.AddGroupBy(nameof(Product.Name));
dynamic result = query.ReadOneDynamic();
string name = result.Name;
int total = result.totalStock;
```

## Section 5: Aggregation

### Aggregate functions: `AggFn.Count`, `AggFn.Sum`, `AggFn.Avg`, `AggFn.Min`, `AggFn.Max`

```csharp
using var query = connection.GetGenericSelectEntityQuery<Product>();
query.AddToResultset(AggFn.Count, nameof(Product.Id), "count");
query.AddToResultset(AggFn.Avg, nameof(Product.Price), "avgPrice");
query.AddToResultset(AggFn.Max, nameof(Product.Price), "maxPrice");
dynamic result = query.ReadOneDynamic();
```

### With GROUP BY

```csharp
using var query = connection.GetGenericSelectEntityQuery<Product>();
query.AddEntity<Category>(connectToProperty: nameof(Product.Category));
query.AddToResultset(typeof(Category), nameof(Category.Name));
query.AddToResultset(AggFn.Count, nameof(Product.Id), "productCount");
query.AddToResultset(AggFn.Avg, nameof(Product.Price), "avgPrice");
query.AddGroupBy(typeof(Category), nameof(Category.Name));
query.AddOrderBy(typeof(Category), nameof(Category.Name));

var results = query.ReadAllDynamic();
foreach (dynamic row in results)
    Console.WriteLine($"{row.Name}: {row.productCount} products, avg ${row.avgPrice}");
```

### Lambda form with SqlFunction

```csharp
using var query = connection.GetGenericSelectEntityQuery<Product>();
query.AddToResultset<Product, DateTime>(p => SqlFunction.Max(p.Price), "maxPrice");
dynamic result = query.ReadOneDynamic();
```

## Section 6: HAVING

Filter on aggregate results:

```csharp
using var query = connection.GetGenericSelectEntityQuery<Product>();
query.AddToResultset(typeof(Category), nameof(Category.Name));
query.AddToResultset(AggFn.Count, nameof(Product.Id), "count");
query.AddGroupBy(typeof(Category), nameof(Category.Name));
query.Having.Add().Property(nameof(Product.Id)).Count().Gt(5);
```

## Section 7: Ordering and Pagination

### ORDER BY

```csharp
query.AddOrderBy(nameof(Product.Price), SortDir.Desc);
query.AddOrderBy(nameof(Product.Name)); // SortDir.Asc is default
```

### Pagination (LIMIT/OFFSET)

```csharp
query.Skip = 20;   // skip first 20 rows
query.Limit = 10;  // return 10 rows
```

### DISTINCT

```csharp
query.Distinct = true;
```

## Section 8: Subqueries in WHERE

### IN subquery

```csharp
// Find products in categories that have "Electronics" in the name
using var query = connection.GetSelectEntitiesQuery<Product>();
using var subquery = connection.GetGenericSelectEntityQuery<Category>();
subquery.AddToResultset(nameof(Category.Id));
subquery.Where.Property(nameof(Category.Name)).Like("%Electronics%");

query.Where.Property(nameof(Product.Category)).In().Query(subquery);
var products = query.ReadAll<Product>();
```

### NOT IN subquery

```csharp
query.Where.Property(nameof(Product.Category)).NotIn().Query(subquery);
```

### EXISTS / NOT EXISTS

```csharp
query.Where.Exists(subquery);
// or
query.Where.NotExists(subquery);
```

### Parameter sharing between query and subquery

```csharp
using var query = connection.GetSelectEntitiesQuery<Product>();
using var sub = connection.GetGenericSelectEntityQuery<OrderItem>();
sub.AddToResultset(nameof(OrderItem.Product));
sub.Where.Property(nameof(OrderItem.Quantity)).Gt().Parameter("minQty");
query.Where.Property(nameof(Product.Id)).In().Query(sub);
query.BindParam("minQty", 10);
```

## Section 9: Hierarchical Queries (CTE)

For tree-structured data with self-referencing FK:

```csharp
// Category has Parent FK to itself
using var query = connection.GetSelectEntitiesTreeQuery<Category>();
query.Root = parentCategoryId;  // starting node (null for all roots)
var tree = query.ReadAll<Category>();
```

The query uses recursive CTE internally. The entity must have a nullable self-referencing FK property.



# GenericEntityAccessor and Filters

## GenericEntityAccessor<T, TKey>

A higher-level CRUD wrapper that simplifies common operations. No need to create queries manually.

```csharp
var accessor = new GenericEntityAccessor<Product, int>(connection);

// Save (auto-detects insert vs update)
var product = new Product { Name = "Widget", Price = 9.99, Stock = 100 };
accessor.Save(product);      // inserts, populates product.Id
product.Price = 8.99;
accessor.Save(product);      // updates (Id is set)

// Get by primary key
Product p = accessor.Get(42);

// Delete
accessor.Delete(product);

// Check if deletable (no FK references)
bool safe = accessor.CanDelete(product);

// Async versions
await accessor.SaveAsync(product);
await accessor.DeleteAsync(product);
Product p2 = await accessor.GetAsync(42);
```

Key types: `int`, `string`, `Guid`

For Guid primary keys:
```csharp
var accessor = new GenericEntityAccessor<MyEntity, Guid>(connection);
var entity = new MyEntity();
accessor.NewGuidKey(entity);  // generates a unique GUID PK
accessor.Save(entity);
```

## Filters with FilterProperty

Define a filter class by deriving from `GenericEntityAccessorFilterT<T>`:

```csharp
public class ProductFilter : GenericEntityAccessorFilterT<Product>
{
    [FilterProperty(Operation = CmpOp.Eq)]
    public int? Id { get; set; }

    [FilterProperty(Operation = CmpOp.Like, PropertyName = nameof(Product.Name))]
    public string NamePattern { get; set; }

    [FilterProperty(Operation = CmpOp.Ge, PropertyName = nameof(Product.Price))]
    public double? MinPrice { get; set; }

    [FilterProperty(Operation = CmpOp.Le, PropertyName = nameof(Product.Price))]
    public double? MaxPrice { get; set; }

    [FilterProperty(Operation = CmpOp.IsNull, PropertyName = nameof(Product.Category))]
    public bool? CategoryIsNull { get; set; }
}
```

Rules:
- Property types must be nullable versions of entity property types (int?, double?, etc.)
- `null` value means the filter is inactive
- `PropertyName` defaults to the filter property name if omitted
- For `IsNull`/`NotNull`, use `bool?`: `true` = IsNull, `false` = IsNotNull, `null` = inactive
- For `In`/`NotIn`, use `ICollection` or `Array`
- All active filters are joined by AND

### Using filters with the accessor

```csharp
var accessor = new GenericEntityAccessor<Product, int>(connection);
var filter = new ProductFilter { MinPrice = 10.0, NamePattern = "Wid%" };

// Count
int count = accessor.Count(filter);

// Read with sort, skip, limit
var sortOrder = new[]
{
    new GenericEntitySortOrder(nameof(Product.Price), SortDir.Asc),
    new GenericEntitySortOrder(nameof(Product.Name), SortDir.Desc),
};
var products = accessor.Read<EntityCollection<Product>>(filter, sortOrder, skip: 0, limit: 20);

// Navigate to next/previous entity in sort order
Product next = accessor.NextEntity(currentProduct, sortOrder, filter);
Product prev = accessor.NextEntity(currentProduct, sortOrder, filter, reverseDirection: true);

// Get next entity's key without loading the full entity
int nextId = accessor.NextKey(currentProduct, sortOrder, filter);

// Delete matching filter
accessor.DeleteMultiple(filter);

// Update matching filter
accessor.UpdateMultiple(filter, nameof(Product.Stock), 0);
```

### Using filters with entity queries directly

Filters can also be bound to any ConditionEntityQueryBase:
```csharp
var filter = new ProductFilter { MinPrice = 50.0 };
using var query = connection.GetSelectEntitiesQuery<Product>();
filter.BindToQuery(query);
var products = query.ReadAll<Product>();
```

## GenericEntityAccessorWithAggregates<T, TKey>

Extends the base accessor for parent-child relationships where the child (aggregate) entities are managed through the parent.

```csharp
// Product is the parent, OrderItem is the aggregate (child)
var accessor = new GenericEntityAccessorWithAggregates<Product, int>(connection, typeof(OrderItem));

// Get aggregates for a parent entity
var items = accessor.GetAggregates<EntityCollection<OrderItem>, OrderItem>(
    product, filter: null, sortOrder: null, skip: null, limit: null);

// Count aggregates
int itemCount = accessor.GetAggregatesCount<OrderItem>(product, filter: null);

// Save aggregates (handles insert/update/delete diff)
accessor.SaveAggregates(product, originalItems, newItems,
    areDataEqual: (a, b) => a.Quantity == b.Quantity && a.UnitPrice == b.UnitPrice,
    areIDEqual: (a, b) => a.Id == b.Id,
    isDefined: a => a.Id > 0,
    isNew: a => a.Id < 1);
```


# Schema Migration Reference

Gehtsoft.EF now manages schema evolution through the **schema catalogue**: an EF-owned `ef_catalog`
table records the exact schema the framework last applied for each scope, and the controller diffs the
current entity model against that record. This replaces the older approach of introspecting the live
database on every run.

Two controllers exist:

| Controller | Status | How it decides what to change |
|-----------|--------|-------------------------------|
| **`CatalogEntityController`** | **Current — use this** | Diffs the model against the EF-owned `ef_catalog` record |
| `CreateEntityController` | `[Obsolete]` (still compiles, still works) | Introspects the live DB via `connection.Schema()` |

Both live in `Gehtsoft.EF.Db.SqlDb.EntityQueries`. The update mode enum is now the top-level
`EntityUpdateMode` (the old nested `CreateEntityController.UpdateMode` is kept only for source-compat on
the obsolete shim).

## UpdateTables — The Core Migration Method

```csharp
var controller = new CatalogEntityController(typeof(MyEntity), "myapp");
controller.EnsureCatalogInfrastructure(connection);              // once — see below
controller.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update);
```

The controller discovers all entity classes in the assembly, filtered by scope, reads the last-applied
schema for that scope from `ef_catalog`, computes the difference, and applies only the delta. A table
that has no catalogue record yet is created from scratch (diff-vs-nothing).

### The version string is mandatory

Every `CreateTables`/`UpdateTables` call takes a `major.minor.patch` version string (e.g. `"1.0.0"`,
`"2.3.1"`). It is the single ordering key for both structural schema and coded patches. The catalogue
stores it and enforces a **version guard** before touching anything (`Vc` = version currently recorded,
`Vi` = version passed):

- `Vi < Vc` → throws `CatalogVersionRegressed` (older app pointed at a newer DB).
- `Vi == Vc` **and** the model changed → throws `CatalogModelChangedWithoutVersionBump`.
  **Every schema change requires a version bump.**
- `Vi == Vc` and no change → clean no-op.
- `Vi > Vc` (or first contact) → apply.

### EnsureCatalogInfrastructure — the one-time bootstrap

`EnsureCatalogInfrastructure(connection)` creates the `ef_catalog` and `ef_patch_history` bookkeeping
tables if they are absent (idempotent, race-tolerant). Unlike the obsolete controller, `CreateTables` /
`UpdateTables` / `DropTables` **no longer self-bootstrap** — call `EnsureCatalogInfrastructure` once
before the first `UpdateTables` (typically at app/database startup). A missing catalogue otherwise
surfaces as a failing SELECT.

### Update Modes (`EntityUpdateMode`)

| Mode | Creates new tables | Adds new columns | Drops obsolete columns | Drops obsolete tables | Recreates existing |
|------|:-:|:-:|:-:|:-:|:-:|
| `Update` | yes | yes | yes (if DB supports it) | yes | no |
| `CreateNew` | yes | no | no | no | no |
| `Recreate` | yes | n/a | n/a | yes | yes (drops + creates) |

`CreateNew` behaves like `Update` in the catalogue controller (only `Recreate` is ever special-cased).

### Per-Entity Mode Overrides

```csharp
var overrides = new Dictionary<Type, EntityUpdateMode>
{
    { typeof(TempCache), EntityUpdateMode.Recreate }
};
controller.UpdateTables(connection, "1.1.0", EntityUpdateMode.Update, overrides);
```

**Constraint:** You cannot set Recreate on a parent table while a child table (that references it
via FK) is in Update mode. This would break referential integrity. The controller throws
`EfSqlException` with code `CannotRecreateTable` in this case.

### Data-loss safety

The catalogue never silently destroys data. A **column that leaves the model without
`[ObsoleteEntityProperty]`** (or a dynamic-properties side table whose owner stopped being an owner)
is refused by default: `UpdateTables` throws `CatalogColumnDropWouldLoseData` /
`CatalogDynamicPropertiesDropWouldLoseData` **before any DDL runs**. Explicit
`[ObsoleteEntityProperty]` / `[ObsoleteEntity]` drops and `Recreate` are exempt (they are deliberate).
To allow implicit drops, set `controller.DataLossPolicy = CatalogEntityController.CatalogDataLossPolicy.Drop`
(default is `Fail`). Destructive column *modifications* (type/family/geometry-metadata changes) are always
refused (`CatalogColumnAlterNotSupported`) — route them through a patch.

### What is NOT diffed / catalogued

Views are not catalogued — they are always dropped and recreated on `CreateTables`/`UpdateTables`
(a view definition change therefore still needs a version bump to re-apply).

### Lock

`UpdateTables` runs under an `IDbInstanceLock` (`ef_catalog_update:<scope>`) held across the whole
read→diff→apply. Tune via `controller.LockTimeout` (default 30 s) and `controller.LockLease`.

### Async Versions

```csharp
await controller.EnsureCatalogInfrastructureAsync(connection);
await controller.CreateTablesAsync(connection, "1.0.0");
await controller.UpdateTablesAsync(connection, "1.1.0", EntityUpdateMode.Update);
await controller.DropTablesAsync(connection);
```

## Obsolete Entities (Dropping Tables)

Mark an entity class with `[ObsoleteEntity]` to have its table dropped during migration:

```csharp
// The table "old_cache" will be dropped on next UpdateTables call
[ObsoleteEntity(Table = "old_cache", Scope = "myapp")]
public class OldCache { }
```

The `Table` and `Scope` must match the original `[Entity]` attribute values exactly.

The controller drops obsolete tables in reverse dependency order (children first).
If other active tables have FK references to the obsolete table, the drop may be blocked
depending on whether the database supports column drops.

## Obsolete Properties (Dropping Columns)

Mark a property with `[ObsoleteEntityProperty]` to drop its column:

```csharp
[Entity(Scope = "myapp", Table = "products")]
public class Product
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 256)]
    public string Name { get; set; }

    // This column will be dropped on next UpdateTables
    [ObsoleteEntityProperty(Field = "old_description")]
    public string OldDescription { get; set; }

    // If the obsolete column had an index, specify it:
    [ObsoleteEntityProperty(Field = "legacy_code", Sorted = true)]
    public string LegacyCode { get; set; }

    // If the obsolete column was a foreign key, specify it:
    [ObsoleteEntityProperty(Field = "old_category", ForeignKey = true)]
    public object OldCategory { get; set; }
}
```

The `Field` value must match the original column name. If the column had `Sorted = true` or
`ForeignKey = true`, those flags must be set on the obsolete attribute so the controller
can properly clean up indexes and constraints before dropping.

### Database Support for Column Drops

| Database | Drop column supported | Notes |
|----------|:---------------------:|-------|
| SQL Server | yes | Drops indexes and FK constraints automatically |
| PostgreSQL | yes | Standard DROP COLUMN |
| MySQL | yes | Drops FK constraints before column drop |
| Oracle | yes | Also drops sequences for autoincrement columns |
| **SQLite** | **no** | Throws `FeatureNotSupported` |

When `DropColumnSupported` is false (SQLite), the controller silently skips column drops.
The column remains in the table but is ignored by entity queries.

## Adding New Columns

When you add a new `[EntityProperty]` to an existing entity, `UpdateTables` with `Update` mode
detects the missing column and adds it via ALTER TABLE:

```csharp
[Entity(Scope = "myapp", Table = "products")]
public class Product
{
    [AutoId]
    public int Id { get; set; }

    [EntityProperty(DbType = DbType.String, Size = 256)]
    public string Name { get; set; }

    // New column — will be added automatically on UpdateTables
    [EntityProperty(DbType = DbType.Int32, Nullable = true)]
    public int? Rating { get; set; }
}
```

New columns should generally be `Nullable = true` or have a `DefaultValue`, since existing rows
won't have data for them.

## What UpdateTables Cannot Handle

The catalogue controller reconciles columns (add/drop) and indexes (single-column, composite, and JSON),
and always drops+recreates views. It does **not** handle these, and **refuses loudly** rather than
guessing (so nothing silently diverges):

- **Column type / family / geometry-metadata changes** — refused with `CatalogColumnAlterNotSupported`
  (no portable in-place modify, and any automatic form would be destructive). Use a patch.
- **Column renames** — a rename reads as drop+add (no rename detection). Use a patch (add new column,
  copy data, then mark the old one `[ObsoleteEntityProperty]`).
- **Primary key changes** — must recreate the table (`Recreate` mode or a patch).

For these cases, use the **Patch mechanism** (see below).

## Lifecycle Callbacks

Four callback attributes fire at specific points during migration:

| Attribute | Targets | When fired |
|-----------|---------|-----------|
| `[OnEntityCreate]` | class | After table is CREATE'd |
| `[OnEntityDrop]` | class | Before table is DROP'd |
| `[OnEntityPropertyCreate]` | property | After column is ADD'd via ALTER TABLE |
| `[OnEntityPropertyDrop]` | property | After column is DROP'd via ALTER TABLE |

All callbacks reference a static method with signature `void Method(SqlDbConnection connection)`:

```csharp
[Entity(Scope = "myapp", Table = "products")]
public class Product
{
    [AutoId]
    public int Id { get; set; }

    // Populate default value after column is added to existing table
    [OnEntityPropertyCreate(typeof(ProductMigration), nameof(ProductMigration.SetDefaultRating))]
    [EntityProperty(DbType = DbType.Int32, Nullable = true)]
    public int? Rating { get; set; }

    // Clean up before column is dropped
    [ObsoleteEntityProperty(Field = "old_notes")]
    [OnEntityPropertyDrop(typeof(ProductMigration), nameof(ProductMigration.BackupNotes))]
    public string OldNotes { get; set; }
}

internal static class ProductMigration
{
    public static void SetDefaultRating(SqlDbConnection connection)
    {
        using var query = connection.GetQuery("UPDATE products SET rating = 0 WHERE rating IS NULL");
        query.ExecuteNoData();
    }

    public static void BackupNotes(SqlDbConnection connection)
    {
        // Archive data before column drop
    }
}
```

### Listening to Controller Events

For logging or progress tracking:
```csharp
controller.OnAction += (sender, args) =>
{
    Console.WriteLine($"{args.Action}: {args.EntityType?.Name ?? args.Table}");
};
```

## Patch Mechanism — Versioned Custom Migrations

For changes that `UpdateTables` cannot handle (type changes, data transformations, column renames,
etc.), use the patch system. Patches are versioned, tracked in the `ef_patch_history` table, and
applied only once.

With `CatalogEntityController`, **patches are replayed automatically by `UpdateTables`** — you no longer
call `EfPatchProcessor.ApplyPatches` yourself. After the structural diff converges, the controller runs
every discovered patch whose version falls in the window **`(Vc, Vi]`** (newer than the version recorded
in the catalogue, up to and including the version being applied), in `major.minor.patch` order, recording
each in `ef_patch_history`.

Key rules:
- **First contact / `CreateTables` run no patches.** A fresh database is built directly at the target
  version, so everything ≤ that version is already baked into the structure. Patches exist only to migrate
  an *existing* database across versions.
- **Author rule:** a patch may only touch structure that still exists at the target version `Vi` — that
  is what makes replaying a subset of patches safe against the head schema.
- Patches are keyed to the controller's entity scope.

### Defining a Patch

```csharp
[EfPatch("myapp", 1, 0, 1)]  // scope, major, minor, patch
public class RenameDescriptionColumn : IEfPatch
{
    public void Apply(SqlDbConnection connection)
    {
        // Add new column
        using (var q = connection.GetQuery(
            "ALTER TABLE products ADD COLUMN description VARCHAR(1024)"))
            q.ExecuteNoData();

        // Copy data from old column
        using (var q = connection.GetQuery(
            "UPDATE products SET description = old_description"))
            q.ExecuteNoData();

        // Old column will be dropped by [ObsoleteEntityProperty] on next UpdateTables
    }
}
```

For async patches:
```csharp
[EfPatch("myapp", 1, 0, 2)]
public class CreateFullTextIndex : IEfPatchAsync
{
    public void Apply(SqlDbConnection connection) => ApplyAsync(connection).Wait();

    public async Task ApplyAsync(SqlDbConnection connection)
    {
        // ... async migration logic
    }
}
```

### Applying Patches

With the catalogue controller you do not apply patches manually — just deploy the patch classes and
bump the version:

```csharp
// The patch above is [EfPatch("myapp", 1, 0, 1)]. Deploying it and calling:
controller.UpdateTables(connection, "1.0.1", EntityUpdateMode.Update);
// ...replays it automatically if the catalogue's recorded version is < 1.0.1.
```

The legacy manual API still exists and writes the same `ef_patch_history` table
(`EfPatchProcessor.FindAllPatches` + `connection.ApplyPatches(patches, "myapp")`), so the two mechanisms
coexist on one ledger — useful during migration from the old controller. New code should rely on the
automatic catalogue replay instead.

### How Catalogue Patch Replay Works

1. `UpdateTables(conn, Vi, mode)` brings the structure to the current model, then replays patches.
2. The replay window is **keyed on the scope version, not the ledger**: only patches with version in
   `(Vc, Vi]` run — where `Vc` is the version recorded in `ef_catalog` before this run.
3. First contact and `CreateTables` run **no** patches (fresh DB is born at head).
4. Each applied patch is recorded in `ef_patch_history` with a timestamp.
5. Patches are ordered by `major * 10,000,000 + minor * 10,000 + patch`.

### DI Support in Patches

Patches can use dependency injection:
```csharp
[EfPatch("myapp", 1, 1, 0)]
public class MigrateData : IEfPatch
{
    private readonly ILogger _logger;

    public MigrateData(ILogger<MigrateData> logger)
    {
        _logger = logger;
    }

    public void Apply(SqlDbConnection connection)
    {
        _logger.LogInformation("Applying data migration...");
        // ...
    }
}

// Pass service provider when applying
connection.ApplyPatches(patches, "myapp", serviceProvider);
```

### Querying Patch History

```csharp
// Get last applied patch
EfPatchHistoryRecord last = connection.GetLastAppliedPatch("myapp");
Console.WriteLine($"Last patch: {last.MajorVersion}.{last.MinorVersion}.{last.PatchVersion}");

// Get all applied patches
var history = connection.GetAllPatches("myapp");
```

## SQLite Migration Workaround

SQLite does not support DROP COLUMN. For schema changes that require removing columns
on SQLite, use a patch that performs the table rebuild sequence:

```csharp
[EfPatch("myapp", 1, 0, 1)]
public class RebuildProductsTable : IEfPatch
{
    public void Apply(SqlDbConnection connection)
    {
        // 1. Rename old table
        using (var q = connection.GetQuery("ALTER TABLE products RENAME TO products_old"))
            q.ExecuteNoData();

        // 2. Create new table (without the dropped column)
        using (var q = connection.GetCreateEntityQuery<Product>())
            q.Execute();

        // 3. Copy data (list only the columns that remain)
        using (var q = connection.GetQuery(
            "INSERT INTO products (id, name, price) SELECT id, name, price FROM products_old"))
            q.ExecuteNoData();

        // 4. Drop old table
        using (var q = connection.GetQuery("DROP TABLE products_old"))
            q.ExecuteNoData();
    }
}
```

## Recommended Migration Workflow

1. **Initial setup:** `controller.EnsureCatalogInfrastructure(connection)`, then
   `controller.UpdateTables(connection, "1.0.0", EntityUpdateMode.Update)` (or `CreateTables`).
2. **Adding entities/columns:** Add them to the code and **bump the version** — `UpdateTables(Update)`
   handles it.
3. **Removing columns:** Add `[ObsoleteEntityProperty]` and bump the version — `UpdateTables(Update)`
   handles it (except on SQLite — use a patch). An *unmarked* removed column is refused by default
   (data-loss safety).
4. **Removing entities:** Add `[ObsoleteEntity]` and bump the version — `UpdateTables(Update)` drops the
   table (tombstoned in the catalogue).
5. **Complex changes** (type changes, renames, data transforms): Write a patch (`[EfPatch]`) — it replays
   automatically when the version window includes it.
6. **Call order in your initialization:**
   ```csharp
   var controller = new CatalogEntityController(typeof(MyEntity), "myapp");
   controller.EnsureCatalogInfrastructure(connection);   // idempotent; safe to call every startup
   controller.UpdateTables(connection, "1.2.0", EntityUpdateMode.Update);
   // Patches are replayed automatically inside UpdateTables — no separate ApplyPatches call.
   ```

## Switching from the obsolete CreateEntityController

`CreateEntityController` (introspection-based, self-bootstrapping, no version argument) is now
`[Obsolete]` but still functional. New code should use `CatalogEntityController`.

**New / greenfield database** — just use the catalogue controller from the start
(`EnsureCatalogInfrastructure` → `CreateTables`/`UpdateTables`).

**Existing database previously managed by `CreateEntityController`** — the tables already exist but there
is no `ef_catalog` record. Pointing `CatalogEntityController.UpdateTables` straight at it is refused (a
clean "adopt first" signal): first contact with pre-existing tables throws `CatalogOrphanScope`, or with
pre-existing patch-history rows throws `CatalogOrphanPatchHistory` — **zero DDL, catalogue not seeded**.

Adopt the database into the catalogue instead:

```csharp
var controller = new CatalogEntityController(typeof(MyEntity), "myapp");
controller.EnsureCatalogInfrastructure(connection);

// Detect the situation
if (controller.IsOrphanScopeExists(connection))
{
    // Seed ef_catalog from the current model at the version the DB is currently at.
    // TrustModel (recommended): verify the DB already matches the model (throws
    //   SchemaUpdateRequired if not), then record — no DDL.
    // ReconcileToModel (practical): bring the DB into line via the old controller's
    //   introspection reconcile first, then record.
    controller.AdoptExistingScope(connection, "1.5.0",
        CatalogEntityController.CatalogAdoptMode.TrustModel);
}

// From here on, deploy normally — bump the version and UpdateTables:
controller.UpdateTables(connection, "1.6.0", EntityUpdateMode.Update);
```

Notes:
- Pass the version the database is *currently* at to `AdoptExistingScope`; adoption runs no patches and
  sets the `Vc` baseline, so a later `UpdateTables(Vi)` replays only `(Vc, Vi]`.
- If the old code called `connection.ApplyPatches` under a **different scope string** than the entity
  scope, update the `Scope` column of the relevant `ef_patch_history` rows to the entity scope *before*
  adopting (the catalogue replay keys on the entity scope).
- `AdoptExistingScope` throws `CatalogScopeAlreadyAdopted` if the scope already has a catalogue record.

## FK Dependency Order

The controller automatically sorts entities by FK dependencies:
- **Create order:** referenced tables first (Category before Product)
- **Drop order:** dependent tables first (Product before Category)

This is handled by `EntityFinder.ArrangeEntities` which performs a topological sort
on the FK dependency graph.



# Raw SQL, QueryBuilder, and EntityQueryBuilder

## Section 1: SqlQuery (Raw SQL)

For when entity queries aren't enough — custom reports, DDL, database-specific features.

### Execute non-query (DDL/DML)
```csharp
using var query = connection.GetQuery("CREATE INDEX idx_name ON products (name)");
query.ExecuteNoData();
```

### Execute with parameters
```csharp
using var query = connection.GetQuery(
    "UPDATE products SET price = price * @factor WHERE category_id = @catId");
query.BindParam("factor", 1.1);
query.BindParam("catId", 5);
int rowsAffected = query.ExecuteNoData();
```

### Select with reader
```csharp
using var query = connection.GetQuery(
    "SELECT name, SUM(price * stock) as total_value FROM products GROUP BY name");
query.ExecuteReader();
while (query.ReadNext())
{
    string name = query.GetValue<string>(0);
    double total = query.GetValue<double>(1);
}
```

### Null parameters
```csharp
query.BindNull("paramName", DbType.String);
```

### Output parameters
```csharp
query.BindOutputParam("result", DbType.Int32);
query.ExecuteNoData();
int result = query.GetParamValue<int>("result");
```

### SQL injection protection
Enabled by default. To suppress (for dynamic SQL):
```csharp
using var query = connection.GetQuery(dynamicSql, suppressScalarProtection: true);
```

### Async
```csharp
await query.ExecuteNoDataAsync();
await query.ExecuteReaderAsync();
bool hasRow = await query.ReadNextAsync();
```

### Getting a query from a builder
```csharp
var builder = connection.GetSelectQueryBuilder(tableDescriptor);
// ... configure builder ...
using var query = connection.GetQuery(builder);
query.ExecuteReader();
```

## Section 2: QueryBuilder (Low-Level SQL Construction)

QueryBuilder works with `TableDescriptor` instead of entity types. It gives full control over SQL generation while still being database-agnostic.

### TableDescriptor
```csharp
var table = new TableDescriptor { Name = "my_table" };
table.Add(new TableDescriptor.ColumnInfo
{
    Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true
});
table.Add(new TableDescriptor.ColumnInfo
{
    Name = "name", DbType = DbType.String, Size = 128
});
table.Add(new TableDescriptor.ColumnInfo
{
    Name = "value", DbType = DbType.Double, Nullable = true
});
```

### Create / Drop table
```csharp
using var query = connection.GetQuery(connection.GetCreateTableBuilder(table));
query.ExecuteNoData();

using var dropQuery = connection.GetQuery(connection.GetDropTableBuilder(table));
dropQuery.ExecuteNoData();
```

### SELECT with QueryBuilder
```csharp
var builder = connection.GetSelectQueryBuilder(table);
builder.AddToResultset(table["name"]);
builder.AddToResultset(AggFn.Sum, table["value"]);
builder.Where.Property(table["name"]).Is(CmpOp.Like).Parameter("pattern");
builder.AddGroupBy(table["name"]);
builder.OrderBy.Add(table["name"]);

using var query = connection.GetQuery(builder);
query.BindParam("pattern", "A%");
query.ExecuteReader();
```

### INSERT
```csharp
var builder = connection.GetInsertQueryBuilder(table);
using var query = connection.GetQuery(builder);
query.BindParam(table["name"].Name, "test");
query.BindParam(table["value"].Name, 42.5);
query.ExecuteNoData();
```

### UPDATE
```csharp
var builder = connection.GetUpdateQueryBuilder(table);
builder.AddUpdateColumn(table["value"]);
builder.Where.Property(table["name"]).Is(CmpOp.Eq).Parameter("name");
using var query = connection.GetQuery(builder);
query.BindParam(table["value"].Name, 99.9);
query.BindParam("name", "test");
query.ExecuteNoData();
```

### DELETE
```csharp
var builder = connection.GetDeleteQueryBuilder(table);
builder.Where.Property(table["name"]).Is(CmpOp.Eq).Parameter("name");
using var query = connection.GetQuery(builder);
query.BindParam("name", "test");
query.ExecuteNoData();
```

### JOINs with QueryBuilder
```csharp
var builder = connection.GetSelectQueryBuilder(ordersTable);
var join = builder.AddTable(productsTable, TableJoinType.Inner);
join.On.Property(ordersTable["product_id"]).Is(CmpOp.Eq).Property(productsTable["id"]);

builder.AddToResultset(ordersTable["id"]);
builder.AddToResultset(productsTable["name"]);
```

### ALTER TABLE
```csharp
var alter = connection.GetAlterTableQueryBuilder();
alter.AddColumn(table, new TableDescriptor.ColumnInfo
{
    Name = "new_column", DbType = DbType.String, Size = 64, Nullable = true
});
using var query = connection.GetQuery(alter);
query.ExecuteNoData();
```

### Hierarchical (CTE) queries
```csharp
var builder = connection.GetHierarchicalSelectQueryBuilder(
    table, table["parent_id"], rootParameter: "rootId");
builder.AddToResultset(table["id"]);
builder.AddToResultset(table["name"]);
using var query = connection.GetQuery(builder);
query.BindParam("rootId", 1);
query.ExecuteReader();
```

### Views

```csharp
// Create a view from a select builder
var selectBuilder = connection.GetSelectQueryBuilder(productsTable);
selectBuilder.AddToResultset(productsTable["name"]);
selectBuilder.AddToResultset(AggFn.Sum, productsTable["stock"]);
selectBuilder.AddGroupBy(productsTable["name"]);

var viewBuilder = connection.GetCreateViewBuilder("product_stock_summary", selectBuilder);
using (var query = connection.GetQuery(viewBuilder))
    query.ExecuteNoData();

// Drop a view
var dropViewBuilder = connection.GetDropViewBuilder("product_stock_summary");
using (var query = connection.GetQuery(dropViewBuilder))
    query.ExecuteNoData();
```

### Index creation and drop

```csharp
// Create an index via QueryBuilder
var indexBuilder = connection.GetCreateIndexBuilder(table, new CompositeIndex("idx_name_price")
{
    // Add fields to the index
});
using (var query = connection.GetQuery(indexBuilder))
    query.ExecuteNoData();

// Drop an index
var dropIndexBuilder = connection.GetDropIndexBuilder(table, "idx_name_price");
using (var query = connection.GetQuery(dropIndexBuilder))
    query.ExecuteNoData();

// Or use raw SQL for database-specific indexes
using (var query = connection.GetQuery("CREATE INDEX idx_name ON products (name)"))
    query.ExecuteNoData();
```

## Section 3: EntityQueryBuilder

EntityQueryBuilder wraps QueryBuilder with entity metadata — it maps entity property names to table column names automatically.

When you use `connection.GetSelectEntitiesQuery<T>()` and similar methods, they create EntityQueryBuilder internally. You rarely need to use EntityQueryBuilder directly, but it's available when you need the bridge between entity-level and SQL-level:

```csharp
// Access the underlying query builder from an entity query
using var entityQuery = connection.GetSelectEntitiesQuery<Product>();
var selectBuilder = entityQuery.Builder; // AQueryBuilder
```

The key difference:
- **QueryBuilder** — works with `TableDescriptor`, column names, raw SQL concepts
- **EntityQuery** — works with entity types, property names, automatic FK resolution
- **EntityQueryBuilder** — maps between the two

In practice, prefer EntityQuery for entity operations and raw SqlQuery for custom SQL. QueryBuilder is useful when you need database-agnostic SQL generation without entity mapping.



# Gehtsoft.EF Real-World Patterns

## Section 1: DAO Layer Structure

Best practice: separate data access into its own layer with an interface.

```csharp
// Interface defines data operations
public interface IDao : IDisposable
{
    void SaveProduct(Product product);
    Product GetProduct(int id);
    EntityCollection<Product> GetProducts(ProductFilter filter, int? skip, int? limit);
    int GetProductCount(ProductFilter filter);
    void DeleteProduct(Product product);
}

// Implementation wraps SqlDbConnection
public class SqlDao : IDao
{
    private readonly SqlDbConnection mConnection;

    public SqlDao(SqlDbConnection connection)
    {
        mConnection = connection;
    }

    public void Dispose() => mConnection.Dispose();
    // ... implement methods
}
```

For larger projects, use partial classes to organize by entity type:
```
DaoConnection.cs              -- constructor, initialization
DaoConnection.Products.cs     -- Product CRUD
DaoConnection.Categories.cs   -- Category CRUD
DaoConnection.Orders.cs       -- Order CRUD
```

## Section 2: DI Integration

```csharp
// Service wrapping connection factory
public class DaoService : IDaoService
{
    private readonly SqlDbUniversalConnectionFactory mFactory;

    public DaoService(string driver, string connectionString)
    {
        mFactory = new SqlDbUniversalConnectionFactory(driver, connectionString);
    }

    public IDao CreateConnection() => new SqlDao(mFactory.GetConnection());
}

// Extension method for DI registration
public static class ServiceExtensions
{
    public static IServiceCollection AddDao(this IServiceCollection services,
        string driver, string connectionString)
    {
        services.AddSingleton<IDaoService>(new DaoService(driver, connectionString));
        return services;
    }
}
```

## Section 3: Common CRUD Patterns

### Save pattern (insert or update)
```csharp
public void SaveProduct(Product product)
{
    using var query = product.Id < 1
        ? mConnection.GetInsertEntityQuery<Product>()
        : mConnection.GetUpdateEntityQuery<Product>();
    query.Execute(product);
}
```

### Read with optional filter, pagination, and sorting
```csharp
public EntityCollection<Product> ReadProducts(
    ProductFilter filter = null, int? skip = null, int? take = null)
{
    using var query = mConnection.GetSelectEntitiesQuery<Product>();

    if (filter != null)
        ConfigureProductQuery(query, filter);

    query.AddOrderBy(nameof(Product.Name));

    if (skip.HasValue) query.Skip = skip.Value;
    if (take.HasValue) query.Limit = take.Value;

    return query.ReadAll<Product>();
}

public int GetProductCount(ProductFilter filter = null)
{
    using var query = mConnection.GetSelectEntitiesCountQuery<Product>();
    if (filter != null)
        ConfigureProductQuery(query, filter);
    query.Execute();
    return query.RowCount;
}

private static void ConfigureProductQuery(ConditionEntityQueryBase query, ProductFilter filter)
{
    if (!string.IsNullOrEmpty(filter.NameStartsWith))
        query.Where.Property(nameof(Product.Name)).Like(filter.NameStartsWith + "%");
    if (filter.MinPrice.HasValue)
        query.Where.And().Property(nameof(Product.Price)).Ge(filter.MinPrice.Value);
    if (filter.CategoryId.HasValue)
        query.Where.And().Property(nameof(Product.Category)).Eq(filter.CategoryId.Value);
}
```

Notice how `ConfigureProductQuery` accepts `ConditionEntityQueryBase` -- this lets the same filter logic work for both select and count queries.

### Safe delete with dependency check
```csharp
public bool DeleteCategory(Category category)
{
    if (!mConnection.CanDelete(category))
        return false;
    using var query = mConnection.GetDeleteEntityQuery<Category>();
    query.Execute(category);
    return true;
}
```

### Cascading delete (children first)
```csharp
public void DeleteCategoryWithProducts(Category category)
{
    using var transaction = mConnection.BeginTransaction();
    // Delete children first
    using (var q = mConnection.GetMultiDeleteEntityQuery<Product>())
    {
        q.Where.Property(nameof(Product.Category)).Eq(category.Id);
        q.Execute();
    }
    // Then parent
    using (var q = mConnection.GetDeleteEntityQuery<Category>())
        q.Execute(category);
    transaction.Commit();
}
```

## Section 4: Database Initialization Pattern

```csharp
public class SqlDao : IDao
{
    private readonly SqlDbConnection mConnection;

    public SqlDao(SqlDbConnection connection)
    {
        mConnection = connection;
    }

    public void InitializeDatabase(string schemaVersion, bool forceRecreate = false)
    {
        var controller = new CatalogEntityController(GetType(), "myapp");
        controller.EnsureCatalogInfrastructure(mConnection);

        controller.UpdateTables(mConnection, schemaVersion,
            forceRecreate
                ? EntityUpdateMode.Recreate
                : EntityUpdateMode.Update);

        // Database-specific post-init
        ApplyDatabaseSpecificSetup();
    }

    private void ApplyDatabaseSpecificSetup()
    {
        if (mConnection.ConnectionType == UniversalSqlDbFactory.SQLITE)
        {
            using var q = mConnection.GetQuery("PRAGMA case_sensitive_like=true;");
            q.ExecuteNoData();
        }
    }
}
```

## Section 5: Multi-Database Support

The connection type is available via `connection.ConnectionType`:
```csharp
if (connection.ConnectionType == UniversalSqlDbFactory.POSTGRES)
{
    // PostgreSQL-specific index
    using var q = connection.GetQuery(
        "CREATE INDEX IF NOT EXISTS idx_name ON products USING gin(name)");
    q.ExecuteNoData();
}
```

## Section 6: Testing with Gehtsoft.EF

Use SQLite for fast in-memory tests:
```csharp
public class ProductDaoTests : IDisposable
{
    private readonly SqlDbConnection mConnection;

    public ProductDaoTests()
    {
        mConnection = UniversalSqlDbFactory.Create("sqlite", "Data Source=:memory:");
        var controller = new CatalogEntityController(typeof(Product), "myapp");
        controller.EnsureCatalogInfrastructure(mConnection);
        controller.CreateTables(mConnection, "1.0.0");
    }

    public void Dispose() => mConnection.Dispose();

    [Fact]
    public void InsertAndRead()
    {
        var product = new Product { Name = "Test", Price = 9.99, Stock = 5 };
        using (var q = mConnection.GetInsertEntityQuery<Product>())
            q.Execute(product);

        using var select = mConnection.GetSelectOneEntityQuery<Product>(product.Id);
        var loaded = select.ReadOne<Product>();
        loaded.Name.Should().Be("Test");
    }
}
```

### Test fixture pattern for shared setup:
```csharp
public class DatabaseFixture : IDisposable
{
    public SqlDbConnection Connection { get; }

    public DatabaseFixture()
    {
        Connection = UniversalSqlDbFactory.Create("sqlite", "Data Source=:memory:");
        var controller = new CatalogEntityController(typeof(Product), "myapp");
        controller.EnsureCatalogInfrastructure(Connection);
        controller.CreateTables(Connection, "1.0.0");
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Insert reference data used across tests
    }

    public void Dispose() => Connection.Dispose();
}

public class ProductTests : IClassFixture<DatabaseFixture>
{
    private readonly SqlDbConnection mConnection;

    public ProductTests(DatabaseFixture fixture)
    {
        mConnection = fixture.Connection;
    }
}
```

### Async test pattern

```csharp
public class ProductAsyncTests : IClassFixture<DatabaseFixture>
{
    private readonly SqlDbConnection mConnection;

    public ProductAsyncTests(DatabaseFixture fixture)
    {
        mConnection = fixture.Connection;
    }

    [Fact]
    public async Task InsertAndReadAsync()
    {
        var product = new Product { Name = "Widget", Price = 5.99, Stock = 20 };
        using (var q = mConnection.GetInsertEntityQuery<Product>())
            await q.ExecuteAsync(product);

        using var select = mConnection.GetSelectOneEntityQuery<Product>(product.Id);
        var loaded = await select.ReadOneAsync<Product>();
        loaded.Name.Should().Be("Widget");
    }
}
```

### Multi-database test parameterization

To run the same tests against multiple database drivers, use `[Theory]` with connection names:

```csharp
public class MultiDbTests
{
    public static TheoryData<string> Drivers => new TheoryData<string>
    {
        { "sqlite" },
        // Add other drivers as available in the test environment:
        // { "mssql" },
        // { "npgsql" },
    };

    [Theory]
    [MemberData(nameof(Drivers))]
    public void CrudWorksOnAllDrivers(string driver)
    {
        string connStr = driver switch
        {
            "sqlite" => "Data Source=:memory:",
            "mssql" => "Server=...;Database=...;...",
            _ => throw new NotSupportedException(driver)
        };

        using var connection = UniversalSqlDbFactory.Create(driver, connStr);
        var controller = new CatalogEntityController(typeof(Product), "myapp");
        controller.EnsureCatalogInfrastructure(connection);
        controller.CreateTables(connection, "1.0.0");

        // Run test logic...
    }
}
```

## Section 7: Common Mistakes to Avoid

1. **Forgetting `using` on queries** -- queries hold database resources and must be disposed
2. **Wrong FK delete order** -- always delete children before parents
3. **Missing `Size` on string properties** -- will cause runtime errors
4. **Using entity queries without scope** -- entities without matching scope won't be found by the schema controller
5. **Thread-unsafe connection sharing** -- create one connection per thread/request, or use `Lock()`/`LockAsync()`

