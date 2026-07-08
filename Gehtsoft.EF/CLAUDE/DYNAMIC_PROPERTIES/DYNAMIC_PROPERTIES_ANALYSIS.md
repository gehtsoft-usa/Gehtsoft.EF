# Dynamic Entity Properties — Options Analysis

*Research and analysis performed 2026-07-03. Decision made: Option B (see end of document).
The implementation plan is in `DYNAMIC_PROPERTIES_PLAN.md`.*

## Requirements

Add per-row dynamic properties to Gehtsoft.EF entities:

1. Flat list of properties (no hierarchy).
2. Simple data types: string, integer, number (double), date, boolean.
3. Property identified by its name.
4. Access by name: `entity.DynamicProperties.Set("name", value)`,
   `Where().DynamicProperty("p").Gt(5)`.
5. Properties can be searchable (declared beforehand which ones are indexed).
6. Supported at BOTH levels: SQL builder and Entity Query Builder.
7. Works across all 5 drivers: SQLite, MSSQL, MySQL, Postgres, Oracle.

## Codebase facts that shape the decision (verified by exploration)

- **Zero JSON support exists anywhere in the driver layer.** No JSON type names, no JSON
  functions in any `GetSqlFunction` override, nothing. Any JSON-querying option is fully
  greenfield across all 5 `*LanguageSpecifics` classes.
- The dialect-extension mechanism is `SqlFunctionId` enum + per-driver `GetSqlFunction`
  (`Gehtsoft.EF.Db.SqlDb/SqlLanguageSpecifics.cs:806, :505`), plus `TypeName(DbType,...)`
  per driver for DDL. SQLite backs missing functions with UDFs registered in
  `SqliteConnection.cs` (`CreateFunction`).
- `SupportFunctionsInIndexes` is **false for MSSQL and MySQL** — expression indexes on JSON
  extraction wouldn't work there; they'd need computed/generated columns instead. So JSON
  "searchable" = 3 different indexing strategies across 5 drivers.
- Property→column resolution: `EntityQueryWithWhereBuilder.Alias(path)` looks up
  `mItemIndex[path] → EntityQueryItem{ColumnInfo, QueryBuilderEntity}`. A dynamic property
  must either synthesize such an item (join-based) or emit a raw fragment / subquery via
  existing `ConditionBuilder.Query(AQueryBuilder)` paths.
- Join machinery (`QueryWithWhereBuilder.AddTable`, `QueryBuilderEntity.On`) and
  correlated-subquery support already exist at the SQL-builder level — an EAV design needs
  **no dialect changes at all**.
- Read binding: `SelectQueryResultBinder.cs`, built in `SelectEntityQueryBuilder.CreateBinder`;
  post-read hook `IEntitySerializationCallback.AfterDeserealization` — natural seams for
  populating a property bag (batched second query).
- Extension seams for metadata: `AllEntities.AddDiscoverer`, `EntityDescriptor.Tag`.
- Prior art: `EntityQueries/DynamicEntity/` = runtime-declared schema per **type** (still
  fixed columns, one per property) — not per-row EAV, but its `IPropertyAccessor` pattern
  (`DynamicPropertyAccessor`) is reusable. NOTE: type names `DynamicEntityProperty`,
  `IDynamicEntityProperty`, `DynamicPropertyAccessor` are taken — new types must not collide.
- The FTS module (`Gehtsoft.EF.FTS/FtsConnection.cs`) is a working precedent for the whole
  feature shape: side tables + `SqlDbConnection` extension methods + an
  `EntityQueryConditionBuilder` extension building a low-level subquery, binding its
  parameters onto the main query, resolving the main-table PK alias via
  `condition.BaseQuery.ConditionQueryBuilder.Alias(pk.ID, out _)` (see `AddFtsSearch`, line ~576).
- User's original sketch (Property: GetName/GetType/GetValue<T>) — since removed; superseded by this plan.

## Option A — JSON column on the entity table (queried natively)

One JSON column holds the whole bag; filters translate to per-driver JSON extraction.

Per-driver reality:

| Driver | Extract | Searchable/index | Min version |
|---|---|---|---|
| Postgres | `props->>'p'` + cast, `jsonb` | expression index / GIN | 9.4+ |
| MySQL | `JSON_EXTRACT`/`->>` | **generated column** + index | 5.7+ |
| MSSQL | `JSON_VALUE(props,'$.p')` | **computed column** + index | 2016+ |
| Oracle | `JSON_VALUE(... RETURNING ...)` | function-based index | 12c+ |
| SQLite | `json_extract` (JSON1) | expression index | 3.9+/JSON1 |

