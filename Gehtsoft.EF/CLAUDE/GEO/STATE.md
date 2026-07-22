# GEO feature — current state (resume point)

*Snapshot updated 2026-07-22. Read this first when resuming. Branch `geo`. Companion docs: `GEO_PLAN.md`
(overall, has the ARCHITECTURE REVISION banner), `GEO_COMMON_FUNCTIONALITY.md` (per-driver SQL map),
`PHASE_0/`..`PHASE_4/` plans, **`PHASE_5/PHASE_5_PLAN.md`** (the entity-level surface — gates LOCKED,
Increment 1 in progress), `ENTITY_API_REVIEW.md`, and **`PREREQUISITES_STATE.md`**.*

## TL;DR

Phases **0, 1, 2, 3 done.** Phase 3 (table update) is committed on branch `geo` at **`4c666c3`**
(`geo Phase 3 (table update) + MariaDB/MySQL 8 dialects`), package bumps at `b7f36a8`. Geo table
management (create + update: add geo column, add/drop spatial index, drop geo column) is verified on all
five engine families — SQL Server, Oracle, PostGIS, MariaDB, MySQL 8 — via `GeometryEngineAcceptanceTest`.
Full suite **4006 green** across 6 live connections. (Earlier return point `533f8b6` = Phases 0-2.)

**▶ Phase 4 — the geo QUERY surface (pure-SQL builder layer). ★ COMPLETE (committed `965fdcc`, not pushed).
Plan + gate decisions in `PHASE_4/PHASE_4_PLAN.md`.** Gate decisions (user, 2026-07-20): (1) two dedicated enums + two render methods
(`SqlGeoFunctionId`/`GeometryFunction`, `SqlGeoPredicateId`/`GeometryPredicate`) — SOLID, generic
`SqlFunctionId` untouched; (2) WKB only (no WKT); (3) core stays `byte[]`-only — object encode/decode lives
in the NTS module as extension methods, no core decode registry; (4) tests under `Geo/{DataManagement,DataSelecting}/`.
Four increments: **1 renderers ✅ DONE** · **2 insert/update value-wrap + select output-wrap + NTS
`BindGeometryParam`/`GetGeometry` extensions ✅ DONE** · **3 WHERE predicates/measure/within-distance + mass
delete ✅ DONE** · **4 projection + scalar order/group/agg ✅ DONE. ★ PHASE 4 COMPLETE.**

**Increment 1 DONE (2026-07-20, committed 965fdcc).** `SqlGeoFunctionId`/`SqlGeoPredicateId` enums +
`GeoFunctionRequest`/`GeoPredicateRequest` structs + base `GeometryFunction`/`GeometryPredicate` (throw) +
`SupportsGeometryQuery` in `SqlLanguageSpecifics.cs`; shared `RenderOgcGeometry{Function,Predicate}` reused by
PG/MySQL/SpatiaLite; MSSQL (method-call, bit `= 1`) + Oracle (`SDO_*`, RELATE mask `<> 'FALSE'`) own renderers.
Oracle `Crosses` **and** `IsEmpty` throw `FeatureNotSupported`. Tests `Geo/DataSelecting/GeometryRenderTest.cs`
(16, exact-string per the DDL-gen precedent). Geo suite **73 green** (was 57).

**Increment 2 DONE (2026-07-20, committed 965fdcc).** Insert value-wrap (`InsertQueryBuilder.SetColumnValueExpressions`
+ `ParameterToken`), update via existing `AddUpdateColumnExpression`, select output-wrap
(`SelectQueryBuilder.AddGeometryValueToResultset`). NTS module gained `GeometrySqlExtensions.BindGeometryParam`/
`GetGeometry` (+ ProjectReference NTS→Db.SqlDb). **Codec fix:** `NtsGeometryCodec.ToWkb` no longer forces
XYZM on every geometry (broke 2-D round-trip through fixed-dimension columns) — now emits the geometry's own
ordinates. `DummyDbSpecifics` gained opt-in `SupportsGeometrySpec` (OGC rendering) for DB-free tests. Tests:
`Geo/DataManagement/{GeometryValueWrapTest,GeometryRoundTripSpatialiteTest}` + `Geo/DataSelecting/GeometryOutputWrapTest`.
JSON/DynProps/builder regression 1125 green.

**All-engine acceptance verified (2026-07-20, Linux).** `Geo/DataManagement/GeometryRoundTripAcceptanceTest`
runs the insert→select→update→reselect value round-trip through the shipping `CatalogEntityController` +
pure-SQL builders over **every live engine**: SQL Server, Oracle 18, PostGIS, MariaDB (`mysql@25`), MySQL 8
(`mysql8@25`) — **all ✅** (non-indexed geo column; 2-D point; Oracle round-trips coordinates even though
`FROM_WKBGEOMETRY` sets no SRID, per R5). SpatiaLite covered by its own test. Shared helper
`Geo/GeometryRoundTripSupport`. **Geo suite 83 green.** So the Increment-2 query surface (insert value-wrap +
select output-wrap + NTS bind/read) is confirmed on all six driver families, not just SpatiaLite.

