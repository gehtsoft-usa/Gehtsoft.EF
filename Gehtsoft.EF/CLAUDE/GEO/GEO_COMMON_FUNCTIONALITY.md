# Geospatial support — minimum common functionality across the five drivers

*Research performed 2026-07-08. This is the **step-1 "understand"** document: it establishes
what spatial functionality is genuinely portable across all five supported engines, before any
plan is written. Companion analogs: `../DYNAMIC_PROPERTIES/` (EAV) and `../JSON_PROPERTIES/`.
**No decisions are final until the user confirms them** (see "Proposed scope" at the end).*

## Goal of this step

The user wants a `geo` column type on Gehtsoft.EF entities, working across **MSSQL, Oracle 12+,
SQLite, PostgreSQL, and MySQL**. Before designing anything, we need the *intersection* of what
these five engines can do — the largest feature set we can expose uniformly without per-driver
carve-outs. Everything above that line becomes optional / driver-specific / out of scope.

## The five engines at a glance

| Engine | Spatial availability | Column type(s) | Function syntax | Free? |
|---|---|---|---|---|
| **PostgreSQL** | **PostGIS extension** (`CREATE EXTENSION postgis`) — not in core | `geometry`, `geography` | OGC `ST_*` free functions | Yes (OSS) |
| **SQL Server** | **Built in** (all editions incl. Express) | `geometry`, `geography` (CLR UDTs) | **method-call**: `@g.STDistance(@h)`, `geometry::STGeomFromText(...)` | Yes |
| **MySQL** | **Built in** (5.7+; InnoDB spatial index since 5.7) | `geometry` hierarchy only (SRID attribute) | OGC `ST_*` free functions | Yes |
| **SQLite** | **SpatiaLite extension** (`mod_spatialite`) — not in core, must be loaded at runtime | `geometry` (BLOB-backed) | OGC `ST_*` free functions | Yes (OSS) |
| **Oracle 12+** | **Oracle Locator** (free, in every edition) or **Oracle Spatial** (licensed) | `SDO_GEOMETRY` (single type; geodetic vs projected by SRID) | **`SDO_*` package calls** + `SDO_GEOM.*`; optional `ST_GEOMETRY`/`ST_*` (ORDSYS, same licensing) | Locator free; geoprocessing licensed |

Two facts dominate the whole design:

1. **Two engines need an extension that is not present by default and must be provisioned/loaded**
   — PostGIS (server-side install) and SpatiaLite (a native `mod_spatialite` DLL/so that
   `Microsoft.Data.Sqlite` must `LoadExtension`, with the OS library path set up correctly). Our
   SQLite driver uses `Microsoft.Data.SQLite`, which *does* support `LoadExtension`, so this is
   feasible — but it is a runtime prerequisite, not a given.
2. **Three different function-call grammars.** PostGIS / MySQL / SpatiaLite share OGC `ST_*`
   free-function syntax. **SQL Server uses method-call syntax on a CLR UDT.** **Oracle uses
   `SDO_*` package procedures + relate operators.** A single SQL template can never cover all
   three — each driver must render its own spatial SQL. This maps cleanly onto the existing
   per-driver `GetSqlFunction` override model (see the codebase-fit section).

## Where the "minimum" line actually falls

### 1. Type model — planar `geometry` only, with an SRID

- **OGC geometry hierarchy is universal:** `Point`, `LineString`, `Polygon`, `MultiPoint`,
  `MultiLineString`, `MultiPolygon`, `GeometryCollection`. All five support all seven. ✅ common.
- **A distinct `geography` (geodetic/ellipsoidal) type exists on only 2 of 5** — SQL Server and
  PostGIS. MySQL, SQLite/SpatiaLite, and Oracle model geodetic-vs-planar through the **SRID** of a
  single geometry type, not a separate type. → **`geography` as a first-class type is NOT in the
  common set.** Geodetic behaviour (e.g. metres-on-a-sphere distance with SRID 4326) is achievable
  everywhere but through different means; keep it out of the minimum surface.
