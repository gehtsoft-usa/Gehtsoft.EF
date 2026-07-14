# GEO feature — current state (resume point)

*Snapshot 2026-07-09. Read this first when resuming. Branch `geo`. Companion docs: `GEO_PLAN.md`
(overall, has the ARCHITECTURE REVISION banner), `GEO_COMMON_FUNCTIONALITY.md` (per-driver SQL map),
`PHASE_0/`, `PHASE_1/`, `PHASE_2/` plans.*

## TL;DR

Phases **0, 1, 2 are implemented, green, and committed** (39 geo tests + 84 JSON tests, full solution 0
new warnings). Return point = **`533f8b6`** (`geo Phases 0-2: codec-agnostic WKB core, declare,
table-create (5 drivers)`); working tree clean. Process = two human gates per phase (plan-then-approve,
advance-then-approve); **commit only when the user asks**. Next: **Gate for Phase 3 or Phase 4**.

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

- Branch `geo`. Phases 0-2 committed as **`533f8b6`** (the return point); working tree clean. Earlier
  commits: retired in-house Phase-0 (`78130f2`, `f4c99f1`, `b815ed9`) + Gate-1 plan (`8baa33a`).
- New module `Gehtsoft.EF.Geo.NetTopologySuite` added to `Gehtsoft.EF.sln` + `nuget/config.xml`.
- **LFS is scoped:** the LFS rules live in `Gehtsoft.EF.Test/GeoTestData/.gitattributes` (only `*.wkb`/
  `*.wkt`/`*.csv` **under that dir**), so the Northwind sample `csv/*.csv` are NOT converted. There is no
  repo-wide `.gitattributes`. `git lfs install --local` has been run.
- **Do NOT commit unless the user asks.** `version.proj` must stay untouched.

## Build / verify commands

- Full build: `dotnet build Gehtsoft.EF.sln -c Debug -v q`
- Geo tests: filter `~Gehtsoft.EF.Test.Geo`. Drivers other than SQLite need local config
  (`SqlConnectionSources`); SQLite+SpatiaLite runs here.

## Next steps (need a Gate)

- **Phase 3 — TableUpdate:** reconcile geo columns + spatial indexes on a live table (rides the general
  index-reconciliation fix; SD2 — introduce `CompositeIndex.ForSpatial`/`GetTableIndexes` surfacing).
- **Phase 4 — pure-SQL query surface** (the ★ phase): `Geo*` `SqlFunctionId` renderers + arg-channel fix,
  insert/update WKB value-wrap, WHERE predicates/measure/within-distance, select output-wrap + WKB decode,
  projection + scalar order/group/agg. Oracle `Crosses` throws.
- Open follow-ups: **Windows SpatiaLite** verification; official `mod_spatialite` NuGet (optional; system
  lib + `Spatialite.Native` both work); Phase 8 docs (prerequisites, SRID, limitations).