Pros:
- Whole bag reads/writes with the entity row — single query, atomic, no joins/N+1.
- No supplemental tables; adding a property never touches schema.
- One expression per filtered property; no join explosion.

Cons:
- Largest dialect surface of any option: 5 extraction syntaxes, 3 indexing strategies,
  all greenfield (`SqlFunctionId` additions ×5 drivers, DDL builders for computed/generated
  columns which don't exist today).
- Type fidelity: JSON knows string/number/bool. Dates become ISO strings; `Gt(5)` on a
  number and any date comparison need per-driver CASTs keyed by the property's declared
  type — so a property-type registry is required anyway for correct SQL.
- Changing the searchable set = per-driver ALTER TABLE (computed/generated columns).
- Version floors on every driver; the library currently assumes none.
- Collation/case-sensitivity of string comparison inside JSON differs from plain columns.

## Option B — Supplemental EAV table  ← CHOSEN

Per entity table, a child table e.g. `<table>_props`:
`(owner_id FK → PK, name, prop_type, v_str, v_int, v_real, v_date, v_bool)`,
unique `(owner_id, name)` (API-enforced).

Filtering: `Where.DynamicProperty("p").Gt(5)` → correlated subquery / EXISTS against the
props table — expressible with today's builder machinery. Reading: batched second query
`WHERE owner_id IN (…page ids…)`. Writing: delete+insert rows in the same transaction as
the entity save.

Pros:
- **Pure SQL92 — identical on all 5 drivers, zero LanguageSpecifics changes, no version
  floors.** Fits the library's portability-first architecture.
- Real typed columns → correct comparison/sort semantics for int/real/date/bool with no
  casts; `Gt(5)` picks `v_int` from the declared type.
- Searchable = ordinary index on `(name, v_int)` etc. — one uniform mechanism; can still
  filter on non-indexed properties (slower, but works).
- FK integrity, cascade delete; SQL-builder-level support is trivial (it's just a table).
- Plugs into identified seams: `EntityDescriptor.Tag` for metadata,
  `ConditionBuilder.Query()` for subqueries, binder hook for reads.

Cons:
- Entity + bag = 2 queries (mitigated by batching per result page).
- Saving a bag = several row operations; atomic only within a transaction (supported).
- Each distinct filtered property adds a subquery — verbose SQL for many-property filters
  (DBs optimize correlated subqueries/EXISTS well).
- Classic EAV: rows = entities × properties; type consistency per name enforced only by
  the library (mitigated by the declared-only property registry).

## Option C — Hybrid: opaque JSON bag + searchable-props index table

Entity table gets a JSON/text column read/written as an **opaque serialized string** —
no per-driver JSON SQL at all (client-side `System.Text.Json`; every driver can store a
string/CLOB). Searchable properties are *additionally* written as typed rows to a B-style
index table used only for WHERE/ORDER BY.

Pros: A's single-query bag read with B's portable typed search, and none of A's dialect
work; "searchable declared beforehand" maps 1:1.
Cons: dual-write consistency (same transaction + rebuild tool if declarations change);
non-searchable properties cannot be filtered in SQL at all; storage duplication.

**Kept as the documented v2 evolution path** — the property registry and index-table
components are shared between B and C, so B doesn't paint us into a corner.

## Option D — Real columns at runtime (extend existing DynamicEntity)

Declare properties per type at runtime; `ALTER TABLE ADD COLUMN` materializes them.
Rejected: property set becomes fixed per type (not per-row/ad-hoc), runtime ALTER TABLE is
operationally hostile, SQLite can't modify/drop columns (`DropColumnSupported=false`),
sparse-column waste. Only the accessor pattern is worth reusing.

## Comparison summary

| | A: JSON queried | B: EAV table | C: JSON + index table | D: real columns |
|---|---|---|---|---|
| Driver work | Very high (×5 dialects) | None | None | None |
| Type-correct compare/sort | Weak (casts, date-as-string) | Strong | Strong | Strong |
| Read cost | Best | 2nd query (batched) | Best | Best |
| Write cost | Best | N rows | Bag + N rows, dual-write | Best |
| Searchable subset | Per-driver DDL ×3 strategies | Plain indexes | Plain indexes | Plain indexes |
| Filter non-searchable | Yes | Yes (slow) | No | n/a |
| Min DB versions | New floors on all 5 | None | None | None |
| Ad-hoc per-row props | Yes | Yes | Yes | No |

## Decisions (confirmed with the user, 2026-07-03)

1. **Storage: Option B** — supplemental EAV table.
2. **Loading: opt-in per query** (`IncludeDynamicProperties()`), plus standalone
   `LoadDynamicProperties` call.
3. **Declared-only properties** via a runtime registry (name + type + searchable);
   declarations can be **added/removed during the application lifecycle**.
4. **Per-entity props table** (real FK to owner PK), not a single shared table.
5. **3-column value layout** (`v_str` string, `v_int` bigint, `v_real` double) uniform on
   all drivers; Boolean stored as 0/1 and DateTime as .NET ticks in `v_int`.
   Reason: MSSQL reserves full width for fixed-length columns even when NULL — a 5-column
   layout (separate `v_date`/`v_bool`) wastes ~24 bytes/row there (~20% of heap; the other
   4 engines store NULLs in ~0–4 bytes, so waste is noise). Rejected alternatives:
   MSSQL-only compact layout (breaks the uniform-schema principle that motivated Option B);
   a single "number" column for int+double+bool+date-as-Julian-Date (a double can't hold
   int64 exactly past 2^53, and `Eq` on Julian-Date doubles is fragile — ticks are exact).
   Trade-off accepted: stored dates aren't human-readable in raw SQL and can't feed SQL
   date functions.

→ Implementation plan: `CLAUDE/DYNAMIC_PROPERTIES/DYNAMIC_PROPERTIES_PLAN.md`.

## Decisions — REVISION 2 (2026-07-03, superseding item 3 above; items 1, 2, 4, 5 stand)

After further discussion the **runtime registry / declared-only model was dropped** in favour
of a **registration-less (catalogue-free)** design. Rationale: the registry was doing three
jobs and only paid for itself on one edge:

1. Type consistency per name — *not enforced*; a name may hold different types in different
   rows. Semantics: a query targets one value column (chosen by the operand's type), so a
   row whose value is stored in a *different* column reads as `NULL` there and simply drops
   out of the predicate (`NULL > 5` → UNKNOWN). This is automatic from the EAV layout — no
   catalogue needed.
2. Value-column for a bare name in ORDER BY / resultset — supplied instead by an **explicit
   `EntityPropertyType` argument** at those call sites (WHERE infers it from the operand).
3. Searchable subset — replaced by **three static composite indexes** `(name, v_str)`,
   `(name, v_int)`, `(name, v_real)` created with the table, so *every* property is fast; the
   `Searchable` flag and per-subset index-sync are removed.

Also revised:
- **Save = change-tracking**, not delete-all+insert-all. The bag records touched + removed
  property names; the saver upserts changed rows by `(owner, name)` (a type change updates the
  row in place, nulling the other value columns) and deletes tombstoned ones. This is
  **partial-load safe**: saving an entity whose bag was never loaded (opt-in loading) touches
  nothing, so it can't wipe unloaded properties.

Dropped as a result: `PropertySetRegistry`, `EntityPropertyDeclaration`,
`DynamicPropertyDeclarationAttribute`, the `Changed` event, declared-only enforcement, the
`DynamicPropertyNotDeclared` error, `UpdateDynamicPropertiesSchema` (searchable-subset sync),
and declaration-driven `CleanupDynamicProperties`. `DynamicPropertiesAttribute` stays (marks
the owner + carries `NameSize`/`StringValueSize`/`TableSuffix`). `PLAN.md` reflects rev 2.

---

# Appendix — Architecture reference (exploration results, verified 2026-07-03)

Everything below was verified by reading the code on 2026-07-03. It should be enough to
implement the feature without re-exploring the codebase. All paths relative to the repo
root `Gehtsoft.EF/`.

## Project layout

| Project | Role |
|---|---|
| `Gehtsoft.EF.Entities` | driver-neutral: entity attributes (`Attributes/`), enums; new `PropertySet/` core types go here |
| `Gehtsoft.EF.Db.SqlDb` | shared base: `SqlDbConnection`, `SqlDbQuery`, `SqlLanguageSpecifics`, `QueryBuilder/` (dialect-neutral SQL generation), `EntityQueries/` (entity layer); new `PropertySet/` SQL implementation goes here |
| `Gehtsoft.EF.Db.SqliteDb / MssqlDb / MysqlDb / PostgresDb / OracleDb` | the 5 drivers; each overrides `XxxConnection`, `XxxDbLanguageSpecifics`, some builders |
| `Gehtsoft.EF.FTS` | full-text search side-table module — **the pattern template for this feature** |
| `Gehtsoft.EF.Db.SqlDb.Sql`, `.OData`, `Gehtsoft.EF.MongoDb`, `Gehtsoft.EF.Northwind` | text-SQL parser layer, OData, Mongo (out of scope), test data |
| `Gehtsoft.EF.Test` | xUnit v3 tests |

**Build constraint: both `Gehtsoft.EF.Entities.csproj` and `Gehtsoft.EF.Db.SqlDb.csproj`
(and the test csproj) use `EnableDefaultCompileItems=false` — every new `.cs` file needs an
explicit `<Compile Include=...>` item.** XML doc file generation is on → all new public
members need doc comments or the build warns/fails.

## Dialect mechanism (needed only if Option A/C is ever revisited; the chosen design avoids it)

- `SqlDbLanguageSpecifics` (`Gehtsoft.EF.Db.SqlDb/SqlLanguageSpecifics.cs`) is the dialect
  hub, exposed via `SqlDbConnection.GetLanguageSpecifics()` (`SqlDbConnection.cs:193`).
- Function translation: `enum SqlFunctionId` (`SqlLanguageSpecifics.cs:806`) +
  `virtual GetSqlFunction(SqlFunctionId, string[])` (`:505`), overridden per driver.
  SQLite backs missing functions with UDFs registered in `SqliteConnection.cs:36-181`
  (`connection.CreateFunction`: YEAR/MONTH/.../SLEFT/TOSTRING/TOREAL).
- Type mapping: `TypeToDb(Type, out DbType)` (`SqlLanguageSpecifics.cs:323`) +
  `ToDbValue/TranslateValue`; DDL names via abstract `TypeName(DbType, size, precision,
  autoincrement)` per driver (SQLite: TEXT/INTEGER/REAL/BLOB, dates per
  `SqliteGlobalOptions.StoreDateAsString`; MSSQL `nvarchar/identity(1,1)`; Postgres
  `serial/bigserial/uuid`; Oracle `nvarchar2/number(n)/timestamp(3)`).
- Relevant capability flags: `SupportFunctionsInIndexes` (true SQLite/Postgres/Oracle,
  **false MSSQL/MySQL**), `DropColumnSupported`/`ModifyColumnSupported` (false SQLite),
  `SupportsTransactions` (Nested: SQLite/MSSQL/Postgres; Plain: MySQL/Oracle),
  `AutoincrementReturnedAs` (Oracle=Parameter), Oracle wraps DDL in
  `BEGIN EXECUTE IMMEDIATE ... END;` blocks, Oracle identifiers ≤30 chars.

## Entity metadata chain

- Attributes (`Gehtsoft.EF.Entities/Attributes/`): `EntityAttribute` (Table/Scope/View/
  Metadata/NamingPolicy), `EntityPropertyAttribute` (Field/DbType/Size/Precision/AutoId/
  PrimaryKey/Autoincrement/ForeignKey/Sorted/Unique/Nullable/IgnoreRead/DefaultValue),
  `AutoIdAttribute`, `PrimaryKeyAttribute`, `ForeignKeyAttribute`.
- `TableDescriptor` + nested `ColumnInfo` (`Gehtsoft.EF.Db.SqlDb/QueryBuilder/TableDescriptor.cs`):
  the schema unit. `ColumnInfo`: `ID` (= entity property name), `Name` (SQL column),
  `DbType/Size/Precision`, flags, `ForeignTable` (FK → auto-join + auto-index),
  `IPropertyAccessor PropertyAccessor` (get/set CLR value; interface in the same file).
  `TableDescriptor.Metadata` object: if it implements `ICompositeIndexMetadata`,
  `CreateTableBuilder` emits composite indexes at CREATE TABLE time.
- `EntityDescriptor` (`EntityQueries/EntityDiscovery/EntityDescriptor.cs`): EntityType +
  TableDescriptor + PrimaryKey; **`SetTag<T>/GetTag<T>` slot** — use for caching the
  synthesized props table.
- `AllEntities` singleton (`EntityQueries/EntityDiscovery/AllEntities.cs`): `Get(type)`
  lazily builds/caches descriptors via `IEntityDisoverer` list (`StandardEntityDiscoverer`,
  `DynamicEntityDiscoverer`); `AddDiscoverer`, `OnEntityDiscovered` event, `ForgetType`.
  `ColumnDiscoverer.CreateColumnDescriptor` builds one ColumnInfo from accessor+attribute.

## Query path — how `Where().Property("X").Eq(v)` becomes SQL

1. `EntityQueryConditionBuilder.Property(path)` extension → `SingleEntityQueryConditionBuilder`
   (`EntityQueries/EntityQuery/EntityQueryConditionBuilder.cs`, ~line 98).
2. → `EntityQueryWithWhereBuilder.Alias(path, out DbType)`
   (`EntityQueries/EntityQueryBuilder/EntityQueryWithWhereBuilder.cs`): lookup in
   `mItemIndex` (Dictionary<string, EntityQueryItem>; populated by `AddEntityItems` — path =
   `column.ID`, dotted through FK joins e.g. `"Customer.Name"`); throws ColumnNotFound.
3. → `SelectQueryBuilder.GetAlias(ColumnInfo, QueryBuilderEntity)` (`QueryBuilder/
   SelectQueryBuilder.cs` ~697) → `"entityN.column_name"` raw string into the condition.

**Key mechanism for this feature** — subquery as comparison side:
`SingleEntityQueryConditionBuilder.Query(AQueryBuilder builder, DbType? columnType)`
(`EntityQueryConditionBuilder.cs:178`) calls `builder.PrepareQuery()` then
`Raw(Builder.Query(builder), columnType)`. Works on the LEFT of any operator; the DbType
flows into `Value(object)` (`:205`) so parameter binding is typed. `Value` binds via
`Builder.BaseQuery.NextParam` + `BaseQuery.Query.BindParam`.

**FTS precedent** (`Gehtsoft.EF.FTS/FtsConnection.cs`, `FtsQueryExtension.AddFtsSearch`
`:576`): gets owner PK ColumnInfo from `condition.BaseQuery.EntityQueryBuilder.Descriptor
.PrimaryKey`; resolves its alias via `condition.BaseQuery.ConditionQueryBuilder.Alias(pk.ID,
out _)`; binds subquery params via `condition.BaseQuery.BindParam(name, direction, value,
type)`; composes `condition.Add().Raw(leftSide).Is(CmpOp.In).Query(subquery)`. Copy this
shape for `DynamicProperty(...)`.

## Low-level builders

- `SelectQueryBuilder` (`QueryBuilder/SelectQueryBuilder.cs`): `AddToResultset(ColumnInfo,
  entity, alias)`, `AddExpressionToResultset(rawExpr, DbType, isAggregate, alias)`,
  OrderBy/GroupBy/Having/Distinct/Skip/Limit.
- `QueryWithWhereBuilder` (`QueryBuilder/QueryWithWhereBuilder.cs`): join model —
  `QueryBuilderEntity` (Table/Alias/JoinType/On ConditionBuilder), `AddTable(...)` with
  FK↔PK auto-connect; `TableJoinType {None, Inner, Left, Right, Outer}`.
- `ConditionBuilder` (`QueryBuilder/ConditionBuilder.cs`): `Add(LogOp, left, CmpOp, right)`,
  `PropertyName(entity, column)`, `Parameter`, `Query(AQueryBuilder)` (renders `(SELECT …)`),
  `AddGroup`. `CmpOp` includes `Exists/NotExists/In/NotIn`. Entity-level thin adapter:
  `EntityQueries/EntityQueryBuilder/EntityConditionBuilder.cs`.
- Other builders: `InsertQueryBuilder`, `UpdateQueryBuilder`, `DeleteQueryBuilder`
  (`GetDeleteQueryBuilder(table)` + `Where` for the cleanup/save-delete statements),
  `CreateTableBuilder`, `TableDdlBuilder` (auto-creates FK/Sorted indexes),
  `CreateIndexBuilder`/`DropIndexBuilder` via `SqlDbConnection.GetCreateIndexBuilder/
  GetDropIndexBuilder` (virtual, driver-overridden where needed).
- Existence checks: `connection.DoesObjectExist(tableName, objectName, "table"|"index")` —
  works on all drivers, already used in tests (`Gehtsoft.EF.Test/Entity/Query/
  QueryiesOnDb_Create.cs:234`).

## Read / bind path

- Binder built in `SelectEntityQueryBuilder.CreateBinder` (`EntityQueries/
  EntityQueryBuilder/SelectEntityQueryBuilder.cs`); executed by `SelectQueryResultBinder`
  (`Gehtsoft.EF.Db.SqlDb/SelectQueryResultBinder.cs`) — maps resultset columns to
  `IPropertyAccessor.SetValue`, recurses into FK-joined sub-binders.
- `SelectEntitiesQuery.ReadOne()` calls binder then `IEntitySerializationCallback
  .AfterDeserealization` if the entity implements it (`EntityQueries/
  IEntitySerializationCallback.cs`) — per-entity post-read hook.
- `ReadAll<TC,T>/ReadAllAsync` in `SelectEntitiesQueryBase.cs`; also public
  `AddOrderByExpr(string, SortDir)` (~line 353) and `AddExpressionToResultset(string,
  DbType, string alias)` (~line 542); `ReadAllDynamic` reads into ExpandoObject.
- **`SqlDbQuery` keeps its `DbDataReader` open until dispose** (`Gehtsoft.EF.Db.SqlDb/
  SqlQuery.cs`, dispose logic ~lines 98–101) — a second query on the same connection is
  blocked on MSSQL without MARS; the plan adds `internal void CloseReader()` there.

## Entity CRUD queries

- `InsertEntityQuery`/`UpdateEntityQuery`/`DeleteEntityQuery` derive from
  `ModifyEntityQuery` (`EntityQueries/EntityQuery/ModifyEntityQuery.cs`) — `Execute(object)`
  / `ExecuteAsync` are **virtual**; insert populates the autoincrement PK back into the
  entity (`UpdateQueryToTypeBinder.BindOutputParam`, ~line 445) before `Execute` returns.
- Schema lifecycle: `CreateEntityController` (`EntityQueries/CreateEntity/
  CreateEntityController.cs`) — Create/Drop/UpdateTables actions; the integration point for
  creating/dropping the props table and syncing indexes.
- Transactions: `connection.BeginTransaction()`; the framework never auto-opens
  transactions in any query.

## Prior art / collisions

- `EntityQueries/DynamicEntity/` — runtime-declared schema per TYPE (fixed columns), not
  per-row EAV. **Taken names: `DynamicEntity`, `DynamicEntityProperty`,
  `IDynamicEntityProperty`, `DynamicPropertyAccessor`, `DynamicEntityPropertyCollection`.**
  New types use `EntityProperty*`/`PropertySet*` names; fluent METHOD names may use
  "DynamicProperty/DynamicProperties" wording (methods don't collide with types).
- LINQ layer exists: `EntityQueries/Linq/ExpressionCompiler.cs` (C# expression trees → SQL;
  maps MemberExpression via the same column machinery) — dynamic-property support there is
  explicitly v2, seam = MemberExpression handling.
- Free-name check done: `EntityPropertySet`, `EntityProperty`, `EntityPropertyDeclaration`,
  `PropertySetRegistry`, `EntityPropertyType` have no collisions in the solution.

## Test infrastructure

- xUnit v3. Multi-driver pattern: `[Theory] + [MemberData(nameof(ConnectionNames))]` where
  `ConnectionNames` comes from `SqlConnectionSources.SqlConnectionNames`
  (`Gehtsoft.EF.Test/Utils/SqlConnectionSources.cs`), plus `IClassFixture<T :
  SqlConnectionFixtureBase>` (`Gehtsoft.EF.Test/Utils/SqlConnectionFixtureBase.cs`).
  SQLite always available; other drivers per local test configuration.
- **Template to copy: `Gehtsoft.EF.Test/SqlDb/FtsTest.cs`** (fixture + theory over drivers +
  side-table create/query/drop lifecycle).
- New tests go to `Gehtsoft.EF.Test/Entity/PropertySet/` + explicit csproj Compile items.
- Oracle IN-list limit 1000 → loader chunks at 500.

## User conventions that apply (from memory/CLAUDE.md)

- Don't touch build/packaging beyond adding `<Compile Include>` items (user manages packaging).
- Tests assert INTENDED behavior; product bugs go to KNOWN_BUGS.md, tests are never adapted.
- Prefer high-level (DB-roundtrip) tests over micro unit tests.
- `ArgumentNullException.ThrowIfNull(x, nameof(x))` — always pass the name.
- Never `replace_all` when extracting constants.