- **SRID is universal** as an integer attribute of the value. Defaults and strictness differ
  (MySQL defaults SRID 0 and refuses mixed-SRID operations; SQL Server needs matching SRIDs;
  PostGIS enforces via `Find_SRID`/typmod). → carry SRID explicitly on every value.
- **2D only** in the common set. Z (3D) and M (measure) coordinates have uneven support and
  semantics. → minimum = X,Y.

### 2. Interchange format — WKT + WKB (the lingua franca)

Every engine can import from and export to **Well-Known Text (WKT)** and **Well-Known Binary
(WKB)**. This is the portable wire format and the natural boundary between .NET and each dialect —
exactly the "native on one driver, encode/decode on the others" pattern the codebase already uses
for `Guid`.

| Op | PostGIS / MySQL / SpatiaLite | SQL Server | Oracle |
|---|---|---|---|
| WKT → geom | `ST_GeomFromText(wkt, srid)` | `geometry::STGeomFromText(wkt, srid)` | `SDO_GEOMETRY(wkt, srid)` / `SDO_UTIL.FROM_WKTGEOMETRY` |
| WKB → geom | `ST_GeomFromWKB(wkb, srid)` | `geometry::STGeomFromWKB(wkb, srid)` | `SDO_UTIL.FROM_WKBGEOMETRY` |
| geom → WKT | `ST_AsText(g)` | `g.STAsText()` | `SDO_UTIL.TO_WKTGEOMETRY(g)` |
| geom → WKB | `ST_AsBinary(g)` | `g.STAsBinary()` | `SDO_UTIL.TO_WKBGEOMETRY(g)` |

On the .NET side the CLR representation is an **in-house minimal geometry type** (decided — see
Confirmed decisions), round-tripped purely as WKT/WKB. No NetTopologySuite dependency.

**Chosen bind/read strategy (avoids provider-native geometry types entirely):**
- **Write:** bind the value as a **WKT `string`** (or WKB `byte[]`) ordinary parameter, and wrap it
  in the driver's constructor function *in the generated SQL* (`ST_GeomFromText(@p, srid)` /
  `geometry::STGeomFromText(@p, srid)` / `SDO_GEOMETRY(@p, srid)`). No `NpgsqlDbType.Geometry` /
  `SqlDbType.Udt` needed — every provider binds string/bytes natively, so `BindParameterToQuery`
  needs no per-driver override for geo.
