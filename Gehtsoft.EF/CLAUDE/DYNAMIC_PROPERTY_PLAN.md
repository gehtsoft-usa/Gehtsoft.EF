# Dynamic Entity Properties ("Property Set") — Implementation Plan

*Planned 2026-07-03, approved by the user; implementation not started.
Options analysis and decision record: `DYNAMIC_PROPERTY_ANALYSIS.md`.*

## Context

Add per-row dynamic properties to Gehtsoft.EF entities: a flat bag of named values of
simple types (string, integer/long, double, datetime, boolean), settable/gettable by name,
filterable in queries (`Where.DynamicProperty("p").Gt(5)`), with some properties declared
searchable (indexed). Must work on all 5 SQL drivers and at both the SQL-builder and
Entity-Query-Builder levels. Matches the sketch in `CLAUDE/ProperitySet/OVERVIEW.md`.

**Decided approach (after options analysis):** supplemental EAV table, one per entity table
(`<table>_props`), typed value columns — pure SQL92, **zero per-driver LanguageSpecifics
changes**. Rejected: native-JSON querying (5 dialects of greenfield JSON work, weak typing,
version floors), hybrid JSON+index table (kept as a documented v2 evolution path).

**Decisions:** opt-in loading per query; declared-only properties via a runtime registry
(declarations addable/removable during app lifecycle); per-entity props table with a real FK.

**Verified enablers (prior art):**
- `SingleEntityQueryConditionBuilder.Query(AQueryBuilder, DbType?)` (`EntityQueryConditionBuilder.cs:178`)
  puts a `(SELECT …)` on the left of any comparison and feeds its DbType to `Value()` binding —
  every fluent operator (Gt/Eq/Like/In/IsNull/And/Or) works on a subquery unchanged.
- FTS module is the template for the whole shape: `FtsConnection.cs` (side-table DDL via
  connection extensions) and `FtsQueryExtension.AddFtsSearch` (`FtsConnection.cs:576`): resolves
  main-table PK alias via `condition.BaseQuery.ConditionQueryBuilder.Alias(pk.ID, out _)`, binds
  subquery params via `condition.BaseQuery.BindParam`, then `.Raw(left).Is(op).Query(sub)`.
- `CmpOp.Exists/NotExists/In` exist; `CreateIndexBuilder`/`DropIndexBuilder` +
  `DoesObjectExist(table, name, "index")` exist per driver; composite indexes come from
  `TableDescriptor.Metadata is ICompositeIndexMetadata` in `CreateTableBuilder`.
- `ModifyEntityQuery.Execute/ExecuteAsync` are virtual; insert populates autoincrement PK back
  into the entity before returning — owner id available for props save.
- `SelectEntitiesQueryBase` already exposes `AddOrderByExpr(string, SortDir)` and
  `AddExpressionToResultset(string, DbType, alias)` — ORDER BY/resultset via raw expressions.
- `EntityDescriptor.SetTag<T>/GetTag<T>` — cache slot for the synthesized props TableDescriptor.
- **Both csproj use `EnableDefaultCompileItems=false` — every new .cs needs an explicit
  `<Compile Include>` item** (note: user manages packaging; only add Compile items, nothing else).
- Multi-driver tests: `[Theory] + [MemberData(nameof(ConnectionNames))]` from
  `SqlConnectionSources.SqlConnectionNames` + `IClassFixture<SqlConnectionFixtureBase>`;
  `Gehtsoft.EF.Test/.../FtsTest.cs` is the template.
- Name collision: `DynamicEntityProperty`/`DynamicPropertyAccessor` already exist in
  `EntityQueries/DynamicEntity/` — new *types* use the `EntityProperty*`/`PropertySet*` naming;
  fluent *method* names keep the user's "DynamicProperty/DynamicProperties" wording.

## 1. Core model — project `Gehtsoft.EF.Entities`, new folder/namespace `PropertySet/`

