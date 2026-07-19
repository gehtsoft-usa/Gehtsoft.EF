# GEO feature — current state (resume point)

*Snapshot updated 2026-07-14. Read this first when resuming. Branch `geo`. Companion docs: `GEO_PLAN.md`
(overall, has the ARCHITECTURE REVISION banner), `GEO_COMMON_FUNCTIONALITY.md` (per-driver SQL map),
`PHASE_0/`, `PHASE_1/`, `PHASE_2/`, `PHASE_3/` plans, and **`PREREQUISITES_STATE.md`** (the Schema
Catalogue + locking work geo now waits on).*

## TL;DR

Phases **0, 1, 2 are implemented, green, and committed** (39 geo tests + 84 JSON tests, full solution 0
new warnings). Return point = **`533f8b6`** (`geo Phases 0-2: codec-agnostic WKB core, declare,
table-create (5 drivers)`).

**GEO IS PARKED (2026-07-14).** Planning Phase 3 (TableUpdate) exposed that spatial-index reconciliation
can't ride DB introspection cleanly (spatial indexes hide in per-driver catalogs). Decision: build a
**declared-state Schema Catalogue** first — "catalogue first, geo rides it." Geo resumes at Phase 3 once
the catalogue lands. **All current progress is the two prerequisite tasks + the catalogue design — see
`PREREQUISITES_STATE.md`.** Process = two human gates per phase; **commit only when the user asks**.

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
- Open follow-ups: **Windows SpatiaLite** verification; official `mod_spatialite` NuGet (optional; system
  lib + `Spatialite.Native` both work); Phase 8 docs (prerequisites, SRID, limitations).