- **Read:** never `SELECT` the raw geometry column — each provider returns a *different* native
  object (and SpatiaLite's stored BLOB is a **modified**, non-standard WKB). Instead the SELECT
  builder must wrap the column in the **output function** (`ST_AsBinary(col)` / `col.STAsBinary()` /
  `SDO_UTIL.TO_WKBGEOMETRY(col)`) so a portable WKB `byte[]` (or WKT string) comes back on every
  driver. This is a hard requirement, not an optimization — see Appendix B.

### 3. Operations that are common AND free everywhere

The true constraint on the *free* minimum is **Oracle Locator**, because Oracle's "geoprocessing"
functions require the licensed Oracle Spatial option. The functions below are present in Locator
(and everywhere else), so they define the safe minimum:

- **Measurement:** `Distance`, `Area`, `Length` (Oracle: `SDO_GEOM.SDO_DISTANCE/SDO_AREA/SDO_LENGTH`).
- **Topological predicates (DE-9IM):** `Equals`, `Disjoint`, `Intersects`, `Touches`,
  `Within`, `Contains`, `Overlaps` — plus `Crosses` **with one caveat** (Oracle has no direct mask
  for it; see Appendix A/B). Return-value semantics differ and MUST be normalized: OGC drivers
  return boolean; **SQL Server returns `bit` → compare `= 1`**; **Oracle `SDO_GEOM.RELATE` returns
  the mask string or `'FALSE'` → compare `<> 'FALSE'`**. All return **NULL on SRID mismatch**.
- **Within-distance / nearest filter:** `ST_DWithin`-style proximity (Oracle:
  `SDO_WITHIN_DISTANCE` operator *requires an index*; use `SDO_GEOM.SDO_DISTANCE(a,b,tol) <= d` for
  the index-free form; SQL Server: `a.STDistance(b) < d`). Key for indexed queries.
- **Accessors:** `SRID` (get), `GeometryType`, `IsEmpty`, `X`/`Y` for points, `Envelope` /
  bounding box. Oracle is the outlier (numeric `GET_GTYPE()`, `SDO_POINT.X/Y`, `SDO_GEOM.SDO_MBR`);
  full per-driver mapping in Appendix A.

### 4. Explicitly OUTSIDE the minimum common set

- **Geoprocessing:** `Buffer`, `Union`, `Intersection`, `Difference`, `ConvexHull`, `Centroid`.
  These are **licensed** on Oracle (Spatial, not Locator) → not universally free. Defer or expose
  as opt-in/driver-specific.
- **Distinct `geography` type** (only SQL Server + PostGIS).
- **3D / M coordinates.**
- **Portable spatial-index DDL** — see next section; there is no common syntax.

### 5. Spatial indexing — IN v1, per-driver DDL builders (create-time)

**Decision (user, 2026-07-08): spatial index support IS in v1**, implemented in each driver's
DDL/create-table builder. There is no *portable* spatial-index DDL — each engine differs — so this
is explicitly a per-driver render, not a common template. Every engine indexes spatial columns; the
DDL and preconditions are all different:

| Engine | Index DDL | Extra requirements the geo-column declaration must supply |
|---|---|---|
| PostGIS | `CREATE INDEX ... USING GIST (col)` | — |
| SQL Server | `CREATE SPATIAL INDEX ... USING GEOMETRY_GRID WITH (BOUNDING_BOX=(xmin,ymin,xmax,ymax))` | table needs a **clustered PK** (entities have one); `geometry` needs a **bounding box** |
| MySQL | `SPATIAL INDEX (col)` | column must be **`NOT NULL`** and **SRID-restricted** in its definition |
| SQLite/SpatiaLite | `SELECT CreateSpatialIndex('tbl','col')` — a **function call**, not DDL (R-tree virtual table) | column registered via `AddGeometryColumn`/`RecoverGeometryColumn` |
| Oracle | `CREATE INDEX ... INDEXTYPE IS MDSYS.SPATIAL_INDEX_V2` | an **`INSERT` into `USER_SDO_GEOM_METADATA`** (dimension bounds + tolerance + SRID) **before** the index — a DML step the builder must sequence |

**Why this cleanly fits v1 despite the index-reconciliation defect:** that defect
(`../INDEX_RECONCILIATION_PROBLEM.md`) is specifically about `UpdateTables` not reconciling indexes
on an **already-existing** table. Indexes declared on a **brand-new table/column are still emitted
at `CREATE` time** via `TableDdlBuilder.HandleAfterQuery` / `CreateTableBuilder.HandleCompositeIndex`
(that doc, lines 41–44). So per-driver spatial-index emission in the create-table path works in v1
**without** the reconciliation fix. Adding/dropping a spatial index on a *live* table later inherits
the general index-reconciliation fix — spatial is not a special case there, it rides the same
mechanism (which JSON also consumes).

**What this pulls into the geo-column declaration** (needed by the create-time path itself, index or
not on some engines): **SRID** (MySQL/Oracle require it; needed to build the column), **dimension
bounds / bounding box** (MSSQL index + Oracle metadata), **tolerance** (Oracle metadata), and a
**not-null** flag when indexed on MySQL. These become attributes on the geo property. Two engines
also need **non-DDL steps sequenced into the builder**: Oracle's metadata `INSERT` before the index,
and SpatiaLite's `AddGeometryColumn` + `CreateSpatialIndex` function calls (not `CREATE INDEX`).

**Index model gap to close:** the field model is
`CompositeIndex.Field = (SqlFunctionId? Function, string Name, SortDir)` — no channel for an index
**kind** (spatial vs b-tree) or its metadata (bbox/SRID/tolerance). v1 must extend this to carry a
spatial-index descriptor.

**Bounding box is a declared attribute on the spatial-index declaration** (user decision,
2026-07-08): the developer supplies `(xmin, ymin, xmax, ymax)` (plus tolerance for Oracle, with a
default) on the spatial-index attribute — not auto-derived. It drives SQL Server's
`WITH (BOUNDING_BOX=...)` and Oracle's `USER_SDO_GEOM_METADATA` dimension row; PostGIS, MySQL, and
SpatiaLite need no bounds and ignore it.

## How this fits the codebase (seams already identified)

From the driver/type-system map (2026-07-08) — full detail mirrors the JSON analysis:

- **Declaration:** a marker attribute (mirror `[JsonEntityProperty]`) intercepted in
  `ColumnDiscoverer.CreateColumnDescriptor` (`Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/ColumnDiscoverer.cs:59`).
  `DbType` is the fixed BCL enum — no new enum value; follow the `Guid` precedent (native on one
  driver, encoded on the rest).
- **Storage type string:** override `TypeName(DbType,size,precision,autoincrement)` in each
  `*LanguageSpecifics.cs` to emit `geometry` / `geometry` / `SDO_GEOMETRY` etc. `TypeName` is the
  *only* hook that renders a column type — a new "native type name" channel may be needed since
  `DbType` can't name a geo type.
- **Value binding / reading:** a decorating `IPropertyAccessor` presenting WKT `string` or WKB
  `byte[]` (binders + `SqlLanguageSpecifics` then need no change), OR override the **virtual**
  `BindParameterToQuery` in each `*Query.cs` for provider-native spatial parameter types
  (`NpgsqlDbType.Geometry`, `SqlDbType.Udt`, …). Read path symmetric via `TranslateValue`.
- **Spatial functions:** add members to `enum SqlFunctionId` (`SqlLanguageSpecifics.cs:821`) —
  e.g. `GeoDistance`, `GeoIntersects`, `GeoContains`, `GeoWithin`, `GeoArea`, `GeoLength`,
  `GeoFromText`, `GeoAsText` — give base defaults and override per driver in each
  `*LanguageSpecifics.GetSqlFunction`. **Caveat:** current `GetSqlFunction(id, string[] args)`
  assumes free-function syntax; SQL Server's method-call form and Oracle's tolerance/operator args
  need a richer arg channel. Expose through the same `ConditionBuilder.Raw` / `AddExpressionToResultset`
  seams the EAV feature uses (`DynamicPropertyConditionBuilder`, `DynamicPropertyProjection`).
- **SpatiaLite runtime:** the SQLite driver must `LoadExtension("mod_spatialite")` on connection
  open (opt-in), and CI/test needs the native library on the library path.

## Proposed scope for the geo feature (minimum common — for confirmation)

**In (across all 5, free tier):**
1. One **planar `geometry`** column type (OGC 7-type hierarchy, 2D, SRID-carried).
2. **WKT/WKB** as the .NET↔DB interchange; automatic round-trip on load/save.
3. Query surface for: **Distance, Area, Length**; predicates **Intersects/Contains/Within/
   Disjoint/Equals/Touches/Overlaps/Crosses**; **within-distance** proximity filter; accessors
   **SRID/GeometryType/IsEmpty/X/Y/Envelope**.
4. **Spatial index at table-create time**, per-driver DDL builder (see §5): each engine's native
   spatial index emitted on `CreateTable`, with the required column metadata (SRID, dimension
   bounds/bbox, tolerance, not-null) declarable on the geo property, and the Oracle metadata
   `INSERT` / SpatiaLite `AddGeometryColumn`+`CreateSpatialIndex` steps sequenced by the builder.

**Out / deferred (driver-specific or later phase):**
- `geography` type; geodetic-only functions.
- Geoprocessing (Buffer/Union/Intersection/Difference/ConvexHull/Centroid) — Oracle-licensed.
- 3D/M coordinates.
- **Spatial-index reconciliation on a live table** (add/drop on an already-existing table) — rides
  the general index-reconciliation fix (`../INDEX_RECONCILIATION_PROBLEM.md`), not spatial-specific.
  Create-time spatial indexing (item 4) does **not** depend on it.

## Confirmed decisions (user, 2026-07-08)

1. **CLR representation — in-house minimal geometry type.** A small Gehtsoft geometry class
   round-tripped purely as WKT/WKB. No NetTopologySuite dependency; we own the parsing/plumbing.
2. **Oracle tier — Locator (free).** Geoprocessing (Buffer/Union/Intersection/Difference/
   ConvexHull/Centroid) stays OUT of the portable surface, preserving free-everywhere.
3. **Driver scope — all five** (MSSQL, Oracle, SQLite/SpatiaLite, PostgreSQL/PostGIS, MySQL).
   Implies accepting the **PostGIS server install** and the **SpatiaLite `mod_spatialite` native
   library** as documented runtime prerequisites (SQLite driver must `LoadExtension` on open).
4. **Spatial indexing is IN v1**, via per-driver DDL/create-table builders (create-time; see §5).
   Consequence: the geo-column declaration carries SRID + dimension bounds + tolerance + not-null
   metadata, and the index-field model gains a spatial-kind channel. Live-table index
   add/drop reconciliation is NOT in v1 — it follows the general index-reconciliation fix.
5. **Bounding box is a declared attribute on the spatial-index declaration** (option a — the user
   supplies `(xmin, ymin, xmax, ymax)`; not auto-defaulted). It feeds SQL Server's
   `WITH (BOUNDING_BOX=...)` and Oracle's `USER_SDO_GEOM_METADATA` dimension bounds directly;
   PostGIS/MySQL/SpatiaLite ignore it. Tolerance (Oracle) likewise declared, with a sane default.

These lock the scope in "Proposed scope" above. Next step: write `GEO_PLAN.md`
(phased delivery) — deferred until the user opens the planning gate.

---

## Appendix A — Complete per-driver operation → SQL map (v1 surface)

*This is the implementation reference: every in-scope operation, rendered for each engine, so the
plan/coding phases need no further driver research. `g`, `a`, `b` = geometry expressions; `@p` =
bound WKT/WKB parameter; `tol` = Oracle tolerance (default e.g. `0.005`); `srid` = integer SRID.
Verified against vendor docs 2026-07-08 (sources at end).*

### Construction & output
| Operation | PostGIS | MySQL (8.0+) | SpatiaLite | SQL Server | Oracle (Locator) |
|---|---|---|---|---|---|
| WKT → geom | `ST_GeomFromText(@p,srid)` | `ST_GeomFromText(@p,srid)` | `GeomFromText(@p,srid)` | `geometry::STGeomFromText(@p,srid)` | `SDO_GEOMETRY(@p,srid)` or `SDO_UTIL.FROM_WKTGEOMETRY(@p)` |
| WKB → geom | `ST_GeomFromWKB(@p,srid)` | `ST_GeomFromWKB(@p,srid)` | `GeomFromWKB(@p,srid)` | `geometry::STGeomFromWKB(@p,srid)` | `SDO_UTIL.FROM_WKBGEOMETRY(@p)` (SRID set separately) |
| geom → WKT | `ST_AsText(g)` | `ST_AsText(g)` | `AsText(g)` / `ST_AsText(g)` | `g.STAsText()` | `SDO_UTIL.TO_WKTGEOMETRY(g)` |
| geom → WKB | `ST_AsBinary(g)` | `ST_AsBinary(g)` | `AsBinary(g)` / `ST_AsBinary(g)` | `g.STAsBinary()` | `SDO_UTIL.TO_WKBGEOMETRY(g)` |

### Measurement
| Op | PostGIS | MySQL | SpatiaLite | SQL Server | Oracle |
|---|---|---|---|---|---|
| Distance | `ST_Distance(a,b)` | `ST_Distance(a,b)` | `ST_Distance(a,b)` | `a.STDistance(b)` | `SDO_GEOM.SDO_DISTANCE(a,b,tol)` |
| Area | `ST_Area(g)` | `ST_Area(g)` | `ST_Area(g)` | `g.STArea()` | `SDO_GEOM.SDO_AREA(g,tol)` |
| Length | `ST_Length(g)` | `ST_Length(g)` | `ST_Length(g)` / `GLength(g)` | `g.STLength()` | `SDO_GEOM.SDO_LENGTH(g,tol)` |

### Topological predicates — with normalized boolean result
OGC drivers already return boolean. **SQL Server** returns `bit` → wrap as `(… = 1)`. **Oracle**
uses `SDO_GEOM.RELATE(a,'mask',b,tol)` which returns the mask string or `'FALSE'` → wrap as
`(SDO_GEOM.RELATE(...) <> 'FALSE')`.

| OGC predicate | PostGIS / MySQL / SpatiaLite | SQL Server | Oracle RELATE mask |
|---|---|---|---|
| Intersects | `ST_Intersects(a,b)` | `a.STIntersects(b)=1` | `ANYINTERACT` |
| Disjoint | `ST_Disjoint(a,b)` | `a.STDisjoint(b)=1` | `ANYINTERACT` → **negate** (`= 'FALSE'`) |
| Equals | `ST_Equals(a,b)` | `a.STEquals(b)=1` | `EQUAL` |
| Touches | `ST_Touches(a,b)` | `a.STTouches(b)=1` | `TOUCH` |
| Within | `ST_Within(a,b)` | `a.STWithin(b)=1` | `INSIDE+COVEREDBY` |
| Contains | `ST_Contains(a,b)` | `a.STContains(b)=1` | `CONTAINS+COVERS` |
| Overlaps | `ST_Overlaps(a,b)` | `a.STOverlaps(b)=1` | `OVERLAPBDYINTERSECT` |
| Crosses | `ST_Crosses(a,b)` | `a.STCrosses(b)=1` | **no direct mask** — see Appendix B; treat as driver-limited |

### Within-distance (proximity filter)
| PostGIS | MySQL | SpatiaLite | SQL Server | Oracle |
|---|---|---|---|---|
| `ST_DWithin(a,b,d)` | `ST_Distance(a,b) <= d` | `ST_Distance(a,b) <= d` (or `PtDistWithin`) | `a.STDistance(b) <= d` | `SDO_GEOM.SDO_DISTANCE(a,b,tol) <= d` (index-free) / `SDO_WITHIN_DISTANCE` operator (indexed) |

### Accessors
| Op | PostGIS / MySQL / SpatiaLite | SQL Server | Oracle |
|---|---|---|---|
| SRID (get) | `ST_SRID(g)` | `g.STSrid` | `g.SDO_SRID` |
| GeometryType | `ST_GeometryType(g)` / `GeometryType(g)` | `g.STGeometryType()` | `g.GET_GTYPE()` (numeric 1..7 → map to name) |
| IsEmpty | `ST_IsEmpty(g)` | `g.STIsEmpty()=1` | no direct — test `g IS NULL` / `GET_GTYPE()` (see B) |
| X / Y (point) | `ST_X(g)` / `ST_Y(g)` | `g.STX` / `g.STY` | `g.SDO_POINT.X` / `g.SDO_POINT.Y` (simple points only) |
| Envelope / MBR | `ST_Envelope(g)` | `g.STEnvelope()` | `SDO_GEOM.SDO_MBR(g)` |

### Spatial index creation (create-time; §5)
| PostGIS | MySQL | SpatiaLite | SQL Server | Oracle |
|---|---|---|---|---|
| `CREATE INDEX ix ON t USING GIST (col)` | `SPATIAL INDEX(col)` (col `NOT NULL SRID s`) | `SELECT InitSpatialMetaData();` (once) → `SELECT AddGeometryColumn('t','col',s,'GEOMETRY',2);` → `SELECT CreateSpatialIndex('t','col');` | `CREATE SPATIAL INDEX ix ON t(col) USING GEOMETRY_GRID WITH (BOUNDING_BOX=(xmin,ymin,xmax,ymax))` | `INSERT INTO USER_SDO_GEOM_METADATA(TABLE_NAME,COLUMN_NAME,DIMINFO,SRID) VALUES(...);` → `CREATE INDEX ix ON t(col) INDEXTYPE IS MDSYS.SPATIAL_INDEX_V2` |

## Appendix B — Per-driver gotchas / non-obvious facts (must-know before coding)

Each of these will otherwise surface as a mid-implementation surprise:

**SpatiaLite (SQLite)**
- `SELECT InitSpatialMetaData();` **must be run once per database** before any geometry column /
  spatial function works. The create-table flow must ensure it has run.
- The geometry column is added **after** `CREATE TABLE`, as a *separate* `AddGeometryColumn(...)`
  step (it also installs 4 validation triggers) — it is **not** a normal inline column DDL. This
  breaks the usual "column appears in CREATE TABLE" assumption; the geo column must be emitted
  post-create (fits `TableDdlBuilder.HandleAfterQuery`).
- **Stored BLOB ≠ standard WKB** (it's a modified format carrying the MBR). Always read via
  `AsBinary()`/`ST_AsBinary()`; never bind/read the raw column bytes.
- `mod_spatialite` native library must be on the OS library path and loaded via
  `connection.LoadExtension("mod_spatialite")` on open. CI/test needs the binary present.

**Oracle (Locator)**
- **SRID gotcha:** 4326 (EPSG WGS84) and 8307 (Oracle legacy WGS84) are equivalent; some geodetic
  ops return 8307. Standardize on **4326** and be aware results/metadata may show 8307.
- **`Crosses` has no direct RELATE mask** — Oracle Locator can't express OGC `Crosses` cleanly.
  Options for the plan: emulate via mask combos, or mark `Crosses` unsupported-on-Oracle (throw,
  consistent with the RuleExecutionSide "throw on untranslatable" precedent). Decision deferred to plan.
- `SDO_RELATE`/`SDO_WITHIN_DISTANCE` **operators require a spatial index**; for a WHERE on a
  possibly-unindexed column use the `SDO_GEOM.RELATE`/`SDO_GEOM.SDO_DISTANCE` **functions** instead.
- Spatial index needs a **`USER_SDO_GEOM_METADATA` row first** (DIMINFO bounds + tolerance + SRID) —
  DML sequenced before the `CREATE INDEX`.
- All `SDO_GEOM.*` measurement/relate functions take a **tolerance** argument (no OGC analog);
  needs a default and an override channel.
- Accessors are numeric/structural: `GET_GTYPE()` returns a number (map to type name); `SDO_POINT.X/Y`
  populated only for simple points; no clean `IsEmpty`.

**SQL Server**
- Predicates return **`bit` (0/1)**, not boolean → generated WHERE must append `= 1`.
- All predicates return **NULL if SRIDs don't match** (silent no-match) — enforce a consistent SRID.
- Uses **method-call syntax on the UDT** (`@g.STDistance(@h)`), not free functions — the
  `GetSqlFunction` arg channel must support "receiver.method(args)" shape.
- Spatial index requires a **clustered primary key** on the table (entities normally have one) and,
  for `geometry`, a mandatory **bounding box**.

**MySQL**
- Practical minimum is **8.0** (SRID-aware geometry, full `ST_*` set, InnoDB spatial index).
  5.7 works but lacks SRID-aware distance semantics.
- `ST_Distance` on a **geographic SRID (e.g. 4326) returns metres**; on SRID 0 it returns coordinate
  units. `ST_Distance_Sphere` is the explicit geodetic variant.
- Spatial index requires the column be **`NOT NULL`** and **SRID-restricted** in its definition
  (`col GEOMETRY NOT NULL SRID 4326`).

**PostGIS**
- Cleanest of the five: free `ST_*` functions, `USING GIST` index, no extra column steps. Requires
  the extension installed server-side (`CREATE EXTENSION postgis`) — a provisioning prerequisite,
  not something the driver can do per-connection.

**Cross-cutting**
- Staying **geometry-only (not geography)** also dodges the SQL Server / PostGIS **lat-lon axis-order
  swap** that geography constructors impose — WKT stays consistent `X Y` (lon lat) everywhere.

### Sources (verified 2026-07-08)
- SpatiaLite: gaia-gis.it BLOB-Geometry format; SpatiaLite cookbook (InitSpatialMetaData / AddGeometryColumn); Microsoft.Data.Sqlite extensions doc.
- Oracle: docs.oracle.com SDO_GEOM.RELATE reference, SDO_GEOMETRY object type, SDO_GEOM.SDO_MBR, Locator chapter; SDO_CS (4326 vs 8307).
- SQL Server: learn.microsoft.com STIntersects/STContains (geometry) return `bit`, Spatial Indexes Overview.
- MySQL: dev.mysql.com spatial function reference / spatial index requirements.
