# `UpdateTables` does not reconcile indexes — defect & fix plan

**Status:** ✅ **FIXED — all three stages done, 2026-07-08** (full suite 3396 green on
sqlite/pgsql/mysql/mssql/oracle18/oracle11; docs shipped). This was a **shared prerequisite for both
the JSON-properties and the Geo features** — both are now unblocked (their live-table index
add/drop rides this general mechanism). Written 2026-07-08. Companion to `ENTITY_WHERE_PROBLEM.md`
(same "core-schema limitation + fix" shape). Stage details below and in `INDEX_RECONCILIATION_PLAN.md`.

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
- Set `Sorted = true` on an **existing** column → **not created.**
- Remove an index / set `Sorted = false` → the old index is **never dropped** (orphan).
- Change a composite index's columns/order → **neither** dropped nor recreated.
- *(Also unhandled but **out of scope** for this fix — see Scope: adding/removing `Unique` or
  changing `PRIMARY KEY` on an existing column.)*

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

### Scope (set by NG, 2026-07-08)
Reconcile **only plain `CREATE INDEX` objects**: single-column `Sorted` indexes and compound
(`ICompositeIndexMetadata`) indexes — including FK auto-indexes, which are the same single-column
`CREATE INDEX` shape. **`Unique` and `PRIMARY KEY` are OUT of scope** for now: they are emitted as
*inline column constraints* (`TableDdlBuilder.cs:24,29-30`), their DB backing indexes get
DB-chosen names outside the `<table>_<name>` convention, and changing a uniqueness/PK constraint
is a different (constraint-level, data-validating) operation. This exclusion is *free*: the
reconciler keys on `<table>_<name>` `CREATE INDEX` names, which never include PK/unique backing
indexes — so they are simply never in either the desired or the actual set the reconciler
considers.

### Algorithm
Compute the **desired** index set from the descriptor (every `<table>_<name>` the framework would
create via `CREATE INDEX`: `Sorted`/FK single-column + all `ICompositeIndexMetadata` entries;
**not** `Unique`/PK constraints). Enumerate the **actual** indexes. Then:

- **CREATE** = desired names absent from actual. *Unambiguous and always safe* — this alone fixes
  the reported "I added an index and it wasn't created" scenario.
- **DROP** = the hard half, because a removed declaration leaves an actual index whose name is no
  longer in the desired set, and we must not delete PK/unique backing indexes or a user's
  hand-made index. The `<table>_<name>` naming discipline is the ownership signal.

### How aggressive DROP should be — DECIDED: Level 2 (NG, 2026-07-08)
- **Level 1 — Add-only.** Create missing indexes; never drop. Simple, zero risk. Removed indexes
  stay as orphans (cleared by a manual `Patch`). Fixes only the "add" half.