**Increment 3 DONE (2026-07-20, committed 965fdcc).** Spatial WHERE + mass delete at the builder layer.
`ConditionBuilder` gained `GeoPredicate` (complete boolean: 8 topological + `DWithin`; guard-bypassing
`SetGeoSide`→`Push`→`Add(logOp,string)`) and `GeoScalar` (measurement/accessor as a comparison operand),
on both `SingleConditionBuilder` and `ConditionBuilderExtension` (`Where.GeoPredicate(...)`). Mass delete =
`DeleteQueryBuilder`+geo `Where`. Oracle `Crosses` throws (renderer). Tests: `Geo/DataSelecting/`
`GeometryPredicateSqlTest` (DB-free), `GeometryPredicateSpatialiteTest` (live: Intersects+DWithin+delete),
`GeometryPredicateAcceptanceTest` (**all 5 engines**, topological Intersects+delete). Distance stays
SpatiaLite-only (planar vs geodetic differ per engine); **MySQL 8 validates lat/lon for SRID 4326** so test
coords are valid geographic. **Geo suite 93 green**; condition-builder+JSON regression 124 green.

**Full spatial-op matrix now live-verified on ALL 5 server engines (2026-07-20).**
`Geo/DataSelecting/GeometrySpatialOpsAcceptanceTest` exercises **all 8 topological predicates**
(Equals/Disjoint/Intersects/Within/Contains/Touches/Overlaps + Crosses), **within-distance**, and the
**accessors** (X/Y portable values; GeometryType/Envelope/IsEmpty/Srid execution-smoke) against MSSQL,
Oracle, PostGIS, MariaDB, MySQL 8 — **50 cases, all green.** Key technique: a **generic-geometry column
with SRID 0 (Cartesian)** so every engine evaluates planar (MySQL 8 otherwise treats 4326 geodetically +
validates lat/lon), making results identical everywhere; real polygons/lines (CCW rings for Oracle).
Oracle `Crosses` and `IsEmpty` throw (asserted); Oracle `SDO_SRID` is null so `Srid` smoke is non-Oracle.
So the whole geometry WHERE surface — not just Intersects — is confirmed on real databases. **Geo suite
143 green.**

**Increment 4 DONE (2026-07-20, committed 965fdcc) — ★ PHASE 4 COMPLETE.** Geo scalar projection + ORDER BY /
GROUP BY / aggregation. `SelectQueryBuilder` gained `AddGeometryScalarTo{Resultset(+AggFn),OrderBy,GroupBy}`
via one private byte-identical `GeometryScalarExpr` (GROUP BY match), guard-bypassing. **Value-correctness
verified live on SpatiaLite + all 5 server engines** (`GeometryProjectionChecks` shared by
`Geo/DataSelecting/GeometryProjection{Spatialite,Acceptance}Test`; DB-free exact-string in
`GeometryProjectionSqlTest`): Area(2×2 box)=4, Length=3, X/Y=3/4, Distance=5, order-by-distance=[1,2,3]+top-N,
AVG(Area)=10, GROUP BY Area→{4:2,16:1}. Measurements got their live coverage here. **Geo suite 152 green**;
JSON/select-builder regression 128 green.

**PHASE 4 (pure-SQL geo query surface) is complete** — insert/update/select value round-trip, all 8
predicates + within-distance + mass delete, all accessors, scalar projection + order/group/aggregation — all
verified on real databases across SpatiaLite + MSSQL + Oracle + PostGIS + MariaDB + MySQL 8. **Committed
`965fdcc` (not pushed).**

**▶ PHASE 5 — entity-level geo surface (WHERE · modification · SELECT clauses). Plan in
`PHASE_5/PHASE_5_PLAN.md`; all 3 gate decisions LOCKED (2026-07-22).** The user's framing ("we cannot do it
purely on SQL wrapping") is correct — the plan is delegate-where-the-abstraction-matches, re-implement-where-
it-differs. **Locked gates:** (1) value-wrap = **Option A** (auto-wrap in the pure-SQL `Insert/UpdateQueryBuilder`
keyed on `column.Geometry`, mirroring the autoincrement metadata-driven precedent; explicit
`SetColumnValueExpressions` still overrides), **built bottom-up: prove at the pure-SQL layer first**; (2)
resolution seam = **P-A** (extend `IEntityInfoProvider` with a `(ColumnInfo, QueryBuilderEntity)` resolver —
JSON's alias-only path can't carry `column.Geometry.Srid`); (3) mass-update of a geo field stays OUT (decision
12), HAVING has no dedicated geo API (generic path only). Framework limits honored: no driver knowledge in
core, no hand-written SQL. Increments: **1 pure-SQL value-wrap + null verification** · 2 entity insert/update
round-trip · 3 resolution seam · 4 WHERE + mass-delete · 5 SELECT clauses.

