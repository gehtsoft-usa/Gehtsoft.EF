# Geospatial (geo) Support — Implementation Plan (draft, pre-approval)

*Planned 2026-07-08. Driver-support analysis + verified codebase seams + full per-driver SQL map:
`GEO_COMMON_FUNCTIONALITY.md` (esp. Appendix A/B). Implementation NOT started. Process mirrors the
EAV/JSON features (`../DYNAMIC_PROPERTIES/`, `../JSON_PROPERTIES/`): this overall plan is approved
first, then a per-phase plan is approved before each phase is coded, and advancing between phases is
a second explicit gate.*

## Context

A **geo property** is an entity member holding a 2-D OGC geometry (Point / LineString / Polygon /
MultiPoint / MultiLineString / MultiPolygon / GeometryCollection) with an integer SRID, stored in a
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
9. **Index-reconciliation ordering** = the general fix (`../INDEX_RECONCILIATION_PROBLEM.md`) is done
   **first, as shared prerequisite work** (unblocks JSON too); geo Phase 3 then only extends it
   (user, 2026-07-08).
11. **Oracle `Crosses`** = **throw "unsupported on Oracle"** (user, 2026-07-08 — "easy way, at this
    stage"). No emulation, no `ST_GEOMETRY` wrapping in v1. All 8 predicates elsewhere, 7 on Oracle.
    Mirrors the `RuleExecutionSide` "throw on untranslatable" precedent. A future `ST_Crosses`
    upgrade stays possible but is **not committed** and not part of this feature.
12. **Out of scope (this feature):** mass **update** of geo fields; the `geography` type; 3-D / M
   coordinates; and — because they act on the **geometry value itself**, not a scalar — **spatial
   aggregates** (ST_Union/ST_Collect/ST_Extent, geoprocessing/partly Oracle-licensed), plus
   **ORDER BY / GROUP BY on a raw geometry value** (not meaningful/portable). *(User, this request.)*

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
- Exposed through the existing seams: WHERE/mass-delete via `ConditionBuilder.Raw(expr).…`;
  projection via `AddExpressionToResultset`; read-back via the resultset decode registry.

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

> **Prerequisite for Phase 3 (decided — done first):** the general index-reconciliation fix
> (`../INDEX_RECONCILIATION_PROBLEM.md`) — today `UpdateTables` reconciles **no** indexes — is landed
> **before** Phase 3 as standalone shared work (also unblocks JSON). Create-time spatial indexing
> (Phase 2) does **not** need it; live-table spatial-index add/drop (Phase 3) extends it.

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
- **Phase 4 — Insert/Update (whole value).** Insert and single-entity update of a geometry via the
  transparent accessor: the insert/update builders **wrap the geo value placeholder in the
  constructor function** (`ST_GeomFromText(@p,srid)` / `geometry::STGeomFromText(@p,srid)` /
  `SDO_GEOMETRY(@p,srid)` …) per driver. **Mass update of geo fields is OUT** (decision 7). Deep:
  generated INSERT/UPDATE SQL (AST) incl. the wrap; null handling. Acceptance: full round-trip of
  each subtype + nullable on five drivers. *(User's step 4.)*
- **Phase 5 — WHERE + mass delete.** The geo function renderers (predicates + measurement +
  within-distance) with per-driver normalization (MSSQL `= 1`, Oracle RELATE `<> 'FALSE'` + mask
  mapping + tolerance). Mass delete with a spatial WHERE (via the entity-WHERE text-splice
  workaround, `../ENTITY_WHERE_PROBLEM.md`, as EAV). **`Crosses` throws "unsupported on Oracle"**
  (decision 11); the other 7 predicates map normally. Deep: predicate/measurement SQL (AST),
  SRID-mismatch → NULL semantics, the Oracle-Crosses throw, mass-delete
  SQL. Acceptance: each predicate × subtype, within-distance, mass delete by spatial filter on five
  drivers. *(User's step 5, incl. mass delete.)*
- **Phase 6 — Select query mapping.** The select builder **wraps geo columns in the output
  function** (`ST_AsBinary`/`STAsBinary`/`TO_WKBGEOMETRY`) so a portable WKB comes back on every
  driver (never the raw column — SpatiaLite BLOB is modified WKB); read-back decodes WKB→geometry via
  the accessor. Select-with-spatial-filter + count. Deep: select output-wrap SQL (AST), decoded
  geometry, count. Acceptance: select + filtered count on five drivers. *(User's step 6.)*
- **Phase 7 — Projection + scalar order-by / group-by / aggregation.** Project a computed scalar
  (`GeoDistance`, `GeoArea`, `GeoLength`, accessors) and/or the geometry itself (as WKB) into the
  resultset via `AddExpressionToResultset` + decode type. Per the "scalar in, geometry-value out"
  principle, also wire the **existing** `AddOrderByExpr` / `AddGroupByExpr` / `AggFn` machinery to a
  geo scalar expression, giving: **order by distance + take N** (nearest-neighbour top-N), **group by
  a geo scalar**, and **numeric aggregation** (`COUNT/SUM/AVG/MIN/MAX`) of a geo scalar (e.g.
  `AVG(GeoArea(col))`). These reuse the cached byte-identical expression (GROUP BY string-match
  caveat). Out: spatial aggregates + ORDER BY/GROUP BY on a raw geometry value (decision 12). Deep:
  projection / order-by / group-by / aggregate SQL (AST) + decoded values + order & grouping
  correctness. Acceptance: projected scalars, order-by-distance top-N, and a grouped `AVG(GeoArea)`
  on five drivers. *(User's step 7 + scalar aggregation.)*
- **Phase 8 — docs.** docgen pages + XML doc comments on all new public API; document the runtime
  prerequisites (PostGIS install, `mod_spatialite`), the SRID discipline, and the v1 limitations
  (no order-by-distance, no mass update, no aggregation, geometry-only).

## Explicitly OUT of scope (v1)

Mass update of geo fields · **spatial aggregates** (ST_Union/ST_Collect/ST_Extent) · **ORDER BY /
GROUP BY on a raw geometry value** · `geography` type · geoprocessing (Buffer/Union/Intersection/
Difference/ConvexHull/Centroid, Oracle-licensed) · 3-D / M coordinates · live-table spatial-index
reconciliation beyond what the general index-reconciliation prerequisite provides.

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
