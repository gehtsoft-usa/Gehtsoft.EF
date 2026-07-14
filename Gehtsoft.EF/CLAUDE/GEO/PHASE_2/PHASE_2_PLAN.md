# Phase 2 — Table create: driver-level geometry column + spatial index + enable-spatial (plan)

*Phase plan for the geo feature (`../GEO_PLAN.md`). **Gate 2** applies. Scope (user, 2026-07-09): the
geometry **column type** AND the **spatial index** DDL across all five drivers, plus the driver
**enable-spatial** flag + connection wiring (SQLite `LoadExtension`/`InitSpatialMetaData`, Postgres
PostGIS presence check). Builds directly on the Phase-1 `ColumnInfo.Geometry` descriptor. Live-table
reconciliation (UpdateTables) stays in Phase 3.*

## Goal

Make `CreateTable` / `CreateTables` emit, for a geometry column, the correct native spatial **column**
and its declared **spatial index** on each engine, gated by a capability flag; and make the SQLite and
PostgreSQL drivers actually usable at runtime via an opt-in enable-spatial flag. After Phase 2 a
geometry entity can be created and dropped on all five drivers, and the column + index verifiably exist.

## Per-driver SQL (condensed from `../GEO_COMMON_FUNCTIONALITY.md` Appendix A)

`sub` = subtype token (`POINT`/`LINESTRING`/…/`GEOMETRY` for any); `srid`, bbox, `tol` from the
descriptor; `dim` = XY / XYZ / XYM / XYZM.

| Engine | Geometry column | Spatial index |
|---|---|---|
| **PostGIS** | inline `geometry(<sub><Zsuffix>,<srid>)` (e.g. `geometry(PointZM,4326)`; `geometry(Geometry,4326)` for any) | `CREATE INDEX <ix> ON <t> USING GIST (<col>)` |
| **MySQL 8** | inline `<sub> [NOT NULL] SRID <srid>` — **NOT NULL required when indexed**; **2-D only** (Z/M unsupported → SD1) | `CREATE SPATIAL INDEX <ix> ON <t>(<col>)` |
| **SQL Server** | inline `geometry` (SRID enforced by the constructor, not the type) | `CREATE SPATIAL INDEX <ix> ON <t>(<col>) USING GEOMETRY_GRID WITH (BOUNDING_BOX=(xmin,ymin,xmax,ymax))` — needs clustered PK (entities have one) + **bbox required** (SD5) |
| **Oracle (Locator)** | inline `SDO_GEOMETRY` | **post-create:** `INSERT INTO USER_SDO_GEOM_METADATA(...) VALUES(<t>,<col>,SDO_DIM_ARRAY(...bbox+tol...),<srid>)` **then** `CREATE INDEX <ix> ON <t>(<col>) INDEXTYPE IS MDSYS.SPATIAL_INDEX_V2` |
| **SpatiaLite** | **NOT inline** — after CREATE TABLE: `SELECT InitSpatialMetaData()` (once/db) → `SELECT AddGeometryColumn('<t>','<col>',<srid>,'<sub>','<dim>')` → `SELECT CreateSpatialIndex('<t>','<col>')` |

## Seams & new API (verified against the codebase)

### 1. Capability gate — `SupportsGeometry`
New `public virtual bool SupportsGeometry => false;` on `SqlDbLanguageSpecifics`
(`SqlLanguageSpecifics.cs:50`, beside `SupportsJson`), overridden `=> true;` on all five driver
specifics (MSSQL/MySQL/Oracle/Postgres/SQLite). Guard added in `TableDdlBuilder.HandleColumnDDL`
(`:19`) beside the JSON guard: `if (column.Geometry != null && !mSpecifics.SupportsGeometry) throw new
EfSqlException(EfExceptionCode.FeatureNotSupported);`.