New files (all driver-neutral, so MongoDb could implement later):
- `EntityPropertyType.cs` — enum `{ String, Integer, Double, DateTime, Boolean }` (Integer = C# long).
- `EntityPropertyDeclaration.cs` — immutable `(Name, PropertyType, Searchable)` + name validation.
- `PropertySetRegistry.cs` — thread-safe singleton (`Inst`, pattern of `AllEntities`):
  `Declare(Type, name, type, searchable)`, `Remove`, `RemoveAll`, `Find`, `GetDeclarations`,
  `Changed` event (app subscribes and calls schema-sync; registry never touches the DB).
  Re-declaring same name with different type throws. Seeds from attributes on first access.
- `DynamicPropertiesAttribute.cs` — class attribute marking support; options `NameSize=64`,
  `StringValueSize=256`, `TableSuffix="_props"`.
- `DynamicPropertyDeclarationAttribute.cs` — `AllowMultiple` seeding convenience.
- `EntityProperty.cs` — `{ Name, PropertyType, Value, T GetValue<T>() }` (checked conversion).
- `EntityPropertySet.cs` — the bag: `Set(name, value)` (null value = remove; **nulls never
  stored**), typed overloads, `Get<T>`, `TryGet`, `Contains`, `Remove`, `Clear`, `Count`,
  indexer, `IEnumerable<EntityProperty>`; ordinal name dictionary; internal `IsModified` dirty
  flag (loader resets, save skips clean bags on update). Optional owner-type ctor for eager
  validation against the registry.
- `IEntityPropertySetOwner.cs` — `{ EntityPropertySet DynamicProperties { get; } }`.

Canonical usage:
```csharp
[Entity(Table = "doc"), DynamicProperties]
public class Doc : IEntityPropertySetOwner
{
    [AutoId] public int ID { get; set; }
    public EntityPropertySet DynamicProperties { get; } = new EntityPropertySet(typeof(Doc));
}
```

## 2. Props table — `Gehtsoft.EF.Db.SqlDb`, new folder/namespace `PropertySet/`

`PropertySetTableProvider.cs`:
- `GetPropertyTable(EntityDescriptor)` — synthesizes the TableDescriptor once, cached via
  `EntityDescriptor.SetTag`; throws (new EfExceptionCode) if type lacks `[DynamicProperties]`
  or owner has no PK.
- `GetPropertyTable(TableDescriptor owner, PropertySetTableOptions)` — entity-free raw variant.

Table `<owner_table>_props` (suffix short — Oracle 30-char identifiers; document `TableSuffix`
override for long owner names): `Id` (Int64 PK autoincrement), `Owner` (copy of owner PK
DbType/Size, not null, `ForeignTable` = owner → real FK, FK index auto-created by
TableDdlBuilder), `Name` (String NameSize, not null), `PropType` (Int32, not null), and
**three value columns** (all nullable):

| ColumnInfo.ID | Name | DbType | Stores |
|---|---|---|---|
| `StringValue` | `v_str` | String (StringValueSize) | String |
| `IntValue` | `v_int` | Int64 | Integer; Boolean as 0/1; DateTime as .NET ticks (`DateTime.Ticks`, UTC-normalized) |
| `RealValue` | `v_real` | Double | Double |

**3-column layout rationale (decided 2026-07-03):** MSSQL reserves full width for
fixed-length columns even when NULL (~24 bytes/row wasted with five value columns). Folding
Boolean/DateTime into `v_int` cuts that to ~8 bytes/row while staying **uniform across all
5 drivers** (a per-driver layout was rejected — it would breach the zero-driver-branches
design). Ticks chosen over Julian Date double: int64 is exact, so `Eq` on dates is reliable
and roundtrip is lossless; it also makes DateTime behavior identical on all drivers
(bypasses MSSQL 3ms rounding / SQLite text-vs-julian options). Documented trade-off: raw
`v_int` dates are not human-readable in hand-written SQL, and SQL date functions can't be
applied to stored values.

Encoding/decoding lives in ONE place: internal static `PropertySetValueCodec`
(`PropertySet/PropertySetValueCodec.cs`): `Encode(EntityPropertyType, object) →
(column, object)` and `Decode(EntityPropertyType, string/long/double) → object`. Used by
saver, loader, and query translation (parameter values converted at bind time).

`TableDescriptor.Metadata` = private `ICompositeIndexMetadata` yielding composite index
`on` → `(owner, name)`.

Invariants: exactly one `v_*` non-null per row; `(owner, name)` unique — **API-enforced only**
(no composite-unique support in the framework; documented v1 limitation).

Searchable indexes: one per value **column** `(name, v_<col>)`, names `s_str|s_int|s_real`;
an index exists iff ≥1 searchable declaration *mapping to that column* exists (Integer,
Boolean, and DateTime all map to `v_int`). Declaration add/remove never rebuilds the table —
at most CREATE/DROP INDEX. Non-searchable props remain filterable (slow; documented).

## 3. DDL + declaration lifecycle — `PropertySetConnectionExtension.cs` (pattern: FtsConnection)

- `CreateDynamicPropertiesTable(this SqlDbConnection, Type)` (+Async) — CreateTableBuilder +
  index sync.
- `DropDynamicPropertiesTable(...)` — existence-checked drop.
- `UpdateDynamicPropertiesSchema(...)` — idempotent index sync: per value **column**
  (`v_str`/`v_int`/`v_real`) compare "should exist" (any searchable declaration mapping to
  that column, per registry) vs `DoesObjectExist(props, "s_int", "index")`, then
  `GetCreateIndexBuilder`/`GetDropIndexBuilder`.
- `CleanupDynamicProperties(...)` — **explicit** orphan maintenance:
  `DELETE FROM props WHERE name NOT IN (declared)`; nothing automatic on declaration removal.

Modify `EntityQueries/CreateEntity/CreateEntityController.cs`: on Create → create props table
after owner; on Drop → drop props before owner (FK order); on UpdateTables → create-if-missing
+ always `UpdateDynamicPropertiesSchema`; drop-phase drops props before owner.

## 4. Query DSL — `PropertySetQueryExtension.cs` + `PropertySetQueryHelper.cs`

Extensions on `EntityQueryConditionBuilder`/`SingleEntityQueryConditionBuilder`:
`DynamicProperty(name)`, `HasDynamicProperty(name)`, `HasNoDynamicProperty(name)`.

Translation — **correlated scalar subquery** as the comparison side (design decision; row-for-row
equivalent to EXISTS for positive predicates given the uniqueness invariant, and optimizers
execute it as the same semi-join):
1. Registry lookup (throws `DynamicPropertyNotDeclared` if absent — declared-only enforcement)
   → value column + DbType via `PropertySetValueCodec` (Integer/Boolean/DateTime → `v_int`,
   DbType.Int64; comparison values converted at bind time: bool → 0/1, DateTime → ticks —
   the `DynamicProperty(...)` extension returns a small wrapper so `.Gt(dateTimeValue)`
   converts transparently).
2. Props table via `PropertySetTableProvider`; owner PK alias via
   `condition.BaseQuery.ConditionQueryBuilder.Alias(pk.ID, out _)` (FTS pattern).
3. Low-level `SelectQueryBuilder` on props: resultset = the value column;
   `owner = <pk alias>` (Raw) and `name = :p` (param bound on the main query via
   `BaseQuery.NextParam` + `BaseQuery.Query.BindParam`).
4. `singleBuilder.Query(subqueryBuilder, valueDbType)` → renders
   `(SELECT tp.v_int FROM doc_props tp WHERE tp.owner = entity0.id AND tp.name = :p1) > :p2`;
   all existing operators, And/Or/groups, and `Value()` typing work unchanged.

Semantics: `.IsNull()` = property absent (values never stored as NULL); `.Neq(v)` on absent →
SQL UNKNOWN → excluded (documented); presence via `HasDynamicProperty` → `CmpOp.Exists` +
EXISTS subquery.

ORDER BY / resultset (in scope, cheap — same subquery string):
`AddOrderByDynamicProperty(this SelectEntitiesQueryBase, name, SortDir)` → `AddOrderByExpr`
(ticks/0-1 order identically to the semantic values, so sorting is correct as-is);
`AddDynamicPropertyToResultset(this SelectEntitiesQueryBase, name, alias)` →
`AddExpressionToResultset`. NOTE: resultset values arrive **encoded** (dates as ticks,
bools as 0/1) — documented; readers decode via `PropertySetValueCodec`-backed public helper
or `EntityProperty.GetValue<T>`. LEFT-JOIN optimization noted as possible v2.

`PropertySetQueryHelper.cs` — the raw-level (requirement 6) API reused by the entity layer:
`GetValueColumn(propsTable, type)`, `CreateValueSubquery(connection, propsTable, ownerRefAlias,
nameParam, type)`, `CreateExistsSubquery(...)`; documented recipe + test for raw
`SelectQueryBuilder` users.

## 5. Loading — opt-in

- `PropertySetLoader.cs` (internal): `SELECT owner, name, prop_type, v_str, v_int, v_real
  FROM props WHERE owner IN (…)`, chunked at 500 ids (Oracle limit 1000), decodes values via
  `PropertySetValueCodec` (prop_type drives decoding: ticks → DateTime, 0/1 → bool), maps to
  bags via ownerId→entity dictionary (PK read through `descriptor.PrimaryKey.PropertyAccessor`),
  clears bags first, resets `IsModified`.
- Public: `LoadDynamicProperties<T>(this SqlDbConnection, IEnumerable<T>)` (+Async, +single).
- `SelectEntitiesQuery.IncludeDynamicProperties()` flag; in `ReadAll/ReadAllAsync`, after the
  read loop: close reader, then batch-load. Requires new `internal void CloseReader()` on
  `SqlDbQuery` (`SqlQuery.cs` — dispose logic already exists there) — needed because the open
  DataReader blocks a second query on MSSQL without MARS.
- `ReadOne()` + the flag: not batched (reader open) — documented; use `LoadDynamicProperties`
  after disposing the query.

## 6. Saving

- `PropertySetSaver.cs` (internal): **delete-all + insert-all** per entity
  (`DELETE … WHERE owner=@pk`, then prepared insert per entry, values encoded via
  `PropertySetValueCodec`). Chosen over merge-diff: bags small, no row-identity tracking,
  preserves uniqueness invariant. Save-time validation of every entry against the registry
  (type-checked) → `DynamicPropertyNotDeclared` / `DynamicPropertyTypeMismatch`.
- Auto-integration: in `ModifyEntityQuery.Execute/ExecuteAsync` after `mBinder.BindAndExecute`
  (PK populated), if type has `[DynamicProperties]` (cached via descriptor tag) and entity is
  `IEntityPropertySetOwner` → save bag (`IsModified==false` skips on update).
  `DeleteEntityQuery` deletes props rows before the owner row.
- Public standalone: `SaveDynamicProperties` / `DeleteDynamicProperties` (+Async).
- Transactions: statements run on the same connection; ambient `BeginTransaction()` covers
  them (framework never auto-opens transactions — matches existing behavior). Documented +
  rollback test.

## 7. Errors

Add to `EfExceptionCode` in `Gehtsoft.EF.Db.SqlDb/EfSqlException.cs`:
`DynamicPropertiesNotSupported`, `DynamicPropertyNotDeclared`, `DynamicPropertyTypeMismatch`
(+ resource messages per existing pattern).

## Implementation order (reviewable increments)

1. **Core model (no DB)** — `Gehtsoft.EF.Entities/PropertySet/*` (8 files) + csproj Compile
   items. Tests: `PropertySetCoreTest.cs` (registry lifecycle/conflicts/attribute seeding/
   Changed event; bag semantics; GetValue<T> conversions).
2. **Table synthesis + DDL** — `PropertySetTableProvider.cs`, `PropertySetConnectionExtension.cs`
   (create/drop/update-schema/cleanup), `EfSqlException.cs` codes, `CreateEntityController.cs`
   integration, csproj. Tests: `PropertySetDdlTest.cs` (multi-driver: table+`on` index created,
   searchable index appears/disappears on declare/remove+sync, cleanup, idempotent UpdateTables).
3. **Save/load + CRUD integration** — `PropertySetValueCodec.cs`, `PropertySetSaver.cs`,
   `PropertySetLoader.cs`, extension methods, `ModifyEntityQuery.cs`, `DeleteEntityQuery.cs`.
   Tests: `PropertySetCrudTest.cs` (multi-driver roundtrip of all 5 types — DateTime roundtrip
   is **exact** thanks to ticks encoding, assert equality not tolerance; codec unit tests
   incl. bool/date encode-decode and UTC normalization; update/remove, cascade delete,
   IsModified skip, transaction rollback).
4. **Query DSL + include** — `PropertySetQueryHelper.cs`, `PropertySetQueryExtension.cs`,
   `SelectEntitiesQuery.cs` (`IncludeDynamicProperties`), `SqlQuery.cs` (`CloseReader`). Tests:
   `PropertySetQueryTest.cs` (all operators × types, Has/HasNo, composition with regular
   Property() conditions, two dynamic props in one query, undeclared throws, ORDER BY,
   resultset via ReadAllDynamic, count-query compat), `PropertySetIncludeTest.cs` (>500 rows
   chunking, empty bags, combined with filters, async), `PropertySetLowLevelTest.cs`
   (raw recipe + generated-SQL text assertions; SQLite sufficient).
5. **Docs** — docgen `.ds` pages under `doc/`, XML doc comments on public members.

All test files go in new folder `Gehtsoft.EF.Test/Entity/PropertySet/` + csproj Compile items.

## Verification

- `dotnet test` on `Gehtsoft.EF.Test` — new PropertySet tests parameterized over enabled
  drivers via `SqlConnectionSources.SqlConnectionNames` (SQLite always; others per test config).
- DDL assertions via `DoesObjectExist(table, name, "table"/"index")`.
- SQL-text snapshot assertions for the subquery shape at the low level.
- Full solution build (XML docs enabled → doc comments required on new public API).

## Out of scope / v2 notes

- `DynamicEntity` support (can implement `IEntityPropertySetOwner` itself, nothing special built).
- MongoDb implementation (core types are driver-neutral by placement).
- JSON bag column as read-optimization (hybrid option C) — registry/index-table design is shared.
- LEFT-JOIN-based ORDER BY/resultset optimization; DB-level `(owner,name)` unique constraint.
- LINQ (`ExpressionCompiler`) support for dynamic properties.
