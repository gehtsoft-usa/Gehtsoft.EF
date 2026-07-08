# `UpdateTables` does not reconcile indexes — defect & fix plan

**Status:** *Open defect. Must be fixed before the JSON-properties feature starts* (that feature's
index-change detection is a consumer of the mechanism designed here). Written 2026-07-08.
Companion to `ENTITY_WHERE_PROBLEM.md` (same "core-schema limitation + proposed fix" shape).

---

## 1. The defect

`CreateEntityController.UpdateTables` **never enumerates, adds, or drops indexes** on an
already-existing table. It is not an oversight that slipped in — the controller's own XML doc
declares it (`Gehtsoft.EF.Db.SqlDb/EntityQueries/CreateEntity/CreateEntityController.cs:30-33`):

```
/// Note: The controller does not recognize the situations when:
/// * The type, name or other parameters of the column has changed.
/// * The property deleted from the entity.
/// * A new index is added via ICompositeIndexMetadata
/// * View changed.
```

### What `UpdateTables` actually does (`CreateEntityController.cs:384-426`)
For each existing, non-recreate table it does exactly three things:
1. **Adds missing columns** — `!schema.Contains(info.Table, column.Name)` → `ActionController.AddColumns`.
2. **Drops `[ObsoleteEntityProperty]` columns** (earlier loop, `:326-381`).
3. **Reconciles the EAV dynamic-properties side *table*** (`ReconcileDynamicPropertiesTable`, `:424`)
   — the table itself, **not** its indexes.

There is **no** step that enumerates existing indexes, diffs the declared index set against the
DB, or issues a standalone `CREATE INDEX` / `DROP INDEX`. `connection.Schema()` doesn't even
return index information to compare against (tables + views + columns only —
`SqliteConnection.SchemaCore` / `PostgresConnection` / `OracleConnection`).

### Failing scenarios (all silent — no error, no change)
- Add a **composite index** (`ICompositeIndexMetadata`) to an existing entity → **not created.**
- Set `Sorted = true` (or `Unique`) on an **existing** column → **not created.**
- Remove an index / set `Sorted = false` → the old index is **never dropped** (orphan).
- Change a composite index's columns/order → **neither** dropped nor recreated.

The **only** way an index appears during update today is indirect: a brand-new **column** or a
brand-new **table** carries its own index in its `CREATE`-time DDL (`TableDdlBuilder.HandleAfterQuery`
/ `CreateTableBuilder.HandleCompositeIndex`). Adding an index to columns that already exist does
nothing.