**Increment 1 — STARTED (2026-07-22, UNCOMMITTED).** Began with the NULL round-trip verification (user
directive: test null propagation through the driver conversion functions first). New tests
`Geo/DataManagement/GeometryNullRoundTrip{Spatialite,Acceptance}Test.cs` write a NULL geometry (the
`FromWkb(@p,srid)` value-wrap is still emitted, param bound to NULL) and read it back through the `AsBinary`
output-wrap, expecting null — on all 6 drivers. This **caught a real Oracle product defect**:
`SDO_UTIL.FROM_WKBGEOMETRY(NULL)` (and `TO_WKBGEOMETRY(NULL)`) are Java stored procs that NPE on NULL
(ORA-29532). **Fixed** in `OracleDbLanguageSpecifics.GeometryFunction` — both WKB converters now render
`CASE WHEN <arg> IS NULL THEN NULL ELSE SDO_UTIL.…(<arg>) END` (Oracle-only, in the driver, via the
specifics renderer; non-null behaviour identical). Phase-4 Oracle exact-SQL assertions in `GeometryRenderTest`
updated to the guarded strings. **Full Geo suite 163 green** (was 157; +6 null cases), 0 skipped.
**NEXT: implement the Option-A auto-wrap in `InsertQueryBuilder.PrepareQuery` +
`UpdateQueryBuilder.AddUpdateColumn` (keyed on `column.Geometry`), then re-verify the round-trip (non-null +
null) relies on it with the explicit-expression path still overriding — finishing Increment 1.**

