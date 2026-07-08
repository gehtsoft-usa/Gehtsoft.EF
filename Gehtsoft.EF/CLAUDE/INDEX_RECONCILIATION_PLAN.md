# `UpdateTables` index reconciliation — implementation plan

*Planned 2026-07-08. Defect + decisions: `INDEX_RECONCILIATION_PROBLEM.md`. Coding starts only
after this plan is approved (human gate). Stages are finish-before-advance.*

## Recap of settled decisions
- **Scope:** plain `CREATE INDEX` objects only — single-column `Sorted`/FK indexes + compound
  (`ICompositeIndexMetadata`) indexes. **`Unique`/`PRIMARY KEY` out** (inline constraints).
- **Drop level 2 (owned-drop):** an actual index is framework-owned iff its name starts with
  `<table>_` **and** it is a non-unique, non-primary index. Owned indexes not in the desired set
  are dropped; changed ones are dropped+recreated.
- **General fix, all 6 drivers.** Tests in `Gehtsoft.EF.Test/Legacy/DbUpdateTests.cs`.
- Residual-risk mitigation = a docgen article (Stage 2).
- **Eliminate `FailIfUnsupported`; add `ExcludeFor` (NG 2026-07-08):** remove
  `CompositeIndex.FailIfUnsupported` from the API entirely. A composite index a driver cannot build
  (a `Function` field where `SupportFunctionsInIndexes=false`, i.e. MSSQL/MySQL) **always throws**
  `EfSqlException(FeatureNotSupported)` — unless the author explicitly lists that driver in the new
  **`ExcludeFor`** (a driver-id list using `UniversalSqlDbFactory` constants), which skips the index
  cleanly on those drivers and **self-documents why**. Applies identically at create-table and in
  the reconciler. Narrow blast radius: only function-field indexes on those two drivers are
  affected; plain indexes (incl. all EAV indexes) never hit this path.

---

## Stage 0 — per-driver "enumerate indexes on a table" helper

### 0.1 New public result type
`Gehtsoft.EF.Db.SqlDb/TableIndexInfo.cs` (new; `<Compile Include>` into `Gehtsoft.EF.Db.SqlDb.csproj`):
```csharp
namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>Describes one physical index found on a table (see SqlDbConnection.GetTableIndexes).</summary>
    public sealed class TableIndexInfo
    {
        public string Name { get; }                       // full DB name, e.g. "mytable_mycol"
        public IReadOnlyList<string> Columns { get; }     // ordered, lower-cased column names; empty when expression/unknown
        public bool IsExpression { get; }                 // true if any key part is an expression (Columns unreliable)
        public bool IsUnique { get; }                     // unique index (incl. the ones backing UNIQUE constraints)
        public bool IsPrimary { get; }                    // index backing the PRIMARY KEY
        public TableIndexInfo(string name, IReadOnlyList<string> columns, bool isExpression, bool isUnique, bool isPrimary) { ... }
    }
}
```
`GetTableIndexes` returns **all** indexes with `IsUnique`/`IsPrimary` set — identifying PK/unique
explicitly rather than hiding them in SQL. The **reconciler ignores** any index with
`IsUnique || IsPrimary` (Stage 1.3); unique/PK reconciliation is out of scope but the flags are the
ready extension point if UNIQUE handling is added later. (PK naming like Postgres `<table>_pkey`
matches the `<table>_` shape, so filtering by the *flag* — not the name — is what keeps it safe.)

### 0.0 Eliminate `FailIfUnsupported`; add `ExcludeFor`; expose the driver id on specifics

**(a) Driver id on specifics.** Add `public abstract string DbName { get; }` to
`SqlDbLanguageSpecifics`, returning the `UniversalSqlDbFactory` id (`"sqlite"`/`"npgsql"`/`"oracle"`/
`"mssql"`/`"mysql"`) — the same value the connection exposes as `SqlDbConnection.ConnectionType`
(`:67`). Implement in the 5 driver specifics + internal `Sql92LanguageSpecifics` + test
`DummyDbSpecifics`. (Builders hold only `mSpecifics`, not the connection, so the id must live here.)

