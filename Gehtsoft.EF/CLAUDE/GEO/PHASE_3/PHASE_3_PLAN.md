# GEO Phase 3 — TableUpdate (add geo column + reconcile spatial indexes)

*Plan drafted 2026-07-14 for the Gate. Depends on the general index-reconciliation fix (already landed,
decision 9). Read `../GEO_PLAN.md` (Phase 3 bullet + decisions), `../STATE.md`, and Phase 2's plan for
the create-time seams this phase splits open. **Nothing is coded until this plan is approved.***

## Goal (from GEO_PLAN Phase 3)

During `CreateEntityController.UpdateTables(..., Update)` on an **existing** table:

1. **Add a geo column** that the entity gained but the table lacks — including the drivers that cannot
   use a plain `ALTER TABLE … ADD COLUMN` for geometry (SpatiaLite registers via `AddGeometryColumn`;
   Oracle must also insert into `USER_SDO_GEOM_METADATA`).
2. **Reconcile spatial indexes** on a geo column: create a declared `[SpatialIndex]` that is missing,
   drop a framework-owned spatial index whose declaration was removed — the spatial analogue of the
   existing plain / JSON index reconciliation in `ReconcileIndexes`.

Unchanged / already covered: create-time column + index (Phase 2), non-geo indexes (existing
reconciler), obsolete-column drop.

## What the code already gives us (verified seams)

- `UpdateTables` → `AddColumns` → `AlterTableQueryBuilder.SetTable(td, addColumns, null)` →
  `GetQueries()`. `HandleCreateQuery` calls `DdlBuilder.HandleColumnDDL(sb, column, true)` then
  `HandlePostfixDDL`; `HandleAfterCreateQuery` adds a plain single-column index when `NeedIndex`.
- Phase 2 already put a geometry branch in `TableDdlBuilder.HandleColumnDDL` (inline
  `col GeometryColumnDDL`), a `SkipInlineColumn` (SpatiaLite → true), and `HandleGeometryAfterQuery`
  that on each driver emits **column registration + spatial index together** (SpatiaLite:
  `AddGeometryColumn` **then** `CreateSpatialIndex`; Postgres: `CREATE INDEX … USING GIST`; Oracle:
  metadata insert + domain index; etc.).
- Index reconciliation lives in `CreateEntityController.ReconcileIndexes` / `ComputeDesiredIndexes`,
  driven by `connection.GetTableIndexes(table)` (actual) vs a desired set built from
  Sorted/FK + `ICompositeIndexMetadata` + `column.Json.Indexes` (via `CompositeIndex.ForJson`).
  Create/drop go through `GetCreateIndexBuilder`/`GetDropIndexBuilder`. `CompositeIndex.ForSpatial`
  does **not** exist yet.
- `GeometryColumnMetadata` already carries `Indexes` (list of `SpatialIndexDefinition`: Name, bbox,
  Tolerance) — the desired-set source for spatial reconciliation.

## The central problem — spatial indexes are not uniformly enumerable

The generic reconciler compares desired vs **`GetTableIndexes`**. Spatial indexes do **not** all show
up there:

| Driver | Spatial index lives in | Seen by current `GetTableIndexes`? |
|---|---|---|
| PostGIS | normal `pg_index` (GIST) | **Yes** |
| MySQL | `information_schema.statistics` (`INDEX_TYPE=SPATIAL`) | **Yes** |
| Oracle | `USER_INDEXES` (`ITYP_NAME='SPATIAL_INDEX'`), domain index | likely yes (needs check) |
| MSSQL | `sys.spatial_indexes`, **not** the normal `sys.indexes` query | **No** (separate catalog) |
| SpatiaLite | R-tree **virtual table** `idx_<t>_<c>` + triggers; flagged in `geometry_columns.spatial_index_enabled` — `PRAGMA index_list` never lists it | **No** |

So we cannot route every driver's spatial-index detection through the plain-index path unchanged. Two
options — I recommend **Option A**:

- **Option A (recommended): surface spatial indexes through `GetTableIndexes`.** Add
  `bool IsSpatial` to `TableIndexInfo`; each driver's `GetTableIndexesCore` also emits its spatial
  indexes (MSSQL adds a `sys.spatial_indexes` query; SpatiaLite reads `geometry_columns`; Oracle keys
  off `ITYP_NAME`; Postgres/MySQL already return them, just tag them). The reconciler then treats a
  desired-vs-actual spatial index exactly like a plain one, and `CreateIndex`/`DropIndex` route to the
  spatial builders when the desired/actual entry is spatial. One structural field, uniform logic.
- **Option B: a separate `GetSpatialIndexes(table)` probe** called only from the geo branch. Less
  invasive to `TableIndexInfo`, but duplicates the add-missing/drop-removed loop. Rejected unless
  Option A proves noisy.

## Design

### A. Add a geo column to a live table

Split Phase 2's combined `HandleGeometryAfterQuery` into two composable pieces on `TableDdlBuilder`
(so both create-table and alter-table reuse them, and the reconciler owns the index half):

- `HandleGeometryColumnRegistration(sb, column)` — the **column** only: no-op for inline drivers
  (the type is already inline via `HandleColumnDDL`), `AddGeometryColumn` for SpatiaLite, the
  `USER_SDO_GEOM_METADATA` insert for Oracle.
- `HandleGeometrySpatialIndex(sb, column, def)` — one spatial index (GIST / `CreateSpatialIndex` /
  MySQL `SPATIAL INDEX` / MSSQL spatial index + bbox / Oracle domain index).

`CreateTableBuilder` (Phase 2 path) calls registration then a loop over `def`s → identical SQL to
today (guard with an AST test that Phase 2 output is unchanged). `AlterTableQueryBuilder` gets a
geometry branch: when `column.Geometry != null`, emit the inline `ALTER … ADD COLUMN col
GeometryColumnDDL` **only for inline drivers**, skip the ADD for SpatiaLite, then emit
`HandleGeometryColumnRegistration`; do **not** emit the spatial index here (the reconciler does, so a
freshly-added geo column with a declared index gets it in the same `UpdateTables` pass). MySQL keeps
Phase 2's Z/M throw.

### B. Reconcile spatial indexes

- New `CompositeIndex.ForSpatial(indexName, columnName, GeometryColumnMetadata, SpatialIndexDefinition)`
  — an unassociated index carrying enough to render the spatial CREATE (subtype/SRID/Z-M from the
  column metadata + bbox/tolerance from the def). Mark it spatial (`IsSpatial`/a `Field` flavour) so
  `MakeDesired`/`CreateIndex` can branch.
- `ComputeDesiredIndexes` gains a geo loop mirroring the JSON loop: for each `column.Geometry`, for
  each `def in column.Geometry.Indexes`, add `ForSpatial(...)`. Naming via the existing
  `SqlDbLanguageSpecifics.IndexName(table, logical)` so desired ↔ actual names line up (Phase 2 used
  the same derivation — confirm they match exactly).
- `CreateIndex` routes a spatial `CompositeIndex` to the driver spatial builder
  (`HandleGeometrySpatialIndex`, reused from A) instead of plain `GetCreateIndexBuilder`; `DropIndex`
  routes spatial drops to a driver spatial-drop (SpatiaLite `DisableSpatialIndex` + drop the R-tree
  virtual table; Oracle `DROP INDEX` + metadata cleanup; others plain `DROP INDEX`).
- Actual set: with Option A, `GetTableIndexes` returns spatial entries tagged `IsSpatial`; the
  add-missing / drop-removed loops work unchanged (a framework-named spatial index no longer desired
  is dropped; column-set change-detection is skipped for spatial — they are single-column by
  construction).

### Files (anticipated)

- `Db.SqlDb`: `TableDdlBuilder` (split hooks), `AlterTableQueryBuilder` (geo branch),
  `CreateTableBuilder` (call split hooks — no output change), `CompositeIndex.ForSpatial` + spatial
  flag, `TableIndexInfo.IsSpatial`, `CreateEntityController.ComputeDesiredIndexes`/`CreateIndex`/
  `DropIndex` geo branches, `SqlDbConnection` spatial-drop plumbing if needed.