**Phase-4 surface amendment — two-form geometry read + subquery predicate operand (2026-07-20, UNCOMMITTED).**
Design conversation with the user pinned the governing rule: **client→server is always `byte[]`→
`ST_GeomFromWKB` (one form); a server-side value's form depends on its destination** — WKB (`ST_AsBinary`)
if it goes to the client, **native** (raw, unwrapped) if it stays on the server (feeds another predicate/
function). Two additive changes to the committed Phase-4 pure-SQL surface implement this:
  - **`GeometryValueForm { Wkb, Native }`** enum (`SqlLanguageSpecifics.cs`, by the geo enums).
    `SelectQueryBuilder.AddGeometryValueToResultset(...)` gained a trailing `form = Wkb` (default =
    committed behaviour). `Native` emits the **raw column** (no `ST_AsBinary`), `DbType.Object` — a
    server-side operand, **not** portably client-readable (a driver-aware client can still `GetValue<object>()`
    and interpret the engine's native form itself).
  - **`ConditionBuilder.GeoPredicate(..., AQueryBuilder subquery, ...)`** overloads (+ `Where.GeoPredicate`
    extensions): the operand is a **subquery yielding a native geometry**, passed straight through as the
    predicate's `b` (no `FromWkb` wrap). Closes the Phase-4 gap that `GeoPredicate` hardwired the operand to
    a bound WKB parameter (`GeoPredicateCore`), so a subquery/column operand was previously inexpressible.
  Tests: `Geo/DataSelecting/GeometryNativeFormSqlTest.cs` (3 DB-free AST — native raw projection, Wkb-is-
  default, native-subquery predicate) + `GeometryNativeSubquerySpatialiteTest.cs` (live SpatiaLite: cities
  intersecting a region polygon from a native-form subquery → 2). **Geo suite 156 green** (was 152);
  full/natural-order `BasicQueryTests` class 118 green with these edits present (no regression). *(A T2_4
  miscount appears only when a fixture-sharing `[TestOrder]` subset is run out of sequence via an ad-hoc
  `~Select` filter — invalid filter selection, reproduces identically on clean HEAD; the full suite is
  unaffected.)*

**Select-focused PLAYGROUND over real datasets (2026-07-20, UNCOMMITTED).** A practical, SpatiaLite-backed
harness that solves real-world tasks purely through the geo query surface, doubling as coverage. Source data
lives in `CLAUDE/GEO/GeoTestData/` (see its `DATASETS.md`): `usa/` (548 polygon features → 22 eastern states),
`ozi/citi/*.map` (6 OziExplorer city map-extents), `ozi/tracks/*.plt` (3 travel tracks). **Pipeline (user's
2-step design):** (1) THROWAWAY code decimated the raw data — states grouped by `ABBREV` into one geometry
each (`GeometryFixer` repairs FL's 208-fragment invalidity), tracks/states topology-preservingly simplified at
~0.01° (~1 km) — and wrote minimized WKT into `Gehtsoft.EF.Test/Geo/Playground/{states,cities,tracks}.tsv`;
(2) those `.tsv` are **embedded resources** (csproj `<EmbeddedResource LogicalName="geo.playground.*">`) loaded
at runtime by `GeoPlaygroundResources.cs` via the NTS codec — **no runtime dependency on the raw folder**. The
throwaway generator + folder-reading loader were deleted after generating. `GeoTestData.cs` locator also now
finds the data under `CLAUDE/GEO/GeoTestData` (supports the pre-existing `GeoThirdPartyFileTest`). Harness:
`Geo/GeoPlaygroundSpatialiteTest.cs` — entities `PgState`(generic-`Geometry` col, mixed POLYGON/MULTIPOLYGON),
`PgCity`(Polygon extent + Point center — **two geo columns per entity, works**), `PgTrack`(MultiLineString);
inserts via pure-SQL `InsertQueryBuilder`+`FromWkb` wrap. **Tasks solved & asserted:** states by `ST_Area`
DESC (matches source `AREA` to 3 dp — e.g. NY 13.888 vs 13.890); city→containing state via `Contains`
(charlotte/wilmington/emerald-isle→NC, richmond→VA, new-york→NY, washington-dc→VA); track→crossed states via
`Intersects` (nj2nc→[NC,VA,MD,NJ,DE,DC], nj2oh→[PA,OH,NJ], nc2tx→[NC,TN]). **Extended (2026-07-20) with 3
more task groups:** (4) **nearest** (order-by-`Distance` top-3 to a probe point → new-york 0.653/dc 3.615/
richmond 4.693) + **`DWithin`** cross-checked against projected `ST_Distance` (richmond sits 0.030° from the
nj2nc track, dc 0.131°, ny 0.744°; `DWithin(1.0°)`==the ≤1° set — predicate agrees with the measurements);
(5) **projection** `ST_Length` (nc2tx 20.157° ≫ nj2nc 8.303° > nj2oh 8.157°) + **GROUP BY region** with
COUNT + `AVG(ST_Area)` (New England 6 states, Southeast 4/avg 10.350, …); (6) **native-form subquery operand
proven on real data** — states crossed by the track chosen via a `WHERE name='nj2nc'` subquery (operand
projected `Native`, no `FromWkb` wrap) → identical `[VA,NJ,NC,MD,DE,DC]` to the bound-parameter form. Also
validates **two geometry columns per entity** (`PgCity` Polygon extent + Point center). All in the single
`Playground` [Fact]. **Geo suite 157 green** (full suite baseline 4105 green). The select surface —
Contains/Intersects/DWithin, Area/Length/Distance, projection, ORDER BY, GROUP BY+agg, subquery operand — is
now exercised end-to-end on real US data. Committed baseline = user's commit; the 3 extension task groups
committed `04280c0`.

**▶ ENTITY-LEVEL API REVIEW (2026-07-20) — read `ENTITY_API_REVIEW.md` before planning Phases 5–7.** Design
review, informed by the playground, of (1) which special entity interfaces are needed (WHERE `GeoPredicateOf`/
`GeoScalarOf`; `AddGeometryScalarTo{Resultset,OrderBy,GroupBy}`; `AddGeometryValueToResultset(form)`; NTS-module
object overloads; + write/read wraps), (2) what works transparently (whole-entity save/read incl. geometry;
filter/order/group by *mapped* attrs — "states by area" via the stored `AREA` col needs zero geo API), and
(3) surviving conflicts (scalar/aggregate projections aren't typed entities; GROUP-BY vs whole-entity read;
Wkb/Native two-form read; core-can't-ref-NTS ergonomics split; entity-WHERE welding for mass ops; Oracle
Crosses/IsEmpty throw; planar-not-metric semantics; and the decision-12 gaps — spatial aggregates, mass geo
UPDATE, order/group on raw geometry — that the SQL layer deliberately can't provide). **DONE:** folded into
`PHASE_5/PHASE_5_PLAN.md` (the three areas WHERE/modification/SELECT, each answering interface+test /
delegate-vs-reimplement / why); all gates now LOCKED — see the Phase 5 block near the top of this file.

For **meaningful Phase-4 behavioural tests (increments 2-4), use the real datasets in
`Gehtsoft.EF.Test/GeoTestData/`** (test.wkt/test.wkb + `tiger-line/`, `usa/`, `mars-i-2294/`; LFS-tracked,
located at runtime by `Geo/GeoTestData.cs`) rather than toy geometries.

**GEO IS UN-PARKED (as of 2026-07-19/20).** The 2026-07-14 park ("catalogue first, geo rides it") is
resolved: the Schema Catalogue landed, geo Phase 3 (table update) rides it (committed `4c666c3`), and
Phase 4 (query surface) is now done (committed `965fdcc`). Process still applies for the remaining phases:
two human gates per phase; **commit only when the user asks**. (Historical park rationale kept below for
context.)

## Architecture (locked)

- Framework is **codec-agnostic with a `byte[]`/WKB core**. A geometry column is fundamentally
  `byte[]` (WKB). The framework owns **no** geometry type or parser.
- Extension point: object-based `IGeometryCodec` + `IGeometryCodecFactory` + static
  `GeometryCodecs.Factory` (**global only**, no per-connection override — WKB is universal across
  drivers). In `Gehtsoft.EF.Entities/Geometry/`.
- Sole shipped codec = **NetTopologySuite**, in separate module **`Gehtsoft.EF.Geo.NetTopologySuite`**
  (NTS 2.6.0). App calls `NtsGeometry.Register()`.
- A `[GeometryEntityProperty]` on a **`byte[]`** property → no codec/accessor; on an **object** (e.g.
  NTS `Geometry`) → decorating `GeometryPropertyAccessor` (object↔WKB via the global codec).
- Wire form is WKB (`ST_GeomFromWKB` / `ST_AsBinary`); the DB constructor takes SRID separately.

## Phase status

- **Phase 0 — codec abstraction + NTS module — DONE.** In-house `GeoGeometry`+codec RETIRED (deleted).
  `IGeometryCodec`/`IGeometryCodecFactory`/`GeometryCodecs` (Entities); `NtsGeometryCodec`/`Factory`/
  `NtsGeometry.Register` (NTS module). Tests in `Gehtsoft.EF.Test/Geo/` (codec round-trip incl. Z/M,
  3rd-party EWKT/EWKB files, registration).
- **Phase 1 — Declare — DONE.** `[GeometryEntityProperty]` (Field, Srid=4326, Subtype, HasZ, HasM,
  Nullable) + repeatable `[SpatialIndex]` (Name, bbox NaN-default, Tolerance=0.005) + `GeometrySubtype`
  enum (Entities). `GeometryColumnMetadata`+`SpatialIndexDefinition`, `GeometryPropertyAccessor`,
  `ColumnInfo.Geometry`, `ColumnDiscoverer` geo branch + guardrail (`EfExceptionCode.GeometryCodecNotFound`)
  (Db.SqlDb). Tests in `Gehtsoft.EF.Test/Geo/Entities/`.
- **Phase 2 — Table create (column + spatial index + enable-spatial) — DONE, 5 drivers.**
  `SupportsGeometry` gate + `GeometryColumnDDL` (base throw) on `SqlDbLanguageSpecifics`;
  `TableDdlBuilder` geometry guard/branch + `SkipInlineColumn` + `HandleGeometryAfterQuery`;
  `CreateTableBuilder` loop skip; `Metadata/GeometryDdlHelper`. Per driver: PostGIS
  `geometry(Sub+ZM,srid)`+GIST · MySQL `SUB NOT NULL SRID`+`CREATE SPATIAL INDEX` (throws on Z/M, 2-D
  only) · MSSQL `geometry`+`GEOMETRY_GRID`+`BOUNDING_BOX` (throws w/o bbox) · Oracle `SDO_GEOMETRY`+
  `USER_SDO_GEOM_METADATA`+`MDSYS.SPATIAL_INDEX_V2` · SpatiaLite `AddGeometryColumn`+`CreateSpatialIndex`.
  Enable-spatial: `SqliteGlobalOptions.EnableSpatial`/`SpatialiteLibrary` + `PostgresDbConnectionFactory.EnableSpatial`.
  Tests in `Gehtsoft.EF.Test/Geo/TableManagement/`.
- **Phase 3a — TableUpdate DDL seams + old-controller guard — DONE (2026-07-19), uncommitted.**
  Geo now rides the **Schema Catalogue** (the pre-catalogue `PHASE_3/PHASE_3_PLAN.md` enumeration
  approach — "Option A: surface spatial indexes through `GetTableIndexes`" — is **SUPERSEDED**; the
  catalogue diff computes column/index deltas from the stored DTO, so no per-driver spatial-index
  introspection is needed).
  - **Old controller fails loudly on geo:** `CreateEntityControllerInternal.LoadTypes` →
    `GuardNoGeometry()` throws `EfExceptionCode.GeometryRequiresCatalogController` when a discovered
    (non-view) entity has a geometry column. The obsolete public shim inherits it. Test:
    `Geo/TableManagement/GeometryOldControllerGuardTest`.
  - **ALTER-time DDL seams (create path untouched → Phase-2 output byte-identical):** base
    `TableDdlBuilder` gains `CollectRegisterGeometryColumn`/`CollectUnregisterGeometryColumn` (no-op
    default) + `CollectCreateSpatialIndex`/`CollectDropSpatialIndex` (throw `FeatureNotSupported`
    default). Base `AlterTableQueryBuilder` is now geo-aware (`HandleCreateQuery` →
    `HandleCreateGeometryColumn`, `HandleAfterCreateQuery`, `HandlePreDropQuery`); all 5 drivers inherit
    it via their existing `CreateDdlBuilder` override. MySQL `HandlePreDropQuery` now chains to base.
  - **Per-driver primitives:** PG `USING GIST` / `DROP INDEX` · MySQL `CREATE SPATIAL INDEX` /
    `DROP INDEX … ON t` · MSSQL `GEOMETRY_GRID`+bbox (throws w/o bbox) / `DROP INDEX … ON t` · Oracle
    `USER_SDO_GEOM_METADATA`+`SPATIAL_INDEX_V2` / `DROP INDEX`+`DELETE … METADATA` (single-quoted —
    bare exec, unlike the create path's EXECUTE-IMMEDIATE doubled quotes) · SpatiaLite
    `AddGeometryColumn`/`DiscardGeometryColumn`+`CreateSpatialIndex`/`DisableSpatialIndex`+drop R-tree.
  - **Verified behaviourally (not by AST/string — per-driver DDL is too divergent for that to be more
    than re-encoding the impl):** `Geo/TableManagement/GeometrySpatialiteBehaviourTest.
    AlterTable_AddsGeometryColumnAndSpatialIndex` creates a base table, ALTER-adds the geo column +
    spatial index through the catalogue's exact `GetAlterTableQueryBuilder → GetQuery → ExecuteNoData`
    path, asserts via `DoesObjectExist`. The other 4 engines are covered on the acceptance tier.
  - Full suite **3628 green** (was 3625; +2 guard, +1 ALTER-path).
- **Phase 3b — catalogue ApplyChanges wiring — DONE (2026-07-19), uncommitted.** The diff (`CatalogDiff`)
  and the DTO (`CatalogGeometryDto`/`CatalogSpatialIndexDto`) already modelled every geometry change; 3b
  was purely the ApplyChanges dispatch (previously a `default`-case throw).
  - **AddGeometryColumn** routes through the same `addColumns` path as a plain add (the 3a geo-aware
    `AlterTableQueryBuilder` emits column + register + spatial indexes). **DropGeometryColumn** routes
    through `dropColumns`; `ReconstructDroppedColumn` now rebuilds `.Geometry` (incl. its spatial indexes)
    from the catalogued DTO so `HandlePreDropQuery` tears down the indexes + unregisters first. Gated by
    `DropColumnSupported` (SpatiaLite skips, consistent).
  - **AddSpatialIndex / DropSpatialIndex** (standalone, on an unchanged geometry column) → new
    `ICatalogControllerAction.Create/DropSpatialIndex` + new
    `AlterTableQueryBuilder.Get{Create,Drop}SpatialIndexQueries` calling the 3a `Collect*` primitives.
  - **Data-loss pre-check** extended to `DropGeometryColumn` (refused under `DataLossPolicy.Fail` unless
    `[ObsoleteEntityProperty]`). Geometry **metadata** change (SRID/subtype/Z/M) stays refused via
    AlterColumn → route through an IEfPatch (drop+add would lose the data).
  - **Scalar-protection fix (the 3a finding):** `AddColumns`/`DropColumns` + the spatial-index action
    methods now use `GetQuery(queryText, suppressScalarProtection: true)` — SpatiaLite geo DDL is
    `SELECT AddGeometryColumn(...)`/`CreateSpatialIndex(...)`, which trips the scalar guard.
  - **`CatalogSerializer` fix:** enabled `JsonNumberHandling.AllowNamedFloatingPointLiterals` — a spatial
    index without a bounding box stores `NaN` bounds, which STJ rejects by default. Purely additive (no
    persisted geo catalogs existed); no schema-format bump. The diff's `DoubleEquals` already used
    `double.Equals` (NaN==NaN true), so re-diff is idempotent.
  - **Verified behaviourally** (live SpatiaLite, full catalogue UpdateTables path) in
    `Geo/TableManagement/CatalogGeometryUpdateTest` (add geo column+index, add spatial index, drop spatial
    index, implicit-drop data-loss refusal) — 4 green. Shared `Geo/SpatialiteTestSupport` helper added.

- **Acceptance tier — live server engines verified (2026-07-19), uncommitted.**
  `Geo/TableManagement/GeometryEngineAcceptanceTest` drives create-with-spatial-index + catalogue
  add-geometry-column through the shipping `CatalogEntityController` path over every configured live engine
  (bounding-boxed index so MSSQL/Oracle accept it; 2-D so MySQL would). Results on the 192.168.1.25 test box:
  - **SQL Server** — ✅ create + update.
  - **PostGIS** — ✅ create + update (required `CREATE EXTENSION postgis` **in the `test` DB** — extensions
    are per-database; it was missing there initially).
  - **Oracle** — ✅ create + update, **and now repeatable**. Fixed a real product gap: `DROP TABLE` on
    Oracle left orphaned `USER_SDO_GEOM_METADATA` rows, so a geometry table could not be recreated
    (`ORA-13223`). `OracleDropTableBuilder.AppendDropTable` now emits a `DELETE FROM USER_SDO_GEOM_METADATA`
    (EXCEPTION-guarded) for each geometry column before the `DROP TABLE`.
  - **MariaDB** (the `mysql@25` connection is MariaDB 10.5) — ✅ create + update, after adding a MariaDB
    dialect. MariaDB has no `SRID` column attribute (it carries the SRID on the value), so the geometry
    column DDL omits it.
  - **MySQL 8** (`mysql8@25`, port 3316) — ✅ create + update. (Config note: the `test` user was created as
    `create user 'test@localhost'` — the whole string is the username, host `%` — so the connection string
    uses `Uid=test@localhost`; MySQL 8's `caching_sha2_password` needs `AllowPublicKeyRetrieval=True`.)
  - **MySQL-family dialect split (no runtime flags — subclasses):** `MysqlDbLanguageSpecifics` is now an
    **abstract base** with two leaves — `MySql8LanguageSpecifics` and `MariaDbLanguageSpecifics` — selected
    per connection from the server banner (`ServerVersion` contains `"MariaDB"`; two static singletons on
    `MysqlDbConnection`). Each leaf overrides `AppendColumnSrid` (MySQL 8 emits `SRID <n>`, MariaDB nothing)
    and acts as the **factory** for its dialect-specific builders (`CreateDropIndexBuilder`,
    `CreateUpdateQueryBuilder`), so `MysqlDbConnection` never branches on a flag. Deterministic unit
    coverage: `GeometryDdlGenerationTest.MariaDb_Column_OmitsSrid` + MySQL-8 `...NotNullSrid_WhenIndexed`.
  - **All five engine families verified for geo create + catalogue table-update** (`GeometryEngineAcceptanceTest`,
    over every configured live connection; skips only a PostgreSQL DB without PostGIS).

- **General MySQL-8 dialect gaps fixed (2026-07-19, non-geo, exposed by adding a real MySQL 8 server
  `mysql8@25` at :3316; the suite had only ever run against MariaDB before).** Both via the dialect
  subclasses above (no flags):
  - **`DROP INDEX`** — MySQL 8 has no `DROP INDEX IF EXISTS`. `MariaDbDropIndexBuilder` uses the native
    `IF EXISTS`; `MySql8DropIndexBuilder` stays idempotent via an `information_schema` existence check
    driving `PREPARE`/`EXECUTE` (analogue of MSSQL's `IF IndexProperty(...)` / Oracle's `EXCEPTION WHEN
    OTHERS`). Covered by `MysqlDropIndexIdempotencyTest` (drops an absent index as a no-op on both servers).
  - **`UPDATE … SET = (correlated subquery over the target)`** — MySQL 8 rejects it (error 1093), MariaDB
    allows it. Base `UpdateQueryBuilder` gained a `protected virtual TransformSubquery` hook;
    `MySql8UpdateQueryBuilder` wraps the target's `FROM <t> AS <a>` into `FROM (SELECT * FROM <t>) AS <a>`
    (materialized derived table dodges 1093; the outer correlation is untouched). `MariaDbUpdateQueryBuilder`
    is the plain base. Verified by `BasicQueryTests.T2_4_UpdateUsingSelect` on both servers.
  - **Config note:** the earlier "13 failures" were mostly a **corrupted `test.db`** from two concurrent
    suite runs (`SQLite Error 11: malformed database schema`), cleared by deleting `test.db`; plus the 6
    real MySQL-8 gaps above (now all fixed). Windows SpatiaLite failures are the unverified Windows native
    path (Linux/macOS symbol-promotion only) — still to confirm.
  - **Windows status (user, 2026-07-19): the ONLY failing tests on Windows are the Windows `mod_spatialite`
    load path (see the SpatiaLite-native-fix TODO). Everything else is green on Windows** — so the sole
    remaining Windows work item is the unimplemented Windows SpatiaLite loader; no other regressions.

### Key decisions (see GEO_PLAN.md for the full list)
1 in-house→**NTS/byte[] pivot** · 8 SRID default 4326 · 11 Oracle `Crosses` throws (Phase 4/6) ·
13 enable-spatial via driver flag · 14 **Z/M supported** (MySQL is the 2-D exception → throws) ·
15 SRID on codec output by default · SD2 spatial-index reconciliation deferred to Phase 3.

## The SpatiaLite native fix (important, already implemented)

Loading `mod_spatialite` under `Microsoft.Data.Sqlite` SIGSEGV'd because `mod_spatialite` calls the
host's `sqlite3_*` symbols directly, but SQLitePCLRaw loads `libe_sqlite3.so` (no soname) with local
visibility. **Fix:** `SqliteDbConnection.PromoteSqliteSymbolsForSpatialite()` does
`dlopen(libe_sqlite3 full path, RTLD_NOW|RTLD_GLOBAL)` before `LoadExtension` (Linux/macOS; Windows
no-op — still to verify). Works with `Spatialite.Native` (test-proj pkg ref) and the system lib.

**✅ Windows `mod_spatialite` load path — IMPLEMENTED & VERIFIED ON WINDOWS (2026-07-20, committed 965fdcc).**
Root cause confirmed by inspecting the `Spatialite.Native` win-x64 DLLs (`objdump -p`): **there is NO
symbol-binding problem on Windows.** `mod_spatialite.dll` imports **no** sqlite3 DLL at all — it binds
SQLite through the extension API routines table, so the Unix `RTLD_GLOBAL` promotion is irrelevant here.
The sole blocker is **dependency-DLL discovery**: `mod_spatialite.dll` → `libgeos_c`, `libproj_9_2`,
`librttopo`, `libfreexl`, `libiconv`, `libminizip`, `libxml2`, `zlib1`, which chain to `libgeos`,
`libstdc++`, `libgcc_s_seh`, `libwinpthread`, `libcurl`, `libsqlite3-0` (PROJ's own SQLite, unrelated to
the engine), `libtiff`, … — **all in `runtimes/win-x64/native/`**, a folder on neither the app base dir
nor `PATH`. So a plain `LoadLibraryW("mod_spatialite")` fails with **error 126** (dependent module not
found). **Fix (SqliteConnection.cs):** `EnableSpatialite` now branches by OS — Unix keeps
`PromoteSqliteSymbolsForSpatialite`; Windows calls `PreloadSpatialiteWindows`, which locates
`mod_spatialite.dll` (`LocateSpatialiteWindows`: honours an explicit `SpatialiteLibrary` path, else probes
`{BaseDir}\mod_spatialite.dll` and `{BaseDir}\runtimes\win-{x64|x86|arm64}\native\mod_spatialite.dll`) and
pre-loads it by **absolute path with `LoadLibraryEx(..., LOAD_WITH_ALTERED_SEARCH_PATH)`** so the loader
resolves the whole dependency graph from the module's own directory. `LoadExtension` is then given that
**full path** (SQLite reuses the already-loaded module and just runs the entry point; entry-point
derivation from the base name is unchanged → `sqlite3_modspatialite_init`, same as Unix). **Geo suite 57
green on BOTH Linux and Windows** (`dotnet.exe test`, 2026-07-20) — the two `GeometrySpatialiteBehaviourTest`
cases (which load `mod_spatialite` natively, create a geometry table + spatial index, and ALTER-add) now
pass on Windows; they were the only Windows failures. No skips.

## Test status

- Geo: **39 green** (deep DDL-generation for 4 drivers via public `*LanguageSpecifics`; MySQL Z/M throw;
  MSSQL index+bbox+throw via public `MssqlTableDdlBuilder`; capability gate via `DummySqlConnection`;
  live SQLite+SpatiaLite create → column + `idx_geo_sl_shape` R-tree). JSON 84 green (no regression).
- Run: `dotnet test Gehtsoft.EF.Test/Gehtsoft.EF.Test.csproj -c Debug --filter "FullyQualifiedName~Gehtsoft.EF.Test.Geo"`
- SpatiaLite behavioural test skips if the native lib is absent; on this box it runs (Spatialite.Native + the fix).

## Test data

`Gehtsoft.EF.Test/GeoTestData/` (LFS: `.gitattributes` marks `*.wkb`/`*.wkt`/`*.csv`), NOT embedded.
Located at runtime by `Gehtsoft.EF.Test/Geo/GeoTestData.cs` (probes next-to-assembly + up to 5 parents;
LFS-pointer guard). Holds `test.wkt`/`test.wkb` (+ `tiger-line/`, `usa/`, `mars-i-2294/`).

## Git / commit state

- Branch `geo`. **Phase 4 + the Windows SpatiaLite loader committed 2026-07-20 as `965fdcc`**
  (`geo: Phase 4 pure-SQL query surface + Windows SpatiaLite loader`, 30 files). Working tree clean.
  **NOT pushed** (user hasn't asked). The SKILL.md updates + xunit-v3-migration skill removal committed
  separately as `5b0e6a3` (`skill: expand gehtsoft-ef guide; retire xunit-v3-migration skill`).
- Earlier: Phase 3 + MariaDB/MySQL 8 dialects `4c666c3`, NuGet bumps `b7f36a8`; Phases 0-2 `533f8b6`;
  retired in-house Phase-0 (`78130f2`, `f4c99f1`, `b815ed9`) + Gate-1 plan (`8baa33a`).
- New module `Gehtsoft.EF.Geo.NetTopologySuite` added to `Gehtsoft.EF.sln` + `nuget/config.xml`.
- **LFS is scoped:** the LFS rules live in `Gehtsoft.EF.Test/GeoTestData/.gitattributes` (only `*.wkb`/
  `*.wkt`/`*.csv` **under that dir**), so the Northwind sample `csv/*.csv` are NOT converted. There is no
  repo-wide `.gitattributes`. `git lfs install --local` has been run.
- **Do NOT commit unless the user asks.** `version.proj` must stay untouched.

## Build / verify commands

- Full build: `dotnet build Gehtsoft.EF.sln -c Debug -v q`
- Geo tests: filter `~Gehtsoft.EF.Test.Geo`. Drivers other than SQLite need local config
  (`SqlConnectionSources`); SQLite+SpatiaLite runs here.

## ⚠ PIVOT 2026-07-14 — geo PARKED behind the Schema Catalogue initiative

Planning geo Phase 3 (TableUpdate) surfaced that spatial-index reconciliation can't ride introspection
cleanly (spatial indexes hide in per-driver catalogs: MSSQL `sys.spatial_indexes`, SpatiaLite virtual
R-tree invisible to `PRAGMA index_list`, Oracle `ITYP_NAME`, …). User decided the deeper fix is a
**declared-state Schema Catalogue** (EF-owned tables recording schema as declared) that replaces
introspection-based reconciliation framework-wide. **Decision: "catalogue first, geo rides it."** Geo is
parked at `533f8b6`; resume geo Phase 3 AFTER the catalogue lands (spatial add/drop then become plain
diff entries — no per-driver spatial catalog reads). Design doc: `CLAUDE/SCHEMA_CATALOGUE/DESIGN.md`
(Gate-1 pending). `CLAUDE/GEO/PHASE_3/PHASE_3_PLAN.md` holds the introspection-based plan — SUPERSEDED
by the catalogue for the reconcile half; the geo-column add + geo-column drop (user wants drop included)
still apply, expressed through the catalogue diff. Also note: user wants geo-column **drop** in Phase 3.

## Next steps (need a Gate)

- **Schema Catalogue (NEW, first):** approve `CLAUDE/SCHEMA_CATALOGUE/DESIGN.md` (Gate 1), then phase it.
- **Phase 3 — TableUpdate (geo), AFTER the catalogue:** geo column add/**drop** + spatial-index add/drop
  as catalogue-diff entries. (Old introspection plan in PHASE_3_PLAN.md is superseded for reconcile.)
- **Phase 4 — pure-SQL query surface** (the ★ phase): `Geo*` `SqlFunctionId` renderers + arg-channel fix,
  insert/update WKB value-wrap, WHERE predicates/measure/within-distance, select output-wrap + WKB decode,
  projection + scalar order/group/agg. Oracle `Crosses` throws.
- Open follow-ups: **Windows SpatiaLite** load path now IMPLEMENTED **and verified on Windows** (57 geo
  green; dependency-DLL preload via `LoadLibraryEx`+`LOAD_WITH_ALTERED_SEARCH_PATH`; see the
  SpatiaLite-native-fix section); official `mod_spatialite` NuGet (optional; system lib +
  `Spatialite.Native` both work); Phase 8 docs (prerequisites, SRID, limitations).