### 2. Inline column type — `GeometryColumnDDL(ColumnInfo)`
New method on `SqlDbLanguageSpecifics` (base: throw `FeatureNotSupported`) returning the **full inline
column tail** (type + any SRID/NOT-NULL the engine folds into the column, e.g. MySQL
`GEOMETRY NOT NULL SRID 4326`). Called from `HandleColumnDDL` when `column.Geometry != null` instead of
`TypeName(...)`; the geometry branch owns its own null/constraint tail and returns (skips the generic
NOT NULL/UNIQUE/DEFAULT appends). **Oracle's `OracleTableDdlBuilder.HandleColumnDDL`
(`OracleTableDdlBuilder.cs:31`) bypasses base**, so the same branch is added there too. Overridden on
Postgres / MySQL / MSSQL / Oracle. **SpatiaLite does not emit inline** (next item).

### 3. SpatiaLite: suppress inline, add post-create
The CREATE-TABLE column loop (`CreateTableBuilder.PrepareQuery:67-75`) is extended to skip a column when
the DDL builder says so: new `protected virtual bool SkipInlineColumn(ColumnInfo) => false;` on
`TableDdlBuilder`, overridden in `SqliteTableDdlBuilder` to return `true` for a geometry column. The loop
checks `SkipInlineColumn` **before** its comma bookkeeping so no dangling comma results. (Default false ⇒
no change for any existing column.)

### 4. Post-create steps + spatial index — `HandleGeometryAfterQuery`
`HandleAfterQuery` is already looped per column at `CreateTableBuilder.PrepareQuery:87-88`
(`TableDdlBuilder.HandleAfterQuery` base `:65`). Add, in the base, `if (column.Geometry != null)
HandleGeometryAfterQuery(builder, descriptor, column);` with a new
`protected virtual void HandleGeometryAfterQuery(...)` (base: no-op). Each driver's DDL builder overrides
it to emit — using the `PreQueryInBlock`/`PostQueryInBlock` + `TerminateWithSemicolon` wrappers so the
statements concatenate into the single `mQuery` (Oracle statements sit inside `EXECUTE IMMEDIATE '…'`,
embedded single quotes doubled):
- **SpatiaLite:** `AddGeometryColumn` (+ once-per-build `InitSpatialMetaData`) then, per declared index,
  `CreateSpatialIndex`.
- **Oracle:** per declared index, the `USER_SDO_GEOM_METADATA` insert then `CREATE INDEX … MDSYS…`.
- **PostGIS / MySQL / MSSQL:** per declared index, the engine's `CREATE [SPATIAL] INDEX …` (GIST /
  SPATIAL / GEOMETRY_GRID+bbox). Index logical name via `mSpecifics.IndexName(table, def.Name)` (the
  Phase-1 `SpatialIndexDefinition.Name`).

Spatial indexes are emitted **directly from `column.Geometry.Indexes`** (create-time), not via
`CompositeIndex` — their DDL is per-engine and non-generic. Making `UpdateTables` reconcile them
(surfacing them through `GetTableIndexes` / a `CompositeIndex.ForSpatial` channel) is **Phase 3** (SD2).

### 5. Enable-spatial flag + connection wiring
Runtime opt-in via **per-driver statics** (mirroring `SqliteGlobalOptions.StoreDateAsString` /
`PostgresDbConnectionFactory.LegacyTimestampBehavior`), since the reflection-based
`UniversalSqlDbFactory` contract only accepts `Create(string)` — no options object (SD3):
- **SQLite:** `SqliteGlobalOptions.EnableSpatial` (default false). When true, in the `SqliteDbConnection`
  constructor (`SqliteConnection.cs:26`, where `SetupFunctions` already runs post-open):
  `mSqlConnection.EnableExtensions(true); mSqlConnection.LoadExtension("mod_spatialite");` plus an
  optional custom library name/path. `InitSpatialMetaData()` is run lazily when the first geometry table
  is created (guarded by a `spatial_ref_sys`-existence probe, reusing the `SQLITE_MASTER` idiom from
  `DoesObjectExistCore`, `SqliteConnection.cs:403`) — SD4.
- **PostgreSQL:** `PostgresDbConnectionFactory.EnableSpatial` (default false). When true, after
  `Open()` (`PostgresConnection.cs:266`), run `SELECT 1 FROM pg_extension WHERE extname='postgis'` and
  throw a clear error if absent (fail fast).
- **MSSQL / MySQL / Oracle:** no connection-level work (built-in); `SupportsGeometry => true` only.

