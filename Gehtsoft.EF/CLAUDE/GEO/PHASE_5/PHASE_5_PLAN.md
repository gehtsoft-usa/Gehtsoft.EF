# GEO Phase 5 — Entity insert / update (whole geometry value)

*Plan drafted 2026-07-20 for the Gate. Read `../GEO_PLAN.md` (Phase 5 bullet + "SQL-builder layer
first, entity queries delegate to it"), `../STATE.md`, and `../PHASE_4/PHASE_4_PLAN.md` first.
**Nothing is coded until this plan is approved (Gate 1).** Coding then proceeds increment-by-increment,
with the second per-phase advance gate at the end.*

## Goal (from GEO_PLAN Phase 5)

> Entity `GetInsertEntityQuery` / single-entity update round-trip the geometry through the Phase-4
> value-wrap, using the `GeometryPropertyAccessor` + geo descriptor that Phase 1 already installed via
> `ColumnDiscoverer`. The entity layer here is a **thin consumer** of Phase-1 discovery and the Phase-4
> SQL wrapping — no new query mechanics. Deep: entity INSERT/UPDATE SQL (AST); null handling.
> Acceptance: full round-trip of each subtype + nullable on five drivers.

Phase 4 already built and verified (on all six engine families) the pure-SQL value-wrap
(`InsertQueryBuilder.SetColumnValueExpressions` + `ParameterToken`;
`UpdateQueryBuilder.AddUpdateColumnExpression`), the `SqlGeoFunctionId.FromWkb`/`AsBinary` renderers,
the select output-wrap (`SelectQueryBuilder.AddGeometryValueToResultset`), and the NTS
`BindGeometryParam`/`GetGeometry` extensions. **Phase 5 wires the entity insert/update path onto them.**

## What already works for free (verified seams — file:line)

- **The WKB value is already bound transparently.** `GeometryPropertyAccessor` presents
  `PropertyType = byte[]` and the column is discovered as `DbType.Binary`
  (`ColumnDiscoverer.CreateGeometryColumnDescriptor`), so `UpdateQueryToTypeBinder.BindAndExecuteCore`
  (`UpdateQueryToTypeBinder.cs:399-438`) binds the WKB `byte[]` with **no change**. `null` geometry →
  `BindNull` → SQL `NULL`. So Phase 5 needs **no binder work** — only the SQL-side wrap.
- **INSERT column loop** — `InsertQueryBuilder.PrepareQuery` (`InsertQueryBuilder.cs:110-145`) iterates
  `mTable`'s columns; each column emits either an autoincrement expression, an explicit value expression
  from `SetColumnValueExpressions` (`:135-139`, `ParameterToken` at `:82`), or the plain bound parameter
  (`:141-144`). `info.Geometry` (the `GeometryColumnMetadata`) is reachable right here.
- **UPDATE column loop** — `UpdateQueryBuilder.AddUpdateAllColumns` (`UpdateQueryBuilder.cs:92-99`) calls
  `AddUpdateColumn(column)` (plain `col=@param`, `:33`) for every non-PK column. `AddUpdateColumnExpression`
  (`:54`, the raw-expression form used by the Phase-4 tests) is the wrap entry point. `mDescriptor`
  carries `column.Geometry`.
- **Entity builders** — `InsertEntityQueryBuilder` (`ModifyEntityQuery.cs:14-26`) creates the insert
  builder + binder; `UpdateEntityQueryBuilder.PrepareBinder` (`:106-117`) calls `AddUpdateAllColumns()`
  then binds all columns. Neither is geo-aware today (grep: no `Geo`/`Geometry` in the entity-query
  classes).
- **The wrap SQL** — `mSpecifics.GeometryFunction(new GeoFunctionRequest(SqlGeoFunctionId.FromWkb,
  parameter: <ref>, srid: column.Geometry.Srid))` → `ST_GeomFromWKB(<ref>, <srid>)` /
  `geometry::STGeomFromWKB(...)` / `SDO_UTIL.FROM_WKBGEOMETRY(...)` per driver (`SqlLanguageSpecifics.cs:108`).

## The one design decision for the Gate — where the value-wrap lives

The WKB parameter is bound fine; the only missing piece is emitting the constructor-function wrap around
the parameter reference on the INSERT VALUES side and the UPDATE SET side for a geometry column. Two
placements:

- **Option A — auto-wrap in the pure-SQL builders (keyed on `column.Geometry`), as a fallback.**
  `InsertQueryBuilder.PrepareQuery` and `UpdateQueryBuilder.AddUpdateAllColumns` detect
  `column.Geometry != null` and emit the `FromWkb` wrap automatically. An explicit
  `SetColumnValueExpressions`/`AddUpdateColumnExpression` still **overrides** (checked first), so the
  Phase-4 tests that set the wrap explicitly are unaffected (pure additive fallback). **Entity
  insert/update stay zero-touch** — exactly like JSON's entity insert/update, which needed no hook. Any
  hand-built pure-SQL insert/update over a geo table also becomes correct automatically. Cost: two small
  additive touches to Phase-4-committed pure-SQL builders (behind the `column.Geometry` gate; no effect
  on non-geo tables).
- **Option B — inject from the entity layer only.** Keep the pure-SQL builders exactly as committed in
  Phase 4. `InsertEntityQueryBuilder` calls `SetColumnValueExpressions(...)` for each geo column in its
  constructor; the entity UPDATE path replaces the `AddUpdateAllColumns()` call with a geo-aware loop
  (plain `AddUpdateColumn` for non-geo, `AddUpdateColumnExpression(FromWkb)` for geo). Geo-awareness is
  confined to the two entity builders; Phase-4 pure-SQL frozen. Cost: the entity UPDATE builder
  re-implements the `AddUpdateAllColumns` loop so it can branch per column; geo-awareness now lives in
  two layers (pure-SQL renderers + entity builders).

**Recommendation: Option A.** It makes the entity layer genuinely thin (mirrors JSON insert/update
needing no hook), keeps a single source of truth for "a geometry column must be constructor-wrapped,"
and fixes direct pure-SQL builder callers too. The touch is a guarded, additive fallback that the
existing exact-SQL Phase-4 tests will confirm causes no drift. (Either option is small; this decision
just fixes which file the wrap logic lands in.)

## Increments (each ends green; commit only when the user asks)

1. **Value-wrap placement (per the Gate decision).** Implement Option A or B. If A: add the
   `column.Geometry` fallback branch to `InsertQueryBuilder.PrepareQuery` and
   `UpdateQueryBuilder.AddUpdateAllColumns`, using `mSpecifics.GeometryFunction(FromWkb, …, srid)` with
   `suppressScalarProtection` where the raw expression is added (the `FromWkb` SQL has no quoted literal,
   but keep parity with the Phase-4 direct-add discipline). Re-run the **Phase-4 exact-SQL suite** to
   prove no drift on the explicit path.
2. **Deep AST tests.** New `Geo/DataManagement/GeometryEntityInsertUpdateSqlTest.cs` (DB-free, via
   `DummyDbSpecifics` with `SupportsGeometrySpec`): build the entity INSERT and single-entity UPDATE for
   an entity carrying a geometry property (both a `byte[]` property and an NTS-object property), parse the
   generated SQL (`.ParseSql()`), and assert the geo column's VALUES/SET side is the `FromWkb(@p,srid)`
   wrap while non-geo columns stay plain parameters. Assert `null` geometry still emits a plain parameter
   bound to NULL (no wrap needed for NULL, or wrap-of-NULL is engine-safe — verify which and document).
3. **Behavioural round-trip (SpatiaLite live).** New `Geo/DataManagement/GeometryEntityRoundTripSpatialiteTest.cs`:
   through the shipping entity API — `connection.GetInsertEntityQuery(type)` / `GetUpdateEntityQuery(type)`
   (via `CatalogEntityController` for DDL) — insert an entity with a geometry, **read it back with the
   Phase-4 pure-SQL select output-wrap + `GetGeometry`** (entity SELECT is Phase 6), assert geometry
   equality within tolerance; then update the geometry, re-read, assert. Cover a `byte[]` property and an
   NTS-object property, plus a nullable geometry set to `null`. Reuse `Geo/SpatialiteTestSupport` +
   `GeometryRoundTripSupport`.
4. **Acceptance tier — all 5 server engines.** New `Geo/DataManagement/GeometryEntityRoundTripAcceptanceTest.cs`
   (`[Theory]` over the live connection names, mirroring `GeometryRoundTripAcceptanceTest`): entity
   insert → pure-SQL read-back → entity update → re-read, one 2-D point on a non-indexed generic geo
   column (SRID 0 Cartesian, per the Phase-4 all-engine technique so every engine round-trips the
   coordinates identically). Confirms the entity write path on MSSQL / Oracle / PostGIS / MariaDB /
   MySQL 8. **★ PHASE 5 COMPLETE** when green on every configured engine.

## Testing model (mirrors Phase 4)

- **DB-free AST** (increment 2): exact generated SQL parsed to AST — runs everywhere.
- **SpatiaLite behavioural** (increment 3): entity write → value read-back, geometry equality.
- **Acceptance** (increment 4): the same round-trip over every live server engine.
- Tests under `Gehtsoft.EF.Test/Geo/DataManagement/` (default compile items — no `<Compile Include>`).
- Real datasets in `Gehtsoft.EF.Test/GeoTestData/` for meaningful geometries where practical.

## Explicitly OUT of scope (Phase 5)

- Entity **WHERE / mass delete / select / count** → **Phase 6.**
- Entity **projection / scalar order-by / group-by / aggregation** → **Phase 7.**
- Entity **mass update** of a geo field (decision 12 — out of the feature entirely).
- Updating a **single explicit** geo column via a targeted entity update API is not in the Phase-5
  acceptance (which is update-all-by-id); note as a follow-up if a user needs it before Phase 6/7.

## Risks

- **R5 (Oracle SRID 4326 vs 8307)** and the **generic-SRID-0 planar technique** already handled in
  Phase 4's acceptance harness — reuse it, assert coordinates tolerantly.
- **Object-property vs byte[]-property** parity: both resolve through `GeometryPropertyAccessor` /
  raw accessor at discovery; the entity write path is identical below the accessor. Test both to be sure.
- **NULL geometry wrap:** confirm each engine's constructor function isn't invoked on a NULL parameter
  (the loops emit a plain parameter for a bound NULL; verify the wrap is skipped or NULL-safe). Covered
  by increment 2 + 3.