**(b) `ExcludeFor` on `CompositeIndex`.** In `Metadata/CompositeIndex.cs`: add
`public string[] ExcludeFor { get; set; }` (driver-id strings; null/empty ⇒ applies to every
driver) with XML doc. **Remove** the `FailIfUnsupported` property (`:74`).

**(c) New guard** in `CreateIndexBuilder.PrepareQuery` (`:37-46`) and
`CreateTableBuilder.HandleCompositeIndex` (`:104-110`), replacing the `FailIfUnsupported` branch
(and swapping the existing LINQ `mIndex.Any(f => f.Function != null)` for an explicit loop — no-LINQ):
```
if (ExcludeFor contains mSpecifics.DbName, OrdinalIgnoreCase) { emit nothing; return/continue; } // explicit, documented skip
if (!mSpecifics.SupportFunctionsInIndexes && index has any Function field)
    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);                                // always loud
```

**(d) Reconciler** builds the desired set only from indexes whose `ExcludeFor` does **not** contain
`connection.ConnectionType`. Consequence (free from the diff): adding a driver to an index's
`ExcludeFor` removes it from *desired* on that driver ⇒ the existing owned-drop step drops it there.

**Breaking API change** (NG's call): `FailIfUnsupported` removed. **Practically non-breaking for
real consumers:** NG confirms the *only* real-project use sets `FailIfUnsupported = true` — i.e. it
wants the throw, which is now the default, so that consumer just deletes the line (identical
behavior). No real code relies on `= false` (silent skip). Within this repo only **test** code uses
the flag. Migrations (grep-verified; `archive/` ignored):
- `Legacy/Entities/SalesEntities.cs:173-178` — "Index2" (`Upper(Note)`, was `FailIfUnsupported=false`)
  → `ExcludeFor = new[] { UniversalSqlDbFactory.MSSQL, UniversalSqlDbFactory.MYSQL }`.
- `SqlDb/SqlQueryBuilder/CreateIndex.cs` — skip-case (`:70-76`, `DummyDbSpecifics
  .SupportFunctionsInIndexesSpec=false`, no flag → expected empty query): set `ExcludeFor` to the
  dummy connection's `ConnectionType` to keep the empty-query expectation. Fail-case (`:78-88`):
  drop `ci.FailIfUnsupported=true`; it now throws by default (assert unchanged).
- `SqlDb/SqlQueryBuilder/CreateTable.cs:515/537/557` — same treatment (flag → `ExcludeFor`/default-throw).
- `Entity/CompositeIndexTest.cs` — metadata-only, no DDL: unaffected except removing any
  `FailIfUnsupported` reference; **add** coverage that `ExcludeFor` round-trips on `CompositeIndex`.

### 0.2 `SqlDbConnection` API (mirrors `Schema()` / `DoesObjectExistCore`)
In `SqlDbConnection.cs`, next to `Schema`/`DoesObjectExist`:
```csharp
public TableIndexInfo[] GetTableIndexes(string tableName)
{ CheckForScalars(tableName); return GetTableIndexesCore(tableName, true, null).ConfigureAwait(false).GetAwaiter().GetResult(); }

public Task<TableIndexInfo[]> GetTableIndexesAsync(string tableName, CancellationToken? token = null)
{ CheckForScalars(tableName); return GetTableIndexesCore(tableName, false, token); }

protected abstract Task<TableIndexInfo[]> GetTableIndexesCore(string tableName, bool sync, CancellationToken? token);
```
`abstract` ⇒ all 6 drivers implement it (same obligation as `SchemaCore`). Each returns **every**
index on the table, groups rows by index name ordered by key position, and sets `IsUnique`/
`IsPrimary`; a part with no column name (expression) ⇒ `IsExpression=true`.

### 0.3 Per-driver `GetTableIndexesCore` queries
Each returns all indexes + `IsUnique`/`IsPrimary` (no unique/primary filtering in SQL):
- **SQLite** (`SqliteConnection.cs`): `PRAGMA index_list('<t>')` → each row: `name`, `unique`(→
  `IsUnique`), `origin` (`'pk'`→`IsPrimary`, `'u'`→unique-constraint, `'c'`→plain CREATE INDEX);
  for each, `PRAGMA index_info('<name>')` → ordered `name` (NULL ⇒ expression).
- **Postgres** (`PostgresConnection.cs`):
  ```sql
  SELECT i.relname, ix.indisunique, ix.indisprimary, a.attname, k.ordinality
  FROM pg_class t
  JOIN pg_index ix ON t.oid = ix.indrelid
  JOIN pg_class i ON i.oid = ix.indexrelid
  LEFT JOIN LATERAL unnest(ix.indkey) WITH ORDINALITY k(attnum,ordinality) ON true
  LEFT JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
  WHERE t.relname = @t AND t.relnamespace = current_schema()::regnamespace
  ORDER BY i.relname, k.ordinality;
  ```
  (`indisunique`→`IsUnique`, `indisprimary`→`IsPrimary`, `attnum = 0` ⇒ expression key.)
- **Oracle** (`OracleConnection.cs`): (identifiers upper-cased like the existing introspection)
  ```sql
  SELECT i.INDEX_NAME, i.UNIQUENESS, c.COLUMN_NAME, c.COLUMN_POSITION,
         (SELECT COUNT(*) FROM ALL_CONSTRAINTS k
          WHERE k.OWNER=i.OWNER AND k.INDEX_NAME=i.INDEX_NAME AND k.CONSTRAINT_TYPE='P') AS IS_PK
  FROM ALL_INDEXES i
  LEFT JOIN ALL_IND_COLUMNS c ON c.INDEX_OWNER = i.OWNER AND c.INDEX_NAME = i.INDEX_NAME
  WHERE i.OWNER = (SELECT USER FROM DUAL) AND i.TABLE_NAME = '<T>'
  ORDER BY i.INDEX_NAME, c.COLUMN_POSITION;
  ```
  (`UNIQUENESS='UNIQUE'`→`IsUnique`, `IS_PK>0`→`IsPrimary`, `SYS_NCnnnnn$` columns ⇒ `IsExpression`.)
- **MSSQL** (`MssqlConnection.cs`): `sys.indexes`+`sys.index_columns`+`sys.columns`, filter only
  `i.type>0` (exclude heaps); read `is_primary_key`→`IsPrimary`, `is_unique`→`IsUnique`.
- **MySQL** (`MysqlConnection.cs`): `information_schema.STATISTICS WHERE table_schema=DATABASE()
  AND table_name=@t`, group by `INDEX_NAME`, order by `SEQ_IN_INDEX`; `NON_UNIQUE=0`→`IsUnique`,
  `INDEX_NAME='PRIMARY'`→`IsPrimary`.

### 0.4 Stage-0 tests (`DbUpdateTests.cs`)
- Deep `[Fact]` (SQLite in-memory): create a table with a `Sorted` column + a 2-column composite
  index (+ a UNIQUE column and a PK); `GetTableIndexes` returns the two plain indexes with the
  right names + ordered columns and `IsUnique==IsPrimary==false`, **and** the PK/unique backing
  indexes with `IsPrimary`/`IsUnique` correctly set (they are returned, just flagged).
- Acceptance `[Theory][MemberData(nameof(ConnectionNames), "")]`: same create → enumerate →
  assert the two plain indexes present with flags clear, and at least the PK entry present with
  `IsPrimary` set, on every configured driver.

---

## Stage 1 — Level-2 reconcile in `UpdateTables`

### 1.1 Where
`CreateEntityController.cs`, the existing-table `else` branch, immediately after
`ReconcileDynamicPropertiesTable(...)` (`:424`), add:
```csharp
ReconcileIndexes(connection, info, entityDescriptor, schema);
```
New private method beside `ReconcileDynamicPropertiesTable`. (Recreate/new-table `if` branch
already re-emits all indexes at CREATE TABLE — untouched.)

### 1.2 Desired set (framework-owned indexes for the owner table)
For `descriptor.TableDescriptor`:
- **Single-column** — for each column where the index rule holds
  (`column.Sorted || (column.ForeignKey && column.ForeignTable==column.Table) ||
    (column.ForeignKey && !specifics.IndexForFKCreatedAutomatically)` — the same predicate as
  `TableDdlBuilder.NeedIndex`; factor it into a reusable static `TableDdlBuilder.NeedIndex(column,
  specifics)` to avoid divergence): desired index logical-name = `column.Name`, columns =
  `[column.Name]`, non-expression.
- **Compound** — `descriptor.TableDescriptor.Metadata as ICompositeIndexMetadata`: each
  `CompositeIndex` → logical-name = `index.Name`, columns = its field names, expression = any
  field has `Function != null`. Honor `SupportFunctionsInIndexes`/`FailIfUnsupported` exactly as
  `CreateIndexBuilder` does (skip silently, or throw) so desired matches what create-table emits.

Desired DB name = `descriptor.TableDescriptor.Name + "_" + logicalName`.

### 1.3 Diff algorithm
(pseudocode — the real implementation uses explicit loops, no LINQ)
```
actual   = GetTableIndexes(table) keeping only entries where !IsUnique && !IsPrimary  // ignore unique/PK
prefix   = table + "_"
desiredByName = { desiredDbName -> desiredIndex }            // OrdinalIgnoreCase
actualByName  = { info.Name -> info }                        // OrdinalIgnoreCase

// CREATE missing
foreach d in desiredByName:
    if d.name not in actualByName: create(d)

// owned-DROP + CHANGE
foreach a in actualByName:
    if not a.name.StartsWith(prefix, OrdinalIgnoreCase):  continue   // not framework-owned
    if a.name not in desiredByName:                        drop(a)   // removed declaration
    else:
        d = desiredByName[a.name]
        if not d.IsExpression and not a.IsExpression
           and not SameColumns(d.columns, a.columns):      { drop(a); create(d) }   // changed definition
        // expression indexes: name-match only (see limitation)
```
`SameColumns` = ordered, case-insensitive column-name compare (direction ignored — introspection
is unreliable on it; documented).

- **create(d):** build a `CompositeIndex(logicalName)`, `.Add(col[, function])` per field (a
  single-column desired index = one plain `Add(column.Name)`), then
  `connection.GetCreateIndexBuilder(descriptor.TableDescriptor, ci)` → execute (skip if its
  `PrepareQuery` yields `""` on an unsupported-function driver). `RaiseUpdate(info.Table)`.
- **drop(a):** logical = `a.Name.Substring(prefix.Length)`; `connection.GetDropIndexBuilder(
  descriptor.TableDescriptor, logical)` → execute. `RaiseUpdate(info.Table)`.

### 1.4 Documented limitations (carry into the Stage-2 article + XML docs)
1. **Ownership is by name convention** — a hand-made index named `<table>_<x>` may be dropped
   (the article warns against this).
2. **Expression/function composite indexes: name-match only** — a change to the *function/columns*
   of a composite index that keeps its **name** is not auto-detected (introspection can't compare
   expressions portably). Workaround: rename the index or use `UpdateMode.Recreate`. *(The JSON
   feature side-steps this by encoding the JSON path into the index name, so a path change becomes
   a name change caught by CREATE/DROP.)*
3. **Function composite index on MSSQL/MySQL** (`SupportFunctionsInIndexes=false`): **throws**
   `EfSqlException(FeatureNotSupported)` — at create-table and in the reconciler alike (§0.0). To
   run such an index on the other drivers while skipping these two, declare
   `ExcludeFor = { UniversalSqlDbFactory.MSSQL, UniversalSqlDbFactory.MYSQL }` — a clean, documented
   skip; the excluded drivers never create it and (absent from *desired*) never trigger a drop.
4. Scope excludes the EAV dynamic-properties side table's own indexes (owner table only in v1).

### 1.5 Stage-1 tests (`DbUpdateTests.cs`)
Use the file's existing pattern: two entity classes sharing a `Table` in different `Scope`s (as
`lv1`/`lv2` already do), or a Recreate baseline then an Update, to simulate gained/lost/changed
indexes. Composite indexes declared via `[Entity(Metadata = typeof(...))]` + `ICompositeIndexMetadata`.

