# PHASE 1 — Dynamic-property **table management**

*Planned 2026-07-04. Scope = everything needed to **stand up and tear down** the per-entity
dynamic-property side table (`<table><suffix>`) — from recognising that an entity opts in, to
synthesising the props table's schema, to creating/dropping it and wiring that into
`CreateEntityController`. No values, no codec, no save/load, no query surface — those are
later phases. Corresponds to Slice 0 (foundation, table part only) + Slice 1 of
`../DYNAMIC_PROPERTIES_PLAN.md`.*

## Props table shape this phase must produce (from the top-level plan)

`<owner_table><TableSuffix>` (default suffix `_props`):

| ColumnInfo.ID | Column | DbType | Notes |
|---|---|---|---|
| `Id` | `id` | Int64 | PK, autoincrement |
| `Owner` | `owner` | copy of owner PK type/size | not null, `ForeignTable`=owner → real FK (+ auto FK index) |
| `Name` | `name` | String(`NameSize`) | not null |
| `PropType` | `prop_type` | Int32 | not null |
| `StringValue` | `v_str` | String(`StringValueSize`) | nullable |
| `IntValue` | `v_int` | Int64 | nullable |
| `RealValue` | `v_real` | Double | nullable |

Composite indexes (via `ICompositeIndexMetadata` on the synthesized `TableDescriptor`):
`(owner, name)`, `(name, v_str)`, `(name, v_int)`, `(name, v_real)`; **plus** the single-column
FK index on `owner` the framework auto-creates ⇒ **5 indexes total**. `(owner, name)`
uniqueness is API-enforced later, no DB unique constraint.

## Task breakdown (finish-before-advance within the phase)

### Task 1 — Entities recognition  ✅ *implemented 2026-07-04 (6 tests green)*
Make the framework recognise opt-in and expose it; close the Mongo/Bson door.

**1a. `DynamicPropertiesAttribute`** *(new)* — `Gehtsoft.EF.Entities/Attributes/DynamicPropertiesAttribute.cs`
- Class-level, inherited (matches `EntityAttribute`); carries the side-table shaping options.
  ```csharp
  [AttributeUsage(AttributeTargets.Class, Inherited = true)]
  public class DynamicPropertiesAttribute : Attribute
  {
      public const string TableSuffix = "_props";  // FIXED: <table>_props (see rationale below)
      public int NameSize { get; set; } = 64;             // name column width
      public int StringValueSize { get; set; } = 256;     // v_str column width
      public int RealValueSize { get; set; } = -1;        // v_real digits; -1 = driver default
      public int RealValuePrecision { get; set; } = -1;   // v_real decimals; -1 = driver default
  }
  ```
  **`TableSuffix` is a fixed `const`, not configurable.** Rationale: the side table must always
  be locatable by name — especially when reconciling schema *after* the attribute is removed
  (Task 4 "lost properties"). A per-entity custom suffix would be unrecoverable then. Sizing
  options stay configurable because they don't affect the table *name* needed for a drop.
- XML doc comments on type + every member (XML-doc generation is on for this csproj).
- **Build:** add `<Compile Include="Attributes\DynamicPropertiesAttribute.cs" />` to
  `Gehtsoft.EF.Entities.csproj` (`EnableDefaultCompileItems=false`).

**1b. Recognition on `EntityDescriptor`** *(edit)* — the result lives on `EntityDescriptor`,
so it is read where the descriptor is built (`AllEntities`), **not** in the discoverer.
`TableDescriptor` is untouched this task — the props `TableDescriptor` is *synthesised
separately* in Task 2; the owner's own `TableDescriptor` never carries this flag.
- **`EntityDescriptor`** (`.../EntityDiscovery/EntityDescriptor.cs`): owns the state.
  ```csharp
  public DynamicPropertiesAttribute DynamicProperties { get; internal set; }  // null ⇒ not opted in
  public bool HasDynamicProperties => DynamicProperties != null;
  ```
  needs `using Gehtsoft.EF.Entities;`.
- **`AllEntities`** (`.../EntityDiscovery/AllEntities.cs`): set `DynamicProperties =
  type.GetCustomAttribute<DynamicPropertiesAttribute>()` at both `EntityDescriptor`
  construction sites — the `this[Type type, bool]` indexer getter and `PreloadEntities`
  (both `using System.Reflection;` and `using Gehtsoft.EF.Entities;` already present).