### Impact
Index changes on a live schema require a hand-written `Patch`. For most apps this is a latent
correctness/perf gap; for the JSON feature it is a hard blocker (JSON indexes are declared per
value-path and *will* be added/removed over an app's life — see `JSON_PROPERTIES/`).

---

## 2. Facts that shape the fix (verified 2026-07-08)

### Index kinds and their naming
All framework-created indexes are named **`<table>_<name>`**, uniformly:
- **Single-column** `Sorted` / FK indexes: `<table>_<columnName>`
  (`TableDdlBuilder.cs:68-77`; `NeedIndex` at `:57-59` = `Sorted || self-FK || (FK && !IndexForFKCreatedAutomatically)`).
- **Composite / expression** indexes: `<table>_<index.Name>`
  (`CreateIndexBuilder.cs:53-60`; drop mirror `DropIndexBuilder.cs:37-42`). A field may carry a
  `SqlFunctionId? Function` → an expression index (`CompositeIndex.Field`), guarded by
  `SupportFunctionsInIndexes`.
- **`Unique` / `PRIMARY KEY`** are emitted **inline as column constraints**
  (`TableDdlBuilder.cs:24,29-30`), *not* as named `CREATE INDEX`. Their DB-created backing indexes
  get **DB-chosen names** (e.g. Postgres `<table>_pkey`) that do **not** match the `<table>_<name>`
  convention. → a reconciler keyed to `<table>_<name>` naturally **won't touch** PK/unique/user
  indexes. This is the load-bearing fact for safe drop.

### Introspection available today
- `connection.Schema()` — no index data.
- `DoesObjectExist(table, name, "index")` — probes **one** named index per driver
  (`SQLITE_MASTER type='index' name='<t>_<n>'` `SqliteConnection.cs:344`;
  `pg_indexes tablename+indexname` `PostgresConnection.cs:172`;
  `ALL_INDEXES OWNER+TABLE_NAME+INDEX_NAME` `OracleConnection.cs:213`). Good for the "add if
  missing" half; **cannot enumerate**, so it cannot find indexes whose declaration was deleted.
- `GetCreateIndexBuilder(table, CompositeIndex)` (`SqlDbConnection.cs:211`) /
  `GetDropIndexBuilder(table, name)` (`:370`) exist, are driver-overridable, and today are used
  **only by tests** — never by the controller. These are the emit primitives the fix wires in.

### What must be built new
1. A per-driver **enumerate-indexes-on-a-table** helper (currently missing):
   - SQLite: `SELECT name FROM SQLITE_MASTER WHERE type='index' AND tbl_name=@t`
   - Postgres: `SELECT indexname FROM pg_indexes WHERE tablename=@t`
   - Oracle: `SELECT INDEX_NAME FROM ALL_INDEXES WHERE OWNER=(SELECT USER FROM DUAL) AND TABLE_NAME=@T`
   For a *structural* (columns/expression) diff, additionally index-column introspection
   (`PRAGMA index_info` / `pg_index`+`pg_attribute` / `ALL_IND_COLUMNS`) — needed only at Level 2+.
2. A **reconcile pass** in `UpdateTables` that diffs desired vs actual and issues create/drop.

---

## 3. The fix — design

Compute the **desired** index set from the descriptor (every `<table>_<name>` the framework would
create: `Sorted`/FK single-column + all `ICompositeIndexMetadata` entries). Enumerate the
**actual** indexes. Then:

- **CREATE** = desired names absent from actual. *Unambiguous and always safe* — this alone fixes
  the reported "I added an index and it wasn't created" scenario.
- **DROP** = the hard half, because a removed declaration leaves an actual index whose name is no
  longer in the desired set, and we must not delete PK/unique backing indexes or a user's
  hand-made index. The `<table>_<name>` naming discipline is the ownership signal.

### The central decision — how aggressive DROP should be
- **Level 1 — Add-only.** Create missing indexes; never drop. Simple, zero risk. Removed indexes
  stay as orphans (documented; cleared by a manual `Patch`). *Fully fixes the user's reported case.*
- **Level 2 — Add + owned-drop (recommended).** Also drop an actual index iff its name matches the
  framework convention `<table>_<name>` **and** it is not in the desired set **and** it is not a
  PK/unique backing index (those don't match the convention, so this is automatic). Optionally
  confirm via index-column introspection that a same-named index whose *definition* changed is
  dropped+recreated. Residual risk: a user who hand-created an index following the exact
  `<table>_<x>` convention could see it dropped — **documented limitation** (same class of
  naming-convention trust the whole builder already relies on).
- **Level 3 — Managed-index catalog.** Persist framework-created index names in a small metadata
  table for unambiguous ownership; reconcile against it. Most robust, most invasive (new table +
  bootstrap for pre-existing DBs). Overkill for now.

**Recommendation: Level 2.** It fixes add *and* remove, the ownership rule is safe by construction
for PK/unique, and the only residual risk is a naming-convention collision the codebase already
implicitly trusts everywhere else.

### Where it plugs in
In `UpdateTables`, the existing-table `else` branch (`:396-425`), after `AddColumns` /
`ReconcileDynamicPropertiesTable`. Add a `ReconcileIndexes(connection, info, entityDescriptor,
actualIndexNames)` that uses `GetCreateIndexBuilder` / `GetDropIndexBuilder`. The Recreate/new-table
`if` branch already re-emits every index via `CREATE TABLE`, so nothing to add there. Respect
`SupportFunctionsInIndexes` (skip/where-`FailIfUnsupported` throw for expression indexes on unsup
drivers) exactly as `CreateIndexBuilder`/`CreateTableBuilder` already do.

---

## 4. Staging

- **Stage 0** — per-driver **enumerate-indexes** helper (`SqlDbConnection.GetTableIndexes(table)`
  + `*Core` per driver) with deep tests (create known indexes, enumerate, assert names).
- **Stage 1 — Level 1 (add-only)** reconcile in `UpdateTables`. Fixes the reported defect. Deep
  (SQLite) + acceptance (SQLite/Postgres/Oracle/MSSQL/MySQL — this is a *general* fix, all drivers):
  add composite index / set `Sorted` on existing column → `UpdateTables` → index now exists;
  idempotent second run is a no-op.
- **Stage 2 — Level 2 (owned-drop)**, gated on the decision. Remove an index / un-sort a column →
  `UpdateTables` → the framework-owned index is gone; PK/unique/user indexes untouched
  (explicit test: a manually-created index with a non-convention name survives). Optional
  structural change-detection via index-column introspection.
- **Regression** — full existing suite green (schema-update tests, `Legacy/DbUpdateTests`,
  create/drop tests); byte-identical `CREATE TABLE` output (indexes still ride along at create time).

Note: this is a **general** fix across **all 6 drivers**, not scoped to the 3 JSON drivers — index
reconciliation is missing for everyone. JSON's expression-index paths are an *extension* of the
index-field model layered on top (Section 5).

---

## 5. Relationship to the JSON-properties feature

`JSON_PROPERTIES/` originally carried "Phase 2 — table update / index reconciliation" as its own
largest subsystem. That work is **hoisted here** as a prerequisite general fix, because the gap is
not JSON-specific. Once this lands, the JSON feature *consumes* it: it only adds a **path-carrying
field** to the index model (`CompositeIndex.Field` currently = `(SqlFunctionId? Function, columnName,
dir)`; JSON needs `+ jsonPath + targetType`) and per-driver `GetSqlFunction(JsonValue, …)`
rendering. The desired/actual diff, the enumerate helper, and the `UpdateTables` wiring are all
provided by this fix. JSON indexes use a reserved name shape (`<table>_<col>_<pathslug>`) that fits
the same `<table>_<name>` convention and ownership rule.

---

## 6. Decision needed before coding
**Target level for DROP reconciliation (Section 3): Level 1, Level 2 (recommended), or Level 3.**
Everything else (enumerate helper, add-reconcile, wiring, tests) is agreed and unambiguous.
