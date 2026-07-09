# Geospatial (geo) Support — Implementation Plan (APPROVED 2026-07-09)

*Planned 2026-07-08; overall plan **approved by the user 2026-07-09** (Gate 1). Driver-support
analysis + verified codebase seams + full per-driver SQL map: `GEO_COMMON_FUNCTIONALITY.md` (esp.
Appendix A/B). Process mirrors the EAV/JSON features (`../DYNAMIC_PROPERTIES/`,
`../JSON_PROPERTIES/`): this overall plan is approved first (done), then a per-phase plan is approved
before each phase is coded, and advancing between phases is a second explicit gate.*

## Context

A **geo property** is an entity member holding an OGC geometry (Point / LineString / Polygon /
MultiPoint / MultiLineString / MultiPolygon / GeometryCollection; 2-D with **optional Z/M** ordinates
— decision 14) with an integer SRID, stored in a
**native spatial column** on the entity's own table, round-tripped automatically on load/save via
**WKT/WKB**, and usable in **WHERE** (predicates + measurements), **mass delete**, **select**, and
**projection**. Scope: **all five drivers** — MSSQL, Oracle 12+ (Locator), SQLite (SpatiaLite),
PostgreSQL (PostGIS), MySQL 8.0+.

The CLR side is an **in-house minimal geometry type** (no NetTopologySuite). The DB side is each
engine's native type, reached through WKT/WKB so no provider-native geometry parameter types are
needed (see `GEO_COMMON_FUNCTIONALITY.md` §2 bind/read strategy).

## Decisions (confirmed with the user 2026-07-08)

1. **CLR representation** = in-house minimal geometry type, round-tripped as WKT/WKB.
2. **Oracle tier** = Locator (free). Geoprocessing (Buffer/Union/Intersection/Difference/
   ConvexHull/Centroid) is OUT.
3. **Driver scope** = all five (accepting PostGIS server install + SpatiaLite `mod_spatialite` as
   runtime prerequisites).
4. **Spatial indexing IS in v1**, per-driver DDL/create-table builders (create-time). Live-table
   index add/drop reconciliation rides the general index-reconciliation fix (prerequisite, below).
5. **Bounding box is declared** on the spatial-index attribute (`xmin,ymin,xmax,ymax`; + Oracle
   tolerance with a default). Feeds MSSQL `WITH (BOUNDING_BOX=…)` and Oracle
   `USER_SDO_GEOM_METADATA`; ignored by PostGIS/MySQL/SpatiaLite.
6. **Bind/read strategy** = bind **WKB** (`byte[]`) wrapped in the constructor function in SQL
   (`ST_GeomFromWKB`/`STGeomFromWKB`/`SDO_UTIL.FROM_WKBGEOMETRY`); read via the WKB output function
   (`ST_AsBinary`/`STAsBinary`/`TO_WKBGEOMETRY`), never the raw column. **WKB, not WKT** (user,
   2026-07-08): compact + exact double round-trip. The in-house codec still provides WKT for
   debugging/tests, but the DB wire form is WKB.
8. **SRID** = **default 4326** (WGS84 lon/lat), overridable per property (user, 2026-07-08). Satisfies
   MySQL/Oracle indexed-column SRID requirements out of the box; planar/other systems override on the
   attribute. Oracle 4326≡8307 caveat still applies.
9. **Index-reconciliation ordering** = the general fix (`../INDEX_RECONCILIATION_PROBLEM.md`) was done
   **first, as shared prerequisite work** — ✅ **landed 2026-07-08** (unblocked JSON too); geo Phase 3
   only extends it (user, 2026-07-08).