- **Level 2 — Add + owned-drop  ← CHOSEN.** Also drop an actual index iff its name matches the
  framework convention `<table>_<name>` **and** it is not in the desired set **and** it is not a
  PK/unique backing index (those don't match the convention, so this is automatic). A same-named
  index whose *definition* changed is dropped+recreated (Level-2 structural check, see staging).
  Residual risk: a user who hand-created an index following the exact `<table>_<x>` convention
  could see it dropped.
- **Level 3 — Managed-index catalog.** Persist framework-created index names in a metadata table
  for unambiguous ownership. Most robust, most invasive (new table + bootstrap for pre-existing
  DBs). Not chosen.

**Chosen: Level 2.** It fixes add *and* remove, and the ownership rule is safe by construction for
PK/unique.

**Mitigation for the residual risk (NG's call): documentation, not code.** A docgen article will
**warn users not to name their own (manually created) indexes using the framework's
`<table>_<indexname>` convention**, because `UpdateTables` may drop an index of that name shape
that it does not find in the entity definition. This is the same class of naming-convention trust
the builder already relies on everywhere. → the article is a required deliverable of this fix
(Stage 3 below). Docs live in `doc1/` (docgen `.ds`; iterate with `/t:MakeDoc`).

### Where it plugs in
In `UpdateTables`, the existing-table `else` branch (`:396-425`), after `AddColumns` /
`ReconcileDynamicPropertiesTable`. Add a `ReconcileIndexes(connection, info, entityDescriptor,
actualIndexNames)` that uses `GetCreateIndexBuilder` / `GetDropIndexBuilder`. The Recreate/new-table
`if` branch already re-emits every index via `CREATE TABLE`, so nothing to add there. Respect
`SupportFunctionsInIndexes` (skip/where-`FailIfUnsupported` throw for expression indexes on unsup
drivers) exactly as `CreateIndexBuilder`/`CreateTableBuilder` already do.

---

## 4. Staging

- **Stage 0 — ✅ DONE (2026-07-08).** Per-driver **enumerate-indexes** helper
  (`SqlDbConnection.GetTableIndexes(table)` + abstract `*Core`, implemented in all 6 drivers) with
  deep + acceptance tests; also eliminated `FailIfUnsupported` and added `CompositeIndex.ExcludeFor`
  + `SqlDbLanguageSpecifics.DbName`. Full suite **3388 green** on sqlite/pgsql/mysql/mssql. Details
  in `INDEX_RECONCILIATION_PLAN.md` §0.0–§0.4.
- **Stage 1 — ✅ DONE (2026-07-08, full suite 3396 green on sqlite/pgsql/mysql/mssql).** Full
  Level-2 reconcile in `UpdateTables` (add + owned-drop + structural change). Details in
  `INDEX_RECONCILIATION_PLAN.md` §1. — original scope below:
- **Stage 1 — full Level-2 reconcile in `UpdateTables`** (add + owned-drop in one stage). Deep
  (SQLite) + acceptance (all 6 drivers — this is a *general* fix):
  - **Add:** add a composite index / set `Sorted` on an existing column → `UpdateTables` → the
    index now exists; idempotent second run is a no-op.
  - **Owned-drop:** remove a composite index / un-sort a column → `UpdateTables` → the
    framework-owned index is gone.
  - **Change:** alter a composite index's columns/order → dropped + recreated (structural check
    via index-column introspection).
  - **Safety:** a PK/unique backing index **and** a manually-created index with a non-convention
    name both survive across all the above (explicit test).
- **Stage 2 — ✅ DONE (2026-07-08).** Docgen tutorial article `@key=tutorialen_autoupdate`
  ("Automatic Schema Update", `@ingroup=tutorialsen`) added to `doc1/src/ns/sqltutorialsen.ds`
  immediately after `tutorialen_entities5` — covers what `UpdateTables` does, index reconciliation,
  `ExcludeFor`, and the prominent naming-convention warning. (An earlier standalone `sql`-group
  article was folded into this tutorial and removed, per NG.) XML doc comments added on all new
  public API; `doc1` rescanned (raw now carries `TableIndexInfo`/`ExcludeFor`, drops
  `FailIfUnsupported`); `dotnet build project.proj /t:MakeDoc` green, link integrity passed.
  — original scope below:
- **Stage 2 — docs.** A docgen `.ds` article (in `doc1/`) documenting `UpdateTables` index
  reconciliation and, prominently, the **warning** that manually-created indexes must not use the
  framework's `<table>_<indexname>` naming convention or they may be dropped on update. XML doc
  comments on any new public API.
- **Regression** — full existing suite green (schema-update tests, `Legacy/DbUpdateTests`,
  create/drop tests); byte-identical `CREATE TABLE` output (indexes still ride along at create time).

Note: this is a **general** fix across **all 6 drivers**, not scoped to the 3 JSON drivers — index
reconciliation is missing for everyone. JSON's expression-index paths are an *extension* of the
index-field model layered on top (Section 5).

---

## 5. Relationship to the JSON-properties and Geo features (shared prerequisite — now satisfied)

Both `JSON_PROPERTIES/` and `GEO/` originally carried live-table index reconciliation as their own
subsystem. That work was **hoisted here** as a single general fix, because the gap is not specific to
either feature. Now that it has landed, both features simply *consume* it:

- **JSON** (`JSON_PROPERTIES/` Phase 2): adds a **path-carrying field** to the index model
  (`CompositeIndex.Field` = `(SqlFunctionId? Function, columnName, dir)`; JSON adds `jsonPath +
  targetType`) + per-driver `GetSqlFunction(JsonValue, …)`. JSON indexes use a reserved name shape
  (`<table>_<col>_<pathslug>`) that fits the same `<table>_<name>` convention and ownership rule.
- **Geo** (`GEO/` Phase 3): create-time spatial indexing (Phase 2) needs nothing from here; only
  **live-table spatial-index add/drop** extends the shared reconciler.

The desired/actual diff, the per-driver enumerate helper (`GetTableIndexes`), the `ExcludeFor`
mechanism, and the `UpdateTables` wiring are all provided by this fix. **Both features are
unblocked.**

---

## 6. Decisions — all settled (2026-07-08)
- Scope = plain `CREATE INDEX` only (`Sorted`/FK single-column + compound); `Unique`/PK out (§3).
- DROP level = **Level 2 (owned-drop)**, keyed on the `<table>_<name>` convention (§3).
- Residual-risk mitigation = a **docgen article** warning against manual indexes using the
  framework naming convention (§3, Stage 3).

Nothing else is open — enumerate helper, add+owned-drop reconcile, wiring, and tests are agreed.
Ready to draft the detailed Stage-0/Stage-1 implementation plan.