**1c. Mongo/Bson exclusion** *(edit ×2)*
- `Gehtsoft.EF.Bson/BsonException.cs`: add `BsonExceptionCode.DynamicPropertiesNotSupported`
  + a `case` in the `Message` switch → `"Entities with dynamic properties are not supported by
  the BSON/Mongo layer"`.
- `Gehtsoft.EF.Bson/AllEntitiesExtension.cs`: at the top of `CreateBsonEntityDescription`,
  ```csharp
  if (descriptor.HasDynamicProperties)
      throw new BsonException(BsonExceptionCode.DynamicPropertiesNotSupported);
  ```

**1d. Tests** — folder `Gehtsoft.EF.Test/DynamicProperties/Entities/`, namespace
`Gehtsoft.EF.Test.DynamicProperties.Entities` (test csproj uses **default compile items**;
no csproj edit — this corrects the stale note in the top-level plan/memory). Pure
metadata/reflection, no DB ⇒ plain `[Fact]`s (the deep/acceptance driver split begins at
Task 5, once a real table exists).
- `DynamicPropertiesRecognitionTest.cs`: local `[Entity]` types — plain, defaulted
  `[DynamicProperties]`, and custom `[DynamicProperties(NameSize=32, StringValueSize=1024)]`
  (each with a PK). Assert `HasDynamicProperties` true/false;
  `DynamicProperties` null vs non-null with matching options `64/256/"_props"` and
  `32/1024/"_dyn"`; attribute defaults on `new DynamicPropertiesAttribute()`.
- `DynamicPropertiesBsonExclusionTest.cs`: `[DynamicProperties]` entity →
  `AllEntities.Inst.FindBsonEntity(type)` throws `BsonException` with
  `Code == DynamicPropertiesNotSupported`; a plain sibling `[Entity]` still builds its
  `BsonEntityDescription` fine.

### Task 2 — EAV `TableDescriptor` synthesis  ✅ *implemented 2026-07-04 (7 tests green)*
Placement per user: **`SqlDb/EntityDiscovery`** (not a separate `PropertySet/` folder). The
synthesised descriptor is a plain `TableDescriptor` so later slices can use the EAV table as a
regular table in the query builder.

**2a. Builder** *(new)* —
`Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/DynamicPropertiesTableBuilder.cs`
(internal static). `TableDescriptor Build(EntityDescriptor owner)`:
- Guard: `owner.TableDescriptor.PrimaryKey` must exist, else
  `throw new EfSqlException(EfExceptionCode.NoPrimaryKeyInTable, owner.TableDescriptor.Name)`
  (reuse the existing code — no new enum member needed).
- Table name = `owner.TableDescriptor.Name + owner.DynamicProperties.TableSuffix`.
- Columns (`ID` = logical, `Name` = SQL):

  | ID | Name | DbType | Size | flags |
  |---|---|---|---|---|
  | `Id` | `id` | Int64 | – | PK, Autoincrement, not null |
  | `Owner` | `owner` | *copy of owner PK* DbType/Size/Precision | | not null, `ForeignTable`=owner `TableDescriptor` (⇒ real FK + auto FK index) |
  | `Name` | `name` | String | `NameSize` | not null |
  | `PropType` | `prop_type` | Int32 | – | not null |
  | `StringValue` | `v_str` | String | `StringValueSize` | nullable |
  | `IntValue` | `v_int` | Int64 | – | nullable |
  | `RealValue` | `v_real` | Double | – | nullable |

- `Metadata` = a private `ICompositeIndexMetadata` holder with four `CompositeIndex`
  (unassociated, columns added by SQL name): `owner_name`(owner,name), `name_str`(name,v_str),
  `name_int`(name,v_int), `name_real`(name,v_real). Combined with the auto FK index on
  `owner` → the 5-index set the Task 5 deep test will pin. *(Index metadata is included now so
  the descriptor is complete/creation-ready; exact index names may be revisited in Task 5.)*

**2b. Expose on `EntityDescriptor`** *(edit)* — lazy, cached, no discovery-time cost / no throw
on unrelated entities:
```csharp
private TableDescriptor mDynamicPropertiesTable;
public TableDescriptor DynamicPropertiesTable
    => HasDynamicProperties ? (mDynamicPropertiesTable ??= DynamicPropertiesTableBuilder.Build(this)) : null;
```
`null` when the entity has no dynamic properties; built on first access otherwise.