### 6. Z/M per driver (decision 14 caveat)
PostGIS / SpatiaLite / MSSQL / Oracle carry Z/M in the column (dim suffix / `AddGeometryColumn` dim /
native). **MySQL is 2-D only** → SD1: `GeometryColumnDDL` on MySQL throws `FeatureNotSupported` when
`HasZ`/`HasM` (fail fast) rather than silently dropping ordinates.

## Where it is implemented

- `Gehtsoft.EF.Db.SqlDb`: `SqlLanguageSpecifics.cs` (`SupportsGeometry`, `GeometryColumnDDL` base),
  `QueryBuilder/TableDdlBuilder.cs` (geometry guard + branch, `SkipInlineColumn`,
  `HandleGeometryAfterQuery` base), `QueryBuilder/CreateTableBuilder.cs` (loop skip hook).
- Each driver project: `*LanguageSpecifics.cs` (`SupportsGeometry`, `GeometryColumnDDL`), its
  `*TableDdlBuilder.cs` (`HandleGeometryAfterQuery`; SQLite also `SkipInlineColumn`; Oracle also the
  column-DDL geometry branch), and — SQLite/Postgres — the connection/factory enable-spatial wiring +
  `*GlobalOptions`/factory static.
- All product `.cs` in `EnableDefaultCompileItems=false` projects need `<Compile Include>` (Db.SqlDb;
  the driver projects use default items except where noted — confirm per project).

## How it is tested

- **Deep (AST + gate), no DB — runs everywhere.** Parse the generated CREATE-TABLE SQL and assert the
  geometry column type + the spatial-index statement per driver via the driver's language specifics
  (build the SQL through the driver `CreateTableBuilder` with a geometry `TableDescriptor`); assert the
  `SupportsGeometry` guard throws on a non-spatial dialect; MySQL Z/M throws (SD1); MSSQL/Oracle
  missing-bbox throws (SD5). No live DB.
- **SQLite + SpatiaLite behavioural** *(requires `mod_spatialite` on the machine — documented
  prerequisite; `SqliteGlobalOptions.EnableSpatial = true`)*: create a geometry entity, assert the
  column and the spatial index exist via `DoesObjectExist(table, col, "column")` / `…, "index")`; drop.
- **Acceptance — five drivers** `[Theory][MemberData(ConnectionNames)]`: create + drop a geometry table
  (each subtype where the engine allows), assert column/index presence. SQLite(+SpatiaLite) always on;
  others per local config.
- Tests under `Gehtsoft.EF.Test/Geo/TableManagement/`.

## Sub-decisions to confirm at Gate 2

- **SD1 — MySQL Z/M:** throw `FeatureNotSupported` when Z/M declared on a MySQL geometry column
  (recommended) vs silently store 2-D.
- **SD2 — spatial-index model:** emit directly from `column.Geometry.Indexes` at create-time now, defer
  reconciliation to Phase 3 (recommended) vs introduce the `CompositeIndex.ForSpatial` reconciliation
  channel already in Phase 2.
- **SD3 — enable-spatial surface:** per-driver statics `SqliteGlobalOptions.EnableSpatial` /
  `PostgresDbConnectionFactory.EnableSpatial` (recommended, matches existing toggles) vs a
  connection-string keyword.
- **SD4 — SpatiaLite `InitSpatialMetaData`:** auto-run once-per-db (guarded probe) on first geometry
  table create (recommended) vs require the app to call it.
- **SD5 — MSSQL/Oracle bbox:** a spatial index declared without a bounding box on MSSQL (or without
  dims/tolerance metadata on Oracle) throws a clear error (recommended).

## Acceptance criteria

- `SupportsGeometry` + `GeometryColumnDDL` on all five drivers; geometry column + declared spatial
  index emitted at CREATE TABLE per engine; SQLite suppresses inline + adds post-create.
- Enable-spatial flag wired for SQLite (LoadExtension + InitSpatialMetaData) and Postgres (PostGIS
  check); no-op elsewhere.
- Deep AST/gate tests green everywhere; SpatiaLite behavioural create/drop green where `mod_spatialite`
  is present; acceptance green on locally-configured drivers.
- Solution builds clean; `version.proj` untouched; no commit unless asked.