- Each driver (`Mssql/Mysql/Oracle/Postgres/Sqlite`): move Phase-2 spatial DDL into the split hooks;
  `GetTableIndexesCore` surfaces spatial indexes (`IsSpatial`); spatial-index drop SQL.
- No `Gehtsoft.EF.Entities` change expected (attributes/metadata already exist). **No reference to the
  NTS module** (hard constraint — Phase 3 is pure DDL over `byte[]`/WKB metadata).

## Testing model — a SEPARATE geo update test set (user instruction, 2026-07-14)

**Do not add geo columns to the existing `UpdateTables` test entities.** New, self-contained geo
fixtures + tests under **`Gehtsoft.EF.Test/Geo/TableUpdate/`** (default-globbed csproj), namespace
`Gehtsoft.EF.Test.Geo.TableUpdate`, using dedicated geo entities that exist only for this phase.

- **Deep / AST (all 5 drivers, DB-free):** via the public `*LanguageSpecifics` / `AlterTableQueryBuilder`
  / `CreateEntityController` desired-index computation — assert (a) add-geo-column SQL per driver
  (inline ADD vs SpatiaLite `AddGeometryColumn` vs Oracle metadata insert), (b) desired spatial-index
  set, (c) create/drop spatial-index SQL, all parsed with `.ParseSql()` where it parses (SpatiaLite
  `SELECT AddGeometryColumn(...)` asserted structurally), (d) **Phase-2 create output is byte-for-byte
  unchanged** after the hook split, (e) MySQL Z/M still throws.
- **Behavioural (SQLite + SpatiaLite here; Postgres if configured):** start from a table without the
  geo column / without an index → `UpdateTables(Update)` → assert the column exists
  (`DoesObjectExist`/`geometry_columns`) and the R-tree/GIST index exists; declare-then-remove a
  `[SpatialIndex]` → assert it is dropped; unchanged → no-op; a non-geo index on the same table is
  untouched.
- Product bugs found → `KNOWN_ISSUES.md` (never adapt a test to a bug).

## Risks / to confirm during coding

- **R-U1 (central): spatial-index enumeration.** Confirm each driver's catalog query and that Option A
  tags spatial indexes without polluting the plain-index path (esp. MSSQL `sys.spatial_indexes`,
  SpatiaLite `geometry_columns`, Oracle `ITYP_NAME`). If Option A proves messy on one driver, fall back
  to Option B for that driver only.
- **R-U2: SpatiaLite spatial-index drop.** `DisableSpatialIndex` + dropping `idx_<t>_<c>` + its triggers
  — verify behaviourally that a subsequent `UpdateTables` re-adds it cleanly.
- **R-U3: name parity.** The Phase-2 create-time spatial index name must equal the Phase-3 desired name
  (`IndexName(table, logical)`), or reconciliation will drop+recreate on every run. Add an explicit
  test.
- **R-U4: Oracle metadata on add-column.** `USER_SDO_GEOM_METADATA` insert must run on add-column, not
  just create-table; dropping the geo column later should clean it (out of scope? confirm — column drop
  for geo is not in the Phase 3 goal, only add + index reconcile).
- **R-U5: no plain single-column index on a geo column.** `AlterTableQueryBuilder.HandleAfterCreateQuery`
  / `NeedIndex` must never emit a plain `CREATE INDEX` on a geometry column.

## Open decisions for the Gate

1. **Option A vs B** for spatial-index enumeration (recommend **A** — add `TableIndexInfo.IsSpatial`).
2. **Geo column *drop*** on update: GEO_PLAN Phase 3 says "add a geo column"; dropping a geo column
   (obsolete property) is not listed. Recommend **out of scope for Phase 3** (add + index reconcile
   only); revisit if wanted.
3. Test fixture location/namespace: `Gehtsoft.EF.Test/Geo/TableUpdate/` (confirm).