**Deep `[Fact]` (SQLite in-memory, `SqliteDbConnectionFactory.CreateMemory()`), assert via
`DoesObjectExist(table, name, "index")` + `GetTableIndexes`:**
- **Add single-column:** baseline table without `Sorted`; update to the `Sorted` variant →
  `<table>_<col>` now exists; second update = no-op.
- **Add composite:** baseline without metadata; update with `ICompositeIndexMetadata` →
  `<table>_<idx>` exists; idempotent.
- **Drop single-column:** baseline `Sorted`; update to non-sorted variant → `<table>_<col>` gone.
- **Drop composite:** baseline with metadata; update without → `<table>_<idx>` gone.
- **Change composite:** baseline index on (a,b); update to (a,c) same name → old dropped + new
  present; `GetTableIndexes` shows columns (a,c).
- **Safety:** manually `CREATE INDEX notconvention ON t(...)` and rely on the PK/unique of the
  entity; after an unrelated update both survive (not dropped).
- **ExcludeFor:** a composite index with `ExcludeFor = { <current driver> }` is **not created** by
  `UpdateTables` on that driver (and an already-present one is owned-dropped); the same index on a
  non-excluded driver **is** created. A function index **without** `ExcludeFor` on MSSQL/MySQL
  throws `FeatureNotSupported` (assert via the builder-level `CreateIndex.cs` test, driver-agnostic).