**2c. Tests** — folder `Gehtsoft.EF.Test/DynamicProperties/TableManagement/`, namespace
`Gehtsoft.EF.Test.DynamicProperties.TableManagement`. Per user, this task only needs to prove
the EAV `TableDescriptor` **is present when needed and correctly filled** — pure metadata, no
DB, plain `[Fact]`s:
- non-opted-in entity → `DynamicPropertiesTable` is `null`.
- opted-in entity → non-null; table name = owner name + suffix; the 7 columns exist with the
  right `ID`/`Name`/`DbType`/`Size`/nullability; `Id` is the PK & autoincrement; `Owner` is a
  FK to the owner table with the owner PK's DbType; `Name`/`v_str` sizes honour the attribute
  (default and custom).
- `Metadata` exposes the four composite indexes with the expected columns.
- same instance returned on repeated access (caching).
- opted-in entity with **no PK** → accessing `DynamicPropertiesTable` throws
  `EfSqlException` (`NoPrimaryKeyInTable`).

### Task 3 — Automatic side-table create / drop via **multi-table DDL builders**  ✅ *implemented 2026-07-04*
*(No table-update/sync yet — next task. No new connection extension, no `EntityQuery` subclass.)*

**Principle.** The owner table **and** its dynamic-property side table must be created/dropped by
one entity query, and this must hold even for the **public** `GetCreateEntityQueryBuilder` — so a
user calling it directly on a dynamic-properties entity can't get a single-table builder that
silently omits the side table. So the fix lives in the **DDL builders**: teach `CreateTableBuilder`
/ `DropTableBuilder` to emit *N* tables in one query. `EntityQueryBuilder`/`EntityQuery` stay pure
pass-throughs; `GetCreate/DropEntityQuery` and `CreateEntityController` inherit it with no changes.

**3a. `CreateTableBuilder.cs` (base) — accept `TableDescriptor[]`.**
- Replace single `mDescriptor` with `List<TableDescriptor> mDescriptors`. Keep the existing
  `(specifics, TableDescriptor)` constructor (seeds a one-element list ⇒ **backward compatible**;
  the five driver subclasses call `base(specifics, table)` and only inject `DdlBuilder`, so **no
  driver create change**). Add `public void AddTable(TableDescriptor td)`.
- `PrepareQuery`: `PreBlock`, then **loop** today's per-table body (lines 47–77) over
  `mDescriptors`, `PostBlock` once at the end. The one injected DdlBuilder is **stateless** (3b),
  so it serves every table with no retargeting — each column resolves its own table via
  `column.Table`.
- `HandleCompositeIndex` / `HandleCompositeIndexColumns` take the current `TableDescriptor` param
  instead of reading `mDescriptor` (both base-only, not driver-overridden ⇒ safe). Composite
  indexes come from `desc.Metadata` per table.
- No create-builder subclass overrides `PrepareQuery` ⇒ **zero driver create changes.**

**3b. `TableDdlBuilder.cs` — make it stateless (drop stored `mDescriptor`).** Every DDL method
already receives a `column`, and `column.Table` is the owning table (`TableDescriptor.Add` sets it;
`AlterTableQueryBuilder.Prepare` defensively sets it; MySQL's `HandlePostfixDDL` already uses
`column.Table.Name`). So remove the `mDescriptor` field and read `column.Table.Name` in the only
three spots that used it: base `HandleAfterQuery` (index name + `ON`), `MssqlTableDdlBuilder`
`HandlePostfixDDL` (constraint name), `OracleTableDdlBuilder` `HandleAfterQuery` (sequence name).
Drop the now-unused `TableDescriptor` argument from the `TableDdlBuilder` constructors and every
construction site: base + the 5 driver `TableDdlBuilder` ctors, the 5 driver `CreateTableBuilder`
ctors that inject `new XxxTableDdlBuilder(...)`, and `AlterTableQueryBuilder.CreateDdlBuilder()`
(base + 4 drivers). No mutable state ⇒ no thread-safety trap, no ordering coupling; no method
signature churn.

**3c. `DropQueryBuilder.cs` (base `DropTableBuilder`) — accept `TableDescriptor[]`.**
- `List<TableDescriptor> mDescriptors` + one-element seed + `AddTable` (as 3a).
- Refactor `PrepareQuery` to loop and delegate each table to a new
  `protected virtual void AppendDropTable(StringBuilder, TableDescriptor)` (base body:
  `DROP TABLE IF EXISTS <name>`). Join multiple statements with `;`+newline; **single-table output
  stays byte-for-byte unchanged** (no trailing terminator) to protect existing tests.