11. **Oracle `Crosses`** = **throw "unsupported on Oracle"** (user, 2026-07-08 — "easy way, at this
    stage"). No emulation, no `ST_GEOMETRY` wrapping in v1. All 8 predicates elsewhere, 7 on Oracle.
    Mirrors the `RuleExecutionSide` "throw on untranslatable" precedent. A future `ST_Crosses`
    upgrade stays possible but is **not committed** and not part of this feature.
12. **Out of scope (this feature):** mass **update** of geo fields; the `geography` type; and —
   because they act on the **geometry value itself**, not a scalar — **spatial aggregates**
   (ST_Union/ST_Collect/ST_Extent, geoprocessing/partly Oracle-licensed), plus **ORDER BY / GROUP BY
   on a raw geometry value** (not meaningful/portable). *(User, this request.)*
14. **Z (3-D) and M (measure) ordinates ARE supported** *(REVISED — user, 2026-07-09: "don't reject Z
   and M, they are useful to store routing data")*. This reverses the earlier "3-D/M out". The CLR
   `GeoCoordinate` carries optional Z and M; the WKT/WKB codec reads and writes all of XY / XYZ / XYM /
   XYZM (WKT ISO `Z`/`M`/`ZM` tags + untagged auto-detect; WKB via the EWKB Z/M flags and ISO type
   offsets). **Implication for later phases:** per-driver column typing must carry dimensionality
   (PostGIS `geometry(PointZM,4326)`, MySQL/Oracle/MSSQL/SpatiaLite equivalents) — a Phase 2 concern;
   spatial *measurement/predicate* ops still operate on the engines' terms (mostly planar XY), with
   Z/M carried through storage/round-trip. Codec support landed in Phase 0.
15. **SRID is carried on codec OUTPUT by default** *(user, 2026-07-09: "keep SRID on output as well")*.
   `ToWkt()`/`ToWkb()` default to EWKT (`SRID=<n>;`) / EWKB (SRID flag) so file/interchange round-trips
   are symmetric with third-party tools. The **plain OGC** form (needed by the DB constructor functions
   `ST_GeomFromWKB`/`STGeomFromWKB`/`SDO_UTIL.FROM_WKBGEOMETRY`, which take the SRID separately) is the
   `includeSrid: false` opt-out — the DB wire path (Phase 4/5) uses that. Contract for the DB layer
   unchanged; only the *default convenience* form now carries SRID.
13. **Runtime spatial extensions are the application's responsibility, opt-in via a driver flag**
    (user, 2026-07-09). The SQLite driver does **not** bundle `mod_spatialite`; the application
    installs the native library. A **driver-level "enable spatial" option** is added:
    - **SQLite:** when enabled, the driver `LoadExtension("mod_spatialite")` on connection open (and
      runs `InitSpatialMetaData` once per DB as needed). Disabled by default → no attempt to load.
      *Open option (decide in Phase 2):* how the native binary is delivered — (a) the app installs it
      OS-wide (baseline), or (b) we depend on / ship a **native NuGet package** that carries
      `mod_spatialite` (e.g. `Spatialite.Native`, cf. github Fsystem/mod_spatialite) or a
      Gehtsoft-built equivalent, and the driver loads it from the package's runtimes path. The
      "enable spatial" flag + `LoadExtension` seam is the same either way; only the library-path
      resolution differs. Also allow the app to specify a custom extension path/name.
    - **PostgreSQL:** when enabled, the driver **verifies the PostGIS extension is installed**
      (fail fast with a clear error if absent) rather than silently producing broken SQL.
    - MSSQL/MySQL/Oracle: spatial is built in / provisioned server-side, so the flag is a
      no-op or a light capability check there.
    Implemented in Phase 2 (driver wiring) alongside the create-table path; documented in Phase 8.
    **Prerequisites to document (Phase 8):** PostGIS server install (`CREATE EXTENSION postgis`);
    SpatiaLite native binary — Linux `sudo apt-get install libsqlite3-mod-spatialite`, macOS
    `brew install spatialite-tools`, Windows: `mod_spatialite` DLL on the library path.

### Guiding principle — "scalar in, geometry-value out" (user, 2026-07-08)
The line between in/out scope is **what the operation acts on**:
- **Operations on a geo *scalar expression*** — `GeoDistance`/`GeoArea`/`GeoLength`/accessors all
  produce an ordinary `double`/number. ORDER BY, GROUP BY, and numeric aggregation
  (`COUNT/SUM/AVG/MIN/MAX`) over such a scalar reuse the existing scalar query machinery
  (`AddOrderByExpr`, `AddGroupByExpr`, `AggFn`) with **nothing geo-specific** in the clause. → **IN v1.**
  - "Order by distance, take N" is `ORDER BY GeoDistance(col,@p)` + the existing result-limit →
    **nearest-neighbour top-N IS supported** (full-scan sort; not index-accelerated on Oracle, which
    needs the `SDO_NN` operator — a possible future add).
  - `AVG(GeoArea(col))`, `MAX(GeoLength(col))`, grouping rows by a geo scalar → supported.
- **Operations on the *geometry value itself*** — spatial aggregates (`ST_Union`/`ST_Collect`/
  `ST_Extent`) and ORDER BY / GROUP BY on a raw geometry are geoprocessing or non-portable/not
  meaningful. → **OUT v1.**

Caveat (from the JSON analysis): `BuildGroupBy` matches on the **exact expression string**, so a geo
scalar used in GROUP BY must be the byte-identical cached expression everywhere it appears.

## Column / storage shape

- One **native spatial column** per geo property on the owner entity's own table (no side table):
  Postgres `geometry(<subtype>,<srid>)`, MySQL `<SUBTYPE> [NOT NULL] SRID <srid>`, SQL Server
  `geometry`, Oracle `SDO_GEOMETRY`, SpatiaLite added post-create via `AddGeometryColumn`.
- SRID defaults to **4326** (decision 8), overridable per property; embedded in the column type on
  Postgres/MySQL and in the constructor calls / `USER_SDO_GEOM_METADATA` elsewhere.
- `DbType` (the fixed BCL enum) cannot name a geo type. **New descriptor channel:** `ColumnInfo`
  gains an optional geometry descriptor (subtype + SRID + not-null-when-indexed); `TableDdlBuilder`
  branches on it and calls a **new per-driver `GeometryTypeName(subtype, srid)`** hook instead of
  `TypeName(DbType,…)`. This is the geo analogue of the `TypeName` switch and the one structural
  addition to the type system.
- CLR `null` ⇔ SQL `NULL`. A geo column that carries a spatial index on MySQL must be `NOT NULL`
  (enforced by the declaration; see Phase 1).

## Query translation

- New `SqlFunctionId` members: `GeoFromText`, `GeoAsText`, `GeoAsBinary`, `GeoDistance`, `GeoArea`,
  `GeoLength`, `GeoIntersects`, `GeoDisjoint`, `GeoEquals`, `GeoTouches`, `GeoWithin`, `GeoContains`,
  `GeoOverlaps`, `GeoCrosses`, `GeoSrid`, `GeoGeometryType`, `GeoIsEmpty`, `GeoX`, `GeoY`,
  `GeoEnvelope`, `GeoDWithin`. Rendered per driver in `GetSqlFunction` exactly per
  `GEO_COMMON_FUNCTIONALITY.md` Appendix A.
- **The arg-channel problem** (analysis §"codebase fit"): today `GetSqlFunction(id, string[] args)`
  assumes free-function syntax. Geo needs (a) **SQL Server receiver-method** shape
  (`receiver.STDistance(arg)`), (b) **Oracle `SDO_GEOM.RELATE(a,'mask',b,tol)` + `<> 'FALSE'`**
  wrapping and the tolerance arg, (c) **SQL Server `bit` → `= 1`** wrapping. Resolve by extending
  the geo function rendering to own its full expression (the renderer returns the complete
  boolean/scalar SQL, including the comparison), so predicate normalization lives in one place.
- A canonical, **cached** expression string per (column, operation) so any reuse is byte-identical
  (matches the EAV `DynamicPropertyJoin.ColumnAlias` discipline; matters even without GROUP BY for
  predicate/projection consistency).
- **SQL-builder layer first, entity queries delegate to it (as JSON did).** The whole geo query
  surface — value-wrapping, predicates/measurements, select output-wrapping, projection, scalar
  order/group/aggregation — is built and tested at the pure-SQL builder level (`ConditionBuilder`,
  `SelectQueryBuilder`, the Insert/Update/Delete builders + binders) operating on a `TableDescriptor`,
  with **no entity queries** (Phase 4). The entity-query layer (Phases 5–7) then adds thin wrappers
  that resolve the column and delegate to these SQL-builder primitives — exactly the
  `SelectEntitiesQueryBase.AddJsonValueToResultset` → `SelectQueryBuilder.AddJsonValueToResultset` and
  `EntityQueryConditionBuilder.JsonPropertyOf` → `ConditionBuilder.JsonValue` relationship.
- **Scalar guard (learned from JSON):** a geo expression can carry a quoted string literal — notably
  Oracle's `SDO_GEOM.RELATE(a,'mask',b,tol)` — which the `SqlInjectionProtectionPolicy` scalar guard
  rejects. So geo predicates/projections/order/group must be added through **dedicated entry points
  that set the operand / add to the builder collections directly, bypassing the guard** (as JSON's
  `JsonValue` and `AddRawJsonExpression*` do), never the generic guarded `Raw` /
  `AddExpressionToResultset` / `AddOrderByExpr` / `AddGroupByExpr`. Read-back via the resultset decode
  registry (WKB→geometry; the geo analogue of the JSON `bool`/`DateTime` decode registry).

## Testing model (two tiers, mirrors EAV/JSON)

- **Deep / white-box tier.**
  - **Codec (Phase 0):** pure-.NET unit tests — WKT⇄geometry⇄WKB round-trip for every subtype,
    SRID, precision, empty geometry, malformed input. No DB needed.
  - **SQL generation:** assert the **exact generated SQL parsed to AST** (`.ParseSql()`, never string
    `Contains`) for column DDL, index DDL, insert/update value-wrapping, WHERE predicates, select
    output-wrapping, projections. No live DB needed → runs everywhere.
  - **SQLite+SpatiaLite behavioural:** column/index existence via `DoesObjectExist(...)`; value
    round-trip — **requires `mod_spatialite` on the test machine** (documented test prerequisite).
- **Acceptance tier — all five drivers.** `[Theory][MemberData(nameof(ConnectionNames))]` +
  `IClassFixture<SqlConnectionFixtureBase>`; SQLite(+SpatiaLite) always on, others per local config.
  Assert observable behaviour (geometry equality within tolerance, counts, distances), not SQL text.
- Tests under `Gehtsoft.EF.Test/Geo/{Geometry,Entities,TableManagement,DataManagement,DataSelecting}`
  (test csproj uses default compile items — no `<Compile Include>` needed).

## Delivery — phases (finish-before-advance; each phase planned in `PHASE_N/` then approved)

> **Ordering (user, this request):** the whole geo **query surface is delivered at the pure-SQL
> builder level first (Phase 4) — before any entity data queries (Phases 5–7), which delegate to it —
> mirroring how JSON ended up (a pure-SQL layer under thin entity wrappers). Declare/discovery
> (Phase 1) stays ahead of Phase 4 only so the pure-SQL phase can obtain a geometry-carrying
> `TableDescriptor`; it installs the accessor/descriptor and is not itself a query. Table
> create/update (Phases 2–3) are already SQL-builder-level DDL.

> **Prerequisite for Phase 3 — ✅ DONE (2026-07-08):** the general index-reconciliation fix
> (`../INDEX_RECONCILIATION_PROBLEM.md`) has landed as standalone shared work (all 3 stages, full
> suite green; also unblocked JSON). `UpdateTables` now reconciles indexes via `GetTableIndexes` +
> `CompositeIndex.ExcludeFor`. Create-time spatial indexing (Phase 2) doesn't need it; live-table
> spatial-index add/drop (Phase 3) extends this shared reconciler.

- **Phase 0 — Foundation: geometry type + WKT/WKB codec.** The in-house geometry CLR type (7 OGC
  subtypes, 2-D coordinates, SRID) and a WKT reader/writer + WKB reader/writer. Pure .NET, no DB
  dependency, exhaustively unit-tested. *(Enabling half of the user's step 1.)*
- **Phase 1 — Declare (entity-level).** `[GeometryProperty]` (SRID, subtype, nullable,
  not-null-when-indexed) and `[SpatialIndex]` (bounding box, tolerance, name; repeatable) attributes;
  `GeometryPropertyAccessor` (decorating accessor presenting **WKB `byte[]`**, `PropertyType` =
  `byte[]`) so binders/`SqlLanguageSpecifics` need no change;
  `ColumnDiscoverer` interception installing the geo descriptor + accessor; `EntityDescriptor`
  recognition + declared spatial-index list. Deep: attribute parsing, accessor WKT/WKB round-trip
  against Phase 0. *(User's step 1 — entity-level support.)*
- **Phase 2 — Table create (column + spatial index).** Per-driver `GeometryTypeName` + geo column
  DDL on all five; the index-field model extended with a **spatial-kind + bbox/tolerance** channel;
  per-driver spatial-index emission at CREATE TABLE, including the **non-DDL sequenced steps**:
  SpatiaLite `InitSpatialMetaData` (once) + `AddGeometryColumn` + `CreateSpatialIndex`; Oracle
  `USER_SDO_GEOM_METADATA` INSERT before `CREATE INDEX`; MySQL `NOT NULL SRID` column; MSSQL
  `BOUNDING_BOX`. Deep: column present + each declared index present (`DoesObjectExist`), DDL AST.
  Acceptance: create/drop across five drivers. *(User's step 2.)*
- **Phase 3 — TableUpdate** *(depends on the PREREQUISITE fix)*. Add a geo column to an existing
  table (SpatiaLite/Oracle need the post-create registration path, not plain `ALTER … ADD`), and
  reconcile spatial indexes (add missing / drop removed) via the general reconciler extended for the
  spatial-kind field. Deep: added column/index created; removed index dropped; unchanged → no-op;
  non-geo indexes untouched. Acceptance: add/remove a geo column + spatial index across five drivers.
  *(User's step 3.)*
- **Phase 4 — Pure-SQL query surface (the geo query level — BEFORE any entity queries).** ★ The
  entire geo query machinery, built and tested at the **SQL-builder layer only** — operating on a
  `TableDescriptor` that carries a geometry column via `InsertQueryBuilder` / `UpdateQueryBuilder` /
  `DeleteQueryBuilder` / `SelectQueryBuilder` / `ConditionBuilder` + the binders, with **no entity
  queries** — exactly as JSON's pure-SQL surface (`JsonPureSqlTest` / `JsonPureSqlProjectionTest`).
  Scope:
  - the geo `SqlFunctionId` renderers + the **arg-channel fix** (R1): receiver-method shape for MSSQL,
    mask+tolerance for Oracle, and the renderer owning its full boolean/scalar result (incl. the
    MSSQL `= 1` / Oracle RELATE `<> 'FALSE'` comparison), so predicate normalization lives in one place;
  - **insert/update value-wrapping** — the placeholder wrapped in the constructor function
    (`ST_GeomFromWKB(@p,srid)` / `geometry::STGeomFromWKB(@p,srid)` / `SDO_UTIL.FROM_WKBGEOMETRY(@p)` …),
    WKB bound as `byte[]`; **mass update OUT** (decision 12);
  - **WHERE predicates + measurements + within-distance** via a dedicated `ConditionBuilder` geo entry
    point (analogous to `JsonValue`), plus **mass delete** with a spatial WHERE; **`Crosses` throws
    "unsupported on Oracle"** (decision 11), the other 7 predicates map normally;
  - **select output-wrapping** (`ST_AsBinary` / `STAsBinary` / `TO_WKBGEOMETRY`) so portable WKB comes
    back on every driver (never the raw column — SpatiaLite BLOB is modified WKB) + read-back
    WKB→geometry decode registry, and filtered **count**;
  - **projection** of a geo scalar (`GeoDistance`/`GeoArea`/`GeoLength`/accessors) or the geometry (as
    WKB), plus **scalar ORDER BY / GROUP BY / numeric aggregation** over a cached byte-identical
    geo-scalar expression — order-by-distance top-N, group-by a geo scalar, `AVG(GeoArea(col))` — via
    the existing `AddOrderByExpr` / `AddGroupByExpr` / `AggFn` machinery ("scalar in, geometry-value
    out"; out: spatial aggregates + order/group on a raw geometry, decision 12).
  All geo expressions are added through **direct-add entry points that bypass the scalar guard** (the
  Oracle RELATE mask is a quoted literal — see Query translation). Deep: every value-wrap / predicate /
  measurement / output-wrap / projection / order / group SQL asserted via AST; SQLite+SpatiaLite
  behavioural round-trip. *(This is the user's requested pure-SQL query step; the entity phases below
  delegate to it.)*
- **Phase 5 — Entity insert/update (whole value).** Entity `GetInsertEntityQuery` / single-entity
  update round-trip the geometry through the Phase-4 value-wrap, using the `GeometryPropertyAccessor` +
  geo descriptor that Phase 1 (declare) already installed via `ColumnDiscoverer`. The entity layer here
  is a thin consumer of the Phase-1 discovery and the Phase-4 SQL wrapping — no new query mechanics.
  Deep: entity INSERT/UPDATE SQL (AST); null handling. Acceptance: full round-trip of each subtype +
  nullable on five drivers. *(User's step 4, entity-level.)*
- **Phase 6 — Entity WHERE + mass delete + select + count.** Entity condition-builder geo predicates
  (`GeoIntersects`/… analogous to `JsonPropertyOf`, string + member-expression forms) delegating to the
  Phase-4 renderers; entity select (whole geometry via the output-wrap) + filtered count; entity mass
  delete by spatial filter (entity-WHERE text-splice workaround, `../ENTITY_WHERE_PROBLEM.md`, as EAV).
  `Crosses` throws on Oracle. Acceptance: each predicate × subtype, within-distance, select + count +
  mass delete on five drivers. *(User's steps 5 & 6, entity-level.)*
- **Phase 7 — Entity projection + scalar order-by / group-by / aggregation.** `SelectEntitiesQueryBase`
  geo wrappers delegating to Phase 4: project a geo scalar / the geometry; **order-by-distance +
  take-N** (nearest-neighbour top-N); **group by a geo scalar**; **numeric aggregation**
  (`AVG(GeoArea(col))` …). Acceptance: projected scalars, order-by-distance top-N, and a grouped
  `AVG(GeoArea)` on five drivers. *(User's step 7 entity-level + scalar aggregation.)*
- **Phase 8 — docs.** docgen pages + XML doc comments on all new public API; document the runtime
  prerequisites (PostGIS install, `mod_spatialite`), the SRID discipline, and the v1 limitations
  (no mass update, no spatial aggregates, no order/group on a raw geometry, geometry-only).

## Explicitly OUT of scope (v1)

Mass update of geo fields · **spatial aggregates** (ST_Union/ST_Collect/ST_Extent) · **ORDER BY /
GROUP BY on a raw geometry value** · `geography` type · geoprocessing (Buffer/Union/Intersection/
Difference/ConvexHull/Centroid, Oracle-licensed) · live-table spatial-index reconciliation beyond
what the general index-reconciliation prerequisite provides.

*(No longer out of scope — decision 14: **Z/M ordinates** are supported at the codec/storage level;
decision 15: SRID is carried on codec output by default.)*

*(NOT out of scope — per the "scalar in, geometry-value out" principle: ORDER BY, GROUP BY, and
numeric aggregation `COUNT/SUM/AVG/MIN/MAX` over a geo **scalar** expression — incl. order-by-distance
+ take-N and `AVG(GeoArea(col))` — are all IN v1 via the existing scalar query machinery.)*

## Constraints / conventions (same as EAV/JSON)

- Product projects `Gehtsoft.EF.Entities.csproj` and `Gehtsoft.EF.Db.SqlDb.csproj` use
  `EnableDefaultCompileItems=false` — **every new product .cs needs an explicit `<Compile Include>`**.
  (Content-only files are never a build change; product source files are.)
- Tests assert INTENDED behaviour; product bugs → a `KNOWN_ISSUES.md` in this folder, tests never
  adapted. Test SQL via AST (`.ParseSql()`), never string `Contains`; prefer behavioural checks
  (`DoesObjectExist`, value round-trip).
- `ArgumentNullException.ThrowIfNull(x, nameof(x))`; never `replace_all` for constant extraction.
- **No LINQ** in product or test code (explicit loops, eager/O(1)).
- Commit `version.proj` bumps together with feature commits.

## Open risks to close during phase planning

- **R1 — arg channel:** the `GetSqlFunction` shape must carry receiver-method (MSSQL), mask+tolerance
  (Oracle), and own the result-comparison. Prototype the renderer contract in Phase 5's plan (and
  the read/insert wrappers in Phases 4/6) before coding.
- **R2 — Oracle `Crosses` (RESOLVED, decision 11):** no `SDO_GEOM.RELATE` mask reproduces OGC
  `Crosses` (root cause: Oracle's relationship keyword set has no "crosses"; the other 4 drivers ship
  a native `ST_Crosses`). v1 **throws "unsupported on Oracle"** — no emulation, no `ST_GEOMETRY`
  wrapping. Rejected alternatives on record: mask-combo emulation (wrong results); SQL/MM
  `ST_GEOMETRY.ST_Crosses` (unverified Locator-legality + possible full-scan; a *possible future*
  upgrade, not committed).
- **R3 — SpatiaLite test infra:** `mod_spatialite` must be present for behavioural tests; confirm the
  CI/local library-path story in Phase 0/2. AST tests stay DB-free as the fallback signal.
- **R4 — SpatiaLite post-create column model:** the geo column is added *after* CREATE TABLE
  (`AddGeometryColumn` + triggers), not inline — verify it fits `TableDdlBuilder.HandleAfterQuery`
  cleanly and that DoesObjectExist/introspection still sees it (Phase 2).
- **R5 — Oracle SRID 4326 vs 8307:** standardize on 4326; some geodetic ops/metadata may surface
  8307 — assert tolerantly and document (Phase 2/5).
- **R6 — index-expression match:** where a spatial index must be *used* by a within-distance query,
  the query expression must line up with the index (esp. Oracle operators need the index) — confirm
  the indexed path in Phase 5.
- **R7 — Phase 3 prerequisite (resolved):** the general index-reconciliation fix lands first as
  shared work (decision 9), then Phase 3 extends it. Sequenced accordingly.