**Acceptance `[Theory][MemberData(nameof(ConnectionNames), "")]`** (all drivers, SQLite always on):
add-index and drop-index round-trips verified by `GetTableIndexes`/`DoesObjectExist`; idempotent
re-run. Skip the function-composite assertions on MSSQL/MySQL (`SupportFunctionsInIndexes=false`).

**Regression:** run `Legacy/DbUpdateTests` (existing `AlterTable`, `EntityUpdate`,
`EntityUpdateDetectsContradictoryModes`), plus create/drop suites; confirm byte-identical
`CREATE TABLE` output (create-time indexes unchanged).

---

## Stage 2 — docs article
Docgen `.ds` in `doc1/` covering `UpdateTables` index reconciliation, with the prominent **warning**
against naming manual indexes `<table>_<indexname>`. XML doc comments on `GetTableIndexes` /
`TableIndexInfo` / any new public surface. Iterate with `/t:MakeDoc` (never `CleanDoc`).

## Conventions
- Explicit `<Compile Include>` for every new `Gehtsoft.EF.Db.SqlDb` source file
  (`EnableDefaultCompileItems=false`); nothing else in build touched. Test csproj auto-includes.
- Tests assert intended behaviour; product bugs → `INDEX_RECONCILIATION` notes, tests unchanged.
  Prefer behavioural checks (`DoesObjectExist`/`GetTableIndexes`) over SQL-text matching.
- `ArgumentNullException.ThrowIfNull(x, nameof(x))`; no `replace_all` for constants; no LINQ.

## Risks
- **R1:** per-driver catalog query correctness (esp. Oracle case + function-based hidden columns;
  MySQL FK auto-index names). Mitigated by Stage-0 acceptance tests on each configured driver.
- **R2:** transaction/DDL wrapping — index create/drop run as separate `ExecuteNoData` like the
  existing `Reconcile*`/`AddColumns` calls; Oracle DDL auto-commits (matches current behavior).
- **R3:** ordering vs FK/column changes — `ReconcileIndexes` runs after `AddColumns`, so indexes on
  newly-added columns resolve; a Sorted new column already gets its index from `AddColumns`' DDL,
  so its desired index will already be present (create step no-ops). Verify no double-create.