- **The only driver-level change in this task:** the two drop overrides move from overriding
  `PrepareQuery` to overriding `AppendDropTable` (they reference a single `mDescriptor`):
  `MssqlDropQueryBuilder` (`IF OBJECT_ID(..) IS NOT NULL DROP TABLE ..;` per table) and
  `OracleDropTableBuilder` (its drop-sequence + drop-table `EXCEPTION` sub-blocks per table; base
  loop wraps once in `PreBlock`/`PostBlock`). SQLite/MySQL/Postgres use the base ⇒ no change.

**3d. Entity wiring (`EntityConnection.cs`).**
- `GetCreateEntityQueryBuilder(type)`: if `descriptor.HasDynamicProperties`,
  `var b = connection.GetCreateTableBuilder(descriptor.TableDescriptor); b.AddTable(descriptor.DynamicPropertiesTable); return b;`
  (owner first, side second ⇒ FK-safe create order). Else unchanged.
- `GetDropEntityQueryBuilder(type)`: if `HasDynamicProperties`,
  `var b = connection.GetDropTableBuilder(descriptor.DynamicPropertiesTable); b.AddTable(descriptor.TableDescriptor); return b;`
  (side first, owner second ⇒ FK-safe drop order). Else unchanged.
- No changes to `EntityQuery`, `EntityQueryBuilder`, or `CreateEntityController`.

**3e. Tests**
- **Deep (SQLite in-memory, `SqliteDbConnectionFactory.CreateMemory()`),** namespace
  `Gehtsoft.EF.Test.DynamicProperties.TableManagement`:
  1. **created when needed** — `GetCreateEntityQuery<OwnerWithProps>().Execute()` ⇒ owner + `_props`
     tables exist **and** all **5 indexes** exist (`DoesObjectExist(propsTable, obj, "index")` for
     `owner`, `owner_name`, `name_str`, `name_int`, `name_real`).
  2. **not created when not needed** — `GetCreateEntityQuery<PlainEntity>().Execute()` ⇒ owner
     table exists, no `_props` table.
  3. **drops in both cases** — after (1), `GetDropEntityQuery<OwnerWithProps>().Execute()` ⇒ neither
     table remains; `GetDropEntityQuery<PlainEntity>().Execute()` and dropping an opted-in entity
     whose `_props` was never created both complete without error. Async create+drop happy path too.
  *(No SQL-text assertion: the public-builder guarantee is proved behaviourally by (1) creating
  both tables through the builder path. If we ever assert generated SQL, do it via parse→AST like
  `SqlQueryBuilder/Drop.cs`, never string `Contains`.)*
- **Acceptance (all drivers)** — `[Theory][MemberData(nameof(ConnectionNames), "")]` +
  `IClassFixture<SqlConnectionFixtureBase>` (mirror `SqlDb/FtsTest.cs`): per connection,
  `GetDropEntityQuery<Owner>().Execute()` (clean slate) → `GetCreateEntityQuery<Owner>().Execute()`
  → `GetDropEntityQuery<Owner>().Execute()` completes without error. SQLite always on.
- **Regression** — run `Legacy/CreateAndDropTests`, `Legacy/DbUpdateTests`,
  `Entity/Query/QueriesOnDb_Create` to confirm single-table create/drop output/behaviour is unchanged.

**Scope note.** `CreateEntityController` picks create/drop up automatically; **side-table schema
sync / `UpdateTables` is explicitly deferred to the next task.**

### Task 4 — `CreateEntityController.UpdateTables` side-table sync  ✅ *implemented 2026-07-04*
`CreateTables`/`DropTables` already handle the side table automatically (they call
`GetCreate/DropEntityQuery`, wired in Task 3). The only remaining case is **`UpdateTables`
against an already-existing owner table** — the two transitions:
1. **Gained** `[DynamicProperties]` (owner table exists, no `_props` table yet) ⇒ **create** the
   `_props` table.
2. **Lost** `[DynamicProperties]` (owner table exists, orphan `_props` table remains) ⇒ **drop**
   the orphan `_props` table.

**Where.** In `UpdateTables`, the *"create and/or update tables"* loop's **`else` branch**
(existing table, mode ≠ Recreate), after the add/drop-column handling. The top `if` (table
missing or Recreate) already routes through `ActionController.Create` ⇒ side table handled by
Task 3; nothing to add there.

**Logic** (using the already-fetched `schema` and `descriptor = AllEntities.Inst[info.EntityType]`):
- Gained: `if (descriptor.HasDynamicProperties && !schema.Contains(descriptor.DynamicPropertiesTable.Name))`
  → run `GetCreateTableBuilder(descriptor.DynamicPropertiesTable)` (creates the `_props` table +
  its 5 indexes; owner already exists so the FK resolves). `RaiseUpdate(info.Table)`.
- Lost: entity no longer opted in, so `descriptor.DynamicPropertiesTable` is `null`. Because the
  suffix is a **fixed const** (`DynamicPropertiesAttribute.TableSuffix`), the orphan name is
  always `info.Table + DynamicPropertiesAttribute.TableSuffix` — unambiguous, no lost-suffix
  problem. **Guard against false positives**: only drop if the schema table both exists **and**
  has the EAV signature columns (`prop_type` + `v_int`) — so a coincidentally-named user table
  isn't destroyed. Drop via a minimal reconstructed descriptor (name + autoincrement `id` column,
  so Oracle also drops the `<props>_id` sequence). `RaiseUpdate(info.Table)`.

(The fixed `TableSuffix` const was introduced in Task 1a specifically to make this reconciliation
unambiguous — there is no custom-suffix limitation.)

**Tests** — `Gehtsoft.EF.Test.DynamicProperties.TableManagement`, deep SQLite in-memory, driving
`CreateEntityController.UpdateTables(connection, Update)` scoped to the test entities. To simulate
a transition on one table, use two entity classes with the **same `Table`** in **different scopes**
(one with `[DynamicProperties]`, one without):
- **Gained**: create only the owner table (raw `GetCreateTableBuilder(ownerDescriptor)`), then
  `UpdateTables` scoped to the opted-in entity ⇒ `_props` table + its 5 indexes now exist.
- **Lost**: `GetCreateEntityQuery<WithProps>().Execute()` (owner + `_props`), then `UpdateTables`
  scoped to the non-opted-in same-table entity ⇒ owner remains, `_props` dropped.
- **Idempotent**: `UpdateTables` twice for an opted-in entity ⇒ second run is a no-op (props
  table already present, still there, no error).
- **False-positive guard**: a plain entity plus a coincidental same-named `*_props` table that
  lacks the signature columns is **not** dropped.
(Create/drop acceptance across drivers already covered in Task 3.)

**Int64 auto-id validation** — `Int64AutoIdCrudTest` (`DynamicProperties.TableManagement`,
`[Theory]` over all configured connections): the props table's PK is `Int64` autoincrement, which
the existing entity tests never exercised (all use Int32 `[AutoId]`). This test creates an entity
with an explicit `[EntityProperty(PrimaryKey=true, Autoincrement=true)] long Id` and drives
insert (auto-id read-back) → read-by-id → update → delete → drop on every driver, via the direct
entity-query API (no LINQ layer). Confirmed: all five drivers' `TypeName` already emit valid
`Int64` autoincrement DDL (`bigint identity` / `bigint AUTO_INCREMENT` / `bigserial` /
`NUMBER`+sequence / `INTEGER PK`).

## Out of scope for this phase (later phases)
- `EntityPropertyType` / `EntityProperty` / `EntityPropertySet` bag + change tracking /
  `IEntityPropertySetOwner`; `PropertySetValueCodec`; save/load; WHERE/ORDER BY/aggregate/
  group by/having; docs.

## Test namespace scheme (whole feature, all phases)
Under the existing `Gehtsoft.EF.Test` root, folders mirror namespaces:
- `Gehtsoft.EF.Test.DynamicProperties.Entities` — attribute recognition, Bson exclusion (Task 1).
- `Gehtsoft.EF.Test.DynamicProperties.TableManagement` — create/drop table, index set (Task 5).
- `Gehtsoft.EF.Test.DynamicProperties.DataManagement` — save/load (later phase).
- `Gehtsoft.EF.Test.DynamicProperties.DataSelecting` — WHERE/ORDER BY/aggregate/group/having (later phase).

## Conventions honoured
- Explicit `<Compile Include>` for every new **Entities** and **Db.SqlDb** source file
  (both csproj use `EnableDefaultCompileItems=false`); nothing else in build/packaging touched.
- XML doc comments on all new public members.
- `ArgumentNullException.ThrowIfNull(x, nameof(x))` where applicable.
- No `replace_all` for constant extraction.
- Tests assert intended behaviour; any product bug found → `KNOWN_BUGS.md`, tests unchanged.

## Gates
Coding starts only after this plan is approved (human gate). Task 1 is fully specified and
implemented first; Tasks 2–5 get their concrete interface detail added here just before each
is coded, finish-before-advance. Advancing to the *next phase* (bag/codec/save-load) is a
separate human gate.
