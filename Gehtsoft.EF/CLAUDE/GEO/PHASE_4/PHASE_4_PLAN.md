# GEO Phase 4 — Pure-SQL query surface (the ★ query phase)

*Plan drafted 2026-07-20 for the Gate. Read `../GEO_PLAN.md` (Phase 4 bullet + "Query translation" +
Appendix A/B of `../GEO_COMMON_FUNCTIONALITY.md` — the per-driver SQL map) and `../STATE.md` first.
**Nothing is coded until this plan is approved (Gate).** Coding then proceeds increment-by-increment
with the second per-phase advance gate at the end.*

## Goal (from GEO_PLAN Phase 4)

Build the **entire geo query machinery at the SQL-builder layer only** — operating on a
`TableDescriptor` that carries a geometry column (`.Geometry`, already installed by Phase 1) through
`InsertQueryBuilder` / `UpdateQueryBuilder` / `DeleteQueryBuilder` / `SelectQueryBuilder` /
`ConditionBuilder` + the binders, with **no entity queries**. This mirrors JSON's pure-SQL surface
(`JsonPureSqlTest` / `JsonPureSqlProjectionTest`); the entity phases (5–7) then add thin wrappers that
resolve the column and delegate here. Scope, verbatim from the overall plan:

1. Geo renderers + the **arg-channel fix** (R1): MSSQL receiver-method shape, Oracle `SDO_*` + RELATE
   mask + tolerance, and the renderer owning its full boolean/scalar result (incl. the MSSQL `= 1` /
   Oracle RELATE `<> 'FALSE'` comparison) so predicate normalization lives in one place. *(Phase-4
   refinement, Gate decision 1: two dedicated enums + two render methods — `SqlGeoFunctionId` /
   `GeometryFunction` for value/scalar ops and `SqlGeoPredicateId` / `GeometryPredicate` for boolean
   predicates — rather than geo members on the generic `SqlFunctionId`. SOLID / segregated concerns.)*
2. **Insert/update value-wrapping** — the placeholder wrapped in the constructor function
   (`ST_GeomFromWKB(@p,srid)` / `geometry::STGeomFromWKB(@p,srid)` / `SDO_UTIL.FROM_WKBGEOMETRY(@p)` …),
   WKB bound as `byte[]`. **Mass update OUT** (decision 12) — single-column value-wrap only.
3. **WHERE predicates + measurements + within-distance** via a dedicated `ConditionBuilder` geo entry
   point (analogue of `JsonValue`), plus **mass delete** with a spatial WHERE. `Crosses` **throws
   "unsupported on Oracle"** (decision 11); the other 7 predicates map normally.
4. **Select output-wrapping** (`ST_AsBinary` / `STAsBinary` / `TO_WKBGEOMETRY`) so portable WKB comes
   back on every driver (never the raw column — SpatiaLite's stored BLOB is *modified* WKB) + filtered
   **count**.
5. **Projection** of a geo scalar (`SqlGeoFunctionId.Distance`/`Area`/`Length`/accessors) or the geometry
   (as WKB), plus **scalar ORDER BY / GROUP BY / numeric aggregation** over a cached byte-identical
   geo-scalar expression ("scalar in, geometry-value out"; out: spatial aggregates + order/group on a
   raw geometry — decision 12).

All geo expressions are added through **direct-add entry points that bypass the scalar guard** (the
Oracle RELATE mask is a quoted literal that trips `SqlInjectionProtectionPolicy`).

## What the code already gives us (verified seams — file:line)

- **Column descriptor:** `TableDescriptor.ColumnInfo.Geometry` (`QueryBuilder/TableDescriptor.cs:181`) →
  `Metadata/GeometryColumnMetadata.cs` (`ClrType, Srid, Subtype, HasZ, HasM, Nullable, Indexes`).
  `GeometrySubtype` enum in `Gehtsoft.EF.Entities/Geometry/GeometrySubtype.cs`. Helper formatting in
  `Metadata/GeometryDdlHelper.cs` (`SubtypeName`, `DimensionToken`, `Number`).
- **Function vocabulary:** `enum SqlFunctionId` at `SqlLanguageSpecifics.cs:930` — **no geo members yet**;
  `Now`/`LinuxSeconds` are the precedent for "dialect-specific, returns null where unsupported".
- **Scalar renderer:** base `SqlDbLanguageSpecifics.GetSqlFunction(SqlFunctionId, string[] args)` at
  `SqlLanguageSpecifics.cs:629`; per-driver overrides delegating to `base` — Mssql :142, Oracle :262,
  Postgres :154, Mysql :206, Sqlite :279. `SupportsGeometry` gate at `SqlLanguageSpecifics.cs:58`
  (overridden true on all 5) — geo renderers guard on it.
- **Condition builder (JSON template):** `ConditionBuilder.SingleConditionBuilder.Raw` at
  `QueryBuilder/ConditionBuilder.cs:73` carries the scalar guard (`:74-76`). JSON bypasses it via
  `JsonValue(...)` (`:129`/`:139`) → private `SetJsonSide(string)` (`:144`) which sets `Left`/`Right`
  directly. `SuppressScalarProtection` flag on `IConditionBuilderInfoProvider` (`:26`).
- **Select builder (JSON template):** resultset item `SelectQueryBuilderResultsetItem`
  (`QueryBuilder/SelectQueryBuilder.cs:17`), generic `AddExpressionToResultset` (:235); JSON path:
  private `JsonExpr(...)` (:489, byte-identical cached expr), `AddJsonValueToResultset` (:495/:515/:522/
  :533), `internal AddRawJsonExpressionToResultset` (:505, pre-built expr, guard-bypassing),
  `AddJsonValueToOrderBy` (:539/:545), `AddJsonValueToGroupBy` (:551…).
- **Update value-wrap (already exists):** `UpdateQueryBuilder.AddUpdateColumnExpression(column,
  rawExpression, parameterDelimiter="@")` at `QueryBuilder/UpdateQueryBuilder.cs:54` →
  `col=<rawExpression>` (:63). This is exactly the constructor-wrap seam for the update side.
- **Insert value-wrap (GAP — must add):** `InsertQueryBuilder` has only a **parameter-name** override
  (`mParameterNames` :28, `SetParameterNames` :63, `ParameterNameFor` :73). The build loop always emits
  `rightSide.Append(prefix + ParameterNameFor(name))` at `QueryBuilder/InsertQueryBuilder.cs:109-112` —
  **no per-column "wrap the parameter in a SQL function" hook.** Phase 4 adds one here (see Design C).
- **Scalar-guard bypass at query level:** `SqlDbConnection.GetQuery(queryText, suppressScalarProtection:
  true)` at `SqlDbConnection.cs:150` — already used for SpatiaLite geo DDL in
  `EntityQueries/Catalog/CatalogEntityController.cs:166/176/185/194`. Builder-level geo entry points
  will construct the SQL in-framework and set sides/resultset items directly (same discipline as JSON),
  so the string-level guard is not hit; where a whole builder is turned into a query text, use the
  `suppressScalarProtection: true` overload.
- **Value read-back today:** geometry object↔`byte[]` is handled at the **accessor** layer
  (`QueryBuilder/GeometryPropertyAccessor.cs`, object↔WKB via the global `IGeometryCodec`), i.e. at the
  *entity* level (Phases 5–7). At the pure-SQL level there are no entities → the SELECT output-wrap
  returns **`byte[]` WKB** and the binder reads bytes. Core never decodes to a geometry object — that is
  the NTS module's / application's job (Gate decision 3).
- **JSON pure-SQL tests (fixture template):** `Gehtsoft.EF.Test/JsonProperties/DataSelecting/
  JsonPureSqlTest.cs` + `JsonPureSqlProjectionTest.cs` — `IClassFixture<Fixture : SqlConnectionFixtureBase>`,
  `[Theory]` over `SqlConnectionSources.SqlConnectionNames(flags)`, entities exist only to obtain a
  `TableDescriptor` (`AllEntities.Inst[typeof(Doc)].TableDescriptor`), builder-only CRUD, AwesomeAssertions,
  **live round-trip** assertions.

## The three central problems

### R1 — the render contract (SETTLED, Gate decision 1)

`GetSqlFunction(id, string[] args)` assumes **free-function** syntax and a scalar result. Geo needs all
three grammars **and** the renderer must own the *full* expression including predicate normalization:

| grammar | example | driver |
|---|---|---|
| OGC free function | `ST_Intersects(a, b)` | PostGIS / MySQL / SpatiaLite |
| receiver-method + `= 1` | `(a.STIntersects(b) = 1)` | SQL Server |
| package + RELATE mask + tol + `<> 'FALSE'` | `(SDO_GEOM.RELATE(a,'ANYINTERACT',b,0.005) <> 'FALSE')` | Oracle |

Plus construction needs an **SRID int** and Oracle measurement a **tolerance double** — neither is a
geometry expression, so the positional `string[]` is unfit. **Dedicated geo render methods, separate from
the scalar `GetSqlFunction` (untouched).**

**Two distinct concerns → two enums + two render methods (SOLID / interface segregation — user, 2026-07-20).**
Predicates (boolean result, DE-9IM topology, need `= 1` / `<> 'FALSE'` normalization) and value/scalar
functions (construction, output, measurement, accessors) are separate axes and must not be conflated in
one enum. Keeping them apart also leaves the door open to expose a predicate through a function-style path
(e.g. project a boolean) or a function through a predicate context later, without a leaky shared enum.

```csharp
// --- value / scalar functions (construction, output, measurement, accessors) ---
public enum SqlGeoFunctionId
{ FromWkb, AsBinary, Distance, Area, Length, Srid, GeometryType, IsEmpty, X, Y, Envelope }

public readonly struct GeoFunctionRequest
{ public SqlGeoFunctionId Op; public string A; public int Srid; public double Tolerance; public string Parameter; }

// Returns the COMPLETE scalar/value SQL fragment. Base throws (no portable default),
// mirroring GeometryColumnDDL. Override per dialect.
public virtual string GeometryFunction(in GeoFunctionRequest request);

// --- topological / proximity predicates (boolean result) ---
public enum SqlGeoPredicateId
{ Intersects, Disjoint, Equals, Touches, Within, Contains, Overlaps, Crosses, DWithin }

public readonly struct GeoPredicateRequest
{ public SqlGeoPredicateId Op; public string A; public string B; public double Tolerance; public double Distance; }

// Returns the COMPLETE boolean SQL fragment, already normalized (SQL Server '= 1',
// Oracle RELATE '<> FALSE'). Throws EfSqlException(FeatureNotSupported) for an op a
// driver cannot express (Oracle Crosses). Base throws. Override per dialect.
public virtual string GeometryPredicate(in GeoPredicateRequest request);

public virtual bool SupportsGeometryQuery => SupportsGeometry;   // capability gate
```
One override of each per dialect (MySQL8/MariaDB inherit the shared `MysqlDbLanguageSpecifics`). Each
method returns the whole fragment so grammar + normalization live in exactly one place. (`FromWkb`/
`AsBinary` are the only construction/output forms — **WKB only**, Gate decision 2; no WKT in Phase 4.)

### R2 — insert value-wrap seam is missing

Unlike update, `InsertQueryBuilder` cannot wrap a column's parameter in a function. Add a per-column
value-expression override symmetric with `AddUpdateColumnExpression`:

```csharp
// InsertQueryBuilder
public void SetColumnValueExpressions(params (string Column, string Expression)[] exprs);
```
The build loop (`InsertQueryBuilder.cs:109-112`) consults it: if a column has a value expression, emit it
verbatim on the right side instead of `prefix+param`; the `{param}` token inside the expression is
substituted with the resolved parameter name. The geo caller supplies
`("shape", specifics.GeometryFunction(construct request))` = `ST_GeomFromWKB(@shape, 4326)`; the WKB is
still bound as an ordinary `byte[]` parameter. (SpatiaLite construction is a plain function too, so no
DDL-style scalar-guard issue for the builder path.)

### R3 — SpatiaLite output-wrap is mandatory, not an optimization

SpatiaLite's stored column BLOB is *modified* WKB (carries the MBR). Reading the raw column returns
non-standard bytes on SpatiaLite and provider-native objects elsewhere. Every geo SELECT/projection of the
**geometry value** MUST wrap the column in the output function (`ST_AsBinary(col)` / `col.STAsBinary()` /
`SDO_UTIL.TO_WKBGEOMETRY(col)`), so a portable `byte[]` WKB comes back on all five. This is a correctness
requirement (Appendix B).

## Design

Everything hangs off the two R1 render methods + the JSON-style guard-bypassing entry points.

### A. Vocabulary — two dedicated geo enums (Increment 1)

- **`SqlGeoFunctionId`** (value/scalar): `FromWkb, AsBinary, Distance, Area, Length, Srid, GeometryType,
  IsEmpty, X, Y, Envelope`. **WKB only** — no `FromText`/`AsText` (Gate decision 2; DB wire form is WKB,
  decision 6).
- **`SqlGeoPredicateId`** (boolean): `Intersects, Disjoint, Equals, Touches, Within, Contains, Overlaps,
  Crosses, DWithin`.

The generic `SqlFunctionId` is **not** extended with geo members (SOLID — Gate decision 1). Rendered per
driver exactly per `GEO_COMMON_FUNCTIONALITY.md` Appendix A.

### B. Per-driver `GeometryFunction` + `GeometryPredicate` renderers (Increment 1)

One override of each per dialect, tables straight from Appendix A. Normalization owned by the renderer:
- **PostGIS / MySQL / SpatiaLite:** OGC `ST_*` (SpatiaLite construction `GeomFromWKB`/`ST_GeomFromWKB`,
  output `ST_AsBinary`). `GeometryPredicate` returns the bare boolean call.
- **SQL Server:** `GeometryFunction` → `geometry::STGeomFromWKB(@p, srid)` / `col.STAsBinary()` /
  `a.STDistance(b)` etc. `GeometryPredicate` → `(a.STIntersects(b) = 1)` etc.
- **Oracle (Locator):** `GeometryFunction` → construction `SDO_UTIL.FROM_WKBGEOMETRY(@p)` (SRID applied
  separately — see R5), output `SDO_UTIL.TO_WKBGEOMETRY(col)`, measurement `SDO_GEOM.SDO_DISTANCE(a,b,tol)`,
  accessors via `GET_GTYPE()`/`SDO_POINT.X/Y`/`SDO_GEOM.SDO_MBR`. `GeometryPredicate` →
  `(SDO_GEOM.RELATE(a,'MASK',b,tol) <> 'FALSE')` with the Appendix-A masks (Disjoint = `ANYINTERACT`
  **negated** → `= 'FALSE'`); **`Crosses` throws `EfExceptionCode.FeatureNotSupported` "Crosses is not
  supported on Oracle"** (decision 11).
- Base `SqlDbLanguageSpecifics.GeometryFunction`/`GeometryPredicate` both throw (no portable default),
  mirroring `GeometryColumnDDL`.

### C. Insert/update value-wrap (Increment 2)

- Insert: new `SetColumnValueExpressions` (R2) + build-loop consult.
- Update: reuse `AddUpdateColumnExpression` with the construction expression. **Mass/multi-row update of a
  geo field stays OUT** (decision 12) — we only wrap the single-row column-set case.
- Both bind WKB as `byte[]`; the constructor + SRID come from `GeometryFunction(FromWkb)`.

### D. Select output-wrap + read-back (Increment 2)

- `SelectQueryBuilder.AddGeometryValueToResultset(TableDescriptor.ColumnInfo column, QueryBuilderEntity
  entity=null, string alias=null)` — clone of `AddJsonValueToResultset`: builds
  `GeometryFunction(AsBinary, A=alias-qualified col)`, adds a `SelectQueryBuilderResultsetItem` with
  `DbType.Binary`, guard-bypassing (direct `mResultset.Add`). Returns `byte[]` WKB.
- **Filtered count** is the existing `AddToResultset(AggFn.Count, …)` — no geo specifics.
- Read-back at pure-SQL = **`byte[]` WKB, permanently** (Gate decision 3, user 2026-07-20). There is **no
  WKB→object decode registry** in core at any phase: `byte[]` is the boundary of the general SQL layer,
  and object encode/decode is the application's job via `Gehtsoft.EF.Geo.NetTopologySuite`. Core
  `Gehtsoft.EF.Db.SqlDb` never references `GeometryCodecs`/`IGeometryCodec` at the query layer. Any future
  convenience (read a geometry object off a `SqlDbQuery`, bind one via `SetParameter`) is delivered as
  **extension methods in the NTS module**, not here.

### E. WHERE predicates + measurements + within-distance + mass delete (Increment 3)

- `ConditionBuilder`: new `GeoPredicate(SqlGeoPredicateId op, ColumnInfo a, QueryBuilderEntity ea, string
  parameterName /*WKB @b*/, double distance=0, …)`, each delegating to a private `SetGeoSide(string expr)`
  twin of `SetJsonSide` (sets `Left`/`Right` directly, bypassing the scalar guard). The operand geometry
  `b` is bound WKB wrapped via `GeometryFunction(FromWkb)`; the predicate SQL comes whole from
  `GeometryPredicate(op)` (already normalized). Within-distance = `SqlGeoPredicateId.DWithin` (`ST_DWithin`
  on PostGIS; `distance(a,b) <= d` elsewhere; Oracle index-free `SDO_GEOM.SDO_DISTANCE(a,b,tol) <= d`).
- **Mass delete** = `DeleteQueryBuilder` + a geo `Where` condition through the same entry point — no new
  delete mechanics.
- `Crosses` throws on Oracle (propagated from `GeometryPredicate`).

### F. Projection + scalar ORDER BY / GROUP BY / aggregation (Increment 4)

"Scalar in, geometry-value out": a geo **scalar** function (`SqlGeoFunctionId.Distance`/`Area`/`Length`/
`X`/…) yields an ordinary number, so it reuses the existing scalar machinery. Add guard-bypassing,
**byte-identical cached** entry points cloned from JSON:
- `AddGeometryScalarToResultset(SqlGeoFunctionId op, column, …, alias, bool aggregate=false)` (and an
  `AggFn` overload) → builds the scalar expr once via a private `GeoScalarExpr(...)` (the `JsonExpr` twin)
  and adds it; `AddGeometryScalarToOrderBy` / `AddGeometryScalarToGroupBy` reuse **the same cached string**
  so `BuildGroupBy`'s exact-string match holds (GEO_PLAN "Caveat").
- Projection of the **geometry itself** = the output-wrap from D.
- **OUT:** spatial aggregates (`ST_Union`/`ST_Collect`/`ST_Extent`), ORDER BY / GROUP BY on a raw geometry
  (decision 12).

### G. NTS module query/parameter extension methods (Increment 2 — test-support + shipped API)

New in **`Gehtsoft.EF.Geo.NetTopologySuite`** (user, 2026-07-20) — the concrete form of Gate decision 3's
"future conveniences live in the NTS module". They keep core `byte[]`-only while giving apps (and the
Phase-4 tests) a readable geometry-object surface:

```csharp
namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    public static class GeometrySqlExtensions
    {
        // encode an NTS Geometry to WKB and bind it as a byte[] parameter
        public static void BindGeometryParam(this SqlDbQuery query, string name, Geometry value);
        // read a WKB byte[] column back into an NTS Geometry (by index or name)
        public static Geometry GetGeometry(this SqlDbQuery query, int column);
        public static Geometry GetGeometry(this SqlDbQuery query, string column);
    }
}
```
Encode/decode go through the NTS codec (`NtsGeometryCodec`, plain OGC WKB — `includeSrid: false`), so they
mirror exactly what the DB constructor/output functions expect. **Requires a new `ProjectReference` from
the NTS module → `Gehtsoft.EF.Db.SqlDb`** (one-way; core never references NTS — verified). Used throughout
the Phase-4 behavioural tests so a test reads `query.GetGeometry("shape")` / `query.BindGeometryParam(...)`
instead of hand-rolling WKB `byte[]`, and asserts on NTS geometry equality.

## Files (anticipated)

- **`Gehtsoft.EF.Db.SqlDb`** (product — every new `.cs` needs an explicit `<Compile Include>`;
  `EnableDefaultCompileItems=false`):
  - `SqlLanguageSpecifics.cs` — `SqlGeoFunctionId` + `SqlGeoPredicateId` enums; `GeoFunctionRequest` +
    `GeoPredicateRequest` structs; base `GeometryFunction` + `GeometryPredicate` (both throw) +
    `SupportsGeometryQuery`. (Generic `SqlFunctionId` **not** touched.)
  - `QueryBuilder/ConditionBuilder.cs` — `GeoPredicate` entry points + `SetGeoSide`.
  - `QueryBuilder/SelectQueryBuilder.cs` — `AddGeometryValueToResultset`, `AddGeometryScalarTo{Resultset,
    OrderBy,GroupBy}` + private `GeoScalarExpr`.
  - `QueryBuilder/InsertQueryBuilder.cs` — `SetColumnValueExpressions` + build-loop consult.
  - (`UpdateQueryBuilder.AddUpdateColumnExpression` already suffices.)
- **Each driver** (`Mssql/Oracle/Postgres/Mysql/Sqlite *LanguageSpecifics.cs`): `GeometryFunction` +
  `GeometryPredicate` overrides. MySQL8/MariaDB inherit the shared base override.
- **`Gehtsoft.EF.Geo.NetTopologySuite`** (§G): new `GeometrySqlExtensions.cs` (default-globbed, auto-
  included) **+ a new `ProjectReference` → `Gehtsoft.EF.Db.SqlDb`** in the module's `.csproj` (the one
  csproj edit this phase; not a version/packaging change).
- **No `Gehtsoft.EF.Entities` change** (attributes/metadata exist). **No entity-query change** (Phases 5–7).
  **Core `Gehtsoft.EF.Db.SqlDb` gains no NTS reference** — it stays `byte[]`/WKB only; the NTS surface
  lives only in the NTS module (§G).

## Increments (finish-and-verify each; small, independently green)

1. **Two geo enums + `GeometryFunction`/`GeometryPredicate` renderers, all 5 dialects.** Oracle `Crosses`
   throws. Deep AST tests per driver for every op (construction/output/measurement/8 predicates/
   within-distance/accessors). No behavioural yet. **✅ DONE (2026-07-20, uncommitted).** `SqlGeoFunctionId`
   + `SqlGeoPredicateId` + `GeoFunctionRequest`/`GeoPredicateRequest` + base `GeometryFunction`/
   `GeometryPredicate` (throw) + `SupportsGeometryQuery` in `SqlLanguageSpecifics.cs`; shared OGC renderers
   (`RenderOgcGeometry{Function,Predicate}`) reused by PG/MySQL/SpatiaLite; MSSQL + Oracle own renderers.
   Oracle `Crosses` **and** `IsEmpty` throw `FeatureNotSupported` (Locator has no clean `IsEmpty`). Tests:
   `Geo/DataSelecting/GeometryRenderTest.cs` (16, exact-string per the DDL-gen precedent). Full geo suite
   **73 green**.
2. **Insert/update value-wrap + select output-wrap + WKB read-back + NTS extension methods (§G).** Enables
   the first SpatiaLite round-trip — `query.BindGeometryParam("shape", point)` in → select it back →
   `query.GetGeometry("shape")` equals the input. **✅ DONE (2026-07-20, uncommitted).**
   - Insert: `InsertQueryBuilder.SetColumnValueExpressions` + `ParameterToken` + build-loop consult.
     Update: reuse `AddUpdateColumnExpression`. Select: `SelectQueryBuilder.AddGeometryValueToResultset`
     (WKB output-wrap, guard-bypassing, `DbType.Binary`).
   - NTS module (§G): `GeometrySqlExtensions.BindGeometryParam`/`GetGeometry` + new `ProjectReference`
     NTS→`Db.SqlDb`.
   - **Codec fix (NTS module, in passing):** `NtsGeometryCodec.ToWkb` was hard-coding `WKBWriter(...,
     emitZ:true, emitM:true)`, forcing **XYZM on every geometry** (a 2-D point serialized as 37-byte
     XYZM with NaN Z/M), which fixed-dimension columns (SpatiaLite XY, MySQL 2-D) reject. Now emits
     exactly the geometry's own ordinates (via an `ICoordinateSequenceFilter`). Phase-0 codec tests stay
     green; the 2-D round-trip now works.
   - Tests: `Geo/DataManagement/GeometryValueWrapTest` (insert/update wrap, exact-string on Dummy
     dialect — which gained an opt-in `SupportsGeometrySpec` rendering the OGC grammar),
     `Geo/DataSelecting/GeometryOutputWrapTest` (output-wrap), `Geo/DataManagement/
     GeometryRoundTripSpatialiteTest` (live SpatiaLite insert→select→update→select via the NTS
     extensions). The test SQL grammar has no spatial-function / function-valued-INSERT rule, so DB-free
     assertions are exact-string (as with the DDL-gen tests), not `.ParseSql()`.
   - Geo suite **78 green** on Linux **and** Windows; JSON/DynProps/query-builder regression 1125 green.
3. **WHERE predicates + measurements + within-distance + mass delete.** **✅ DONE (2026-07-20, uncommitted).**
   `ConditionBuilder`: `GeoPredicate` (standalone boolean — topological + `DWithin`; via `SetGeoSide` +
   `Push`→`Add(logOp,string)`, guard-bypassing) and `GeoScalar` (a measurement/accessor used as a
   comparison operand, e.g. `Where.GeoScalar(Area, col).Gt().Parameter("min")`); both on
   `SingleConditionBuilder` + `ConditionBuilderExtension` (so `Where.GeoPredicate(...)`). Mass delete =
   `DeleteQueryBuilder` + a geo `Where` (no new mechanics). Oracle `Crosses` throws through the renderer.
   Tests: `Geo/DataSelecting/GeometryPredicateSqlTest` (DB-free exact-string, alias-tolerant — predicate,
   within-distance, scalar comparison, mass delete), `GeometryPredicateSpatialiteTest` (live: Intersects +
   DWithin filter + mass delete), `GeometryPredicateAcceptanceTest` (topological Intersects + mass delete
   over **all 5 engines** — distance stays SpatiaLite-only since planar/geodetic semantics differ; MySQL 8
   enforces lat/lon ranges for SRID 4326, so test points use valid coordinates). Geo suite **93 green**;
   condition-builder + JSON regression 124 green.
4. **Projection + scalar ORDER BY / GROUP BY / numeric aggregation.** Order-by-distance top-N,
   group-by a geo scalar, `AVG(Area(col))`. **✅ DONE (2026-07-20, uncommitted).** `SelectQueryBuilder`:
   `AddGeometryScalarToResultset` (+ `AggFn` overload), `AddGeometryScalarToOrderBy`,
   `AddGeometryScalarToGroupBy`, all via one private `GeometryScalarExpr` (byte-identical string for GROUP
   BY matching), guard-bypassing. Tests: `Geo/DataSelecting/GeometryProjectionSqlTest` (DB-free exact-string
   incl. byte-identical order-by-distance), `GeometryProjectionSpatialiteTest` + `GeometryProjectionAcceptanceTest`
   (**value-correctness on SpatiaLite + all 5 engines** via shared `GeometryProjectionChecks`: Area of 2×2
   box = 4, Length = 3, X/Y = 3/4, Distance = 5, order-by-distance = [1,2,3] + top-N, AVG(Area) = 10,
   GROUP BY Area → {4:2,16:1}). Measurements (Distance/Area/Length) thus get their live coverage here.
   **Geo suite 152 green**; JSON/select-builder regression 128 green. **★ Phase 4 complete.**

## Testing model (two tiers, mirrors JSON/EAV)

- **Deep / AST (all 5 drivers, DB-free):** drive the public `*LanguageSpecifics.GeometryFunction`/
  `GeometryPredicate` and the builder entry points; assert generated SQL **parsed to AST** (`.ParseSql()`),
  never string `Contains`; where a fragment isn't a full parseable statement (a bare `SDO_GEOM.RELATE(...)`
  expression), assert it embedded in a minimal `SELECT … WHERE`/projection that parses, or structurally.
  Assert MySQL Z/M and Oracle `Crosses` throw.
- **Behavioural (SQLite + SpatiaLite here; other engines on the acceptance tier / live config):**
  round-trip WKB in/out, spatial predicate filtering, within-distance, mass delete, order-by-distance,
  grouped `AVG(Area)` — asserted on materialized values (geometry equality within tolerance, counts,
  distances), following the `JsonPureSqlTest` fixture pattern (`SqlConnectionFixtureBase`, `[Theory]` over
  `SqlConnectionNames`, builder-only, AwesomeAssertions). Tests use the **§G NTS extension methods**
  (`BindGeometryParam` / `GetGeometry`) for readability, binding/reading NTS geometries and asserting on
  NTS equality rather than raw `byte[]`.
- Tests under **`Gehtsoft.EF.Test/Geo/DataSelecting/`** + **`.../DataManagement/`** (default-globbed csproj;
  namespaces `Gehtsoft.EF.Test.Geo.DataSelecting` / `.DataManagement`), dedicated geo entities used only to
  obtain a geometry-carrying `TableDescriptor` (no entity queries). Product bugs → `KNOWN_ISSUES.md`, tests
  never adapted. Use the real `GeoTestData/` datasets where a meaningful geometry matters.

## Risks / to confirm during coding

- **R1 (renderer contract — SETTLED):** two enums (`SqlGeoFunctionId`/`SqlGeoPredicateId`) + two render
  methods (`GeometryFunction`/`GeometryPredicate`), base throws. Everything downstream depends on this
  shape; freeze it before Increment 1.
- **R2 (insert seam):** confirm the `{param}`-substitution in `SetColumnValueExpressions` interacts
  cleanly with `ParameterNameFor` and the autoincrement branch (`InsertQueryBuilder.cs:106/126`).
- **R3 (output-wrap):** verify SpatiaLite returns standard WKB via `ST_AsBinary` and the binder reads it
  as `byte[]` (the stored BLOB path must never be selected raw).
- **R4 (GROUP BY string match):** the geo scalar expr must be the **byte-identical cached** string in
  resultset + order-by + group-by, or `BuildGroupBy` won't match (GEO_PLAN caveat).
- **R5 (Oracle SRID on construct):** `SDO_UTIL.FROM_WKBGEOMETRY(@p)` sets SRID separately from the WKB —
  confirm how the SRID is applied on insert (set `.SDO_SRID` post-construct, or bind EWKB) and that reads
  round-trip 4326 (may surface 8307 — assert tolerantly, decision R5 of the overall plan).
- **R6 (scalar guard):** confirm every geo entry point sets sides/resultset items **directly** (never
  through guarded `Raw`/`AddExpressionToResultset`/`AddOrderByExpr`/`AddGroupByExpr`); the Oracle RELATE
  mask literal must never reach the guard.
- **R7 (within-distance vs index):** index-free forms are used at Phase 4 (Oracle `SDO_GEOM.SDO_DISTANCE`,
  not the index-only `SDO_WITHIN_DISTANCE` operator); indexed acceleration is a later concern.

## Gate decisions — RESOLVED (user, 2026-07-20)

1. **Renderer contract → dedicated methods, and split predicates from functions (SOLID).** Two enums —
   `SqlGeoFunctionId` (value/scalar) + `SqlGeoPredicateId` (boolean) — and two render methods on
   `SqlDbLanguageSpecifics`: `GeometryFunction(in GeoFunctionRequest)` and
   `GeometryPredicate(in GeoPredicateRequest)`, each returning the complete normalized SQL; base throws;
   one override of each per dialect (MySQL8/MariaDB inherit the shared base). The generic `SqlFunctionId` /
   `GetSqlFunction` is **not** touched. Keeping predicates and functions on separate enums avoids
   conflation and leaves room to route a predicate through a function path (or vice-versa) later.
2. **Construction/output form → WKB only.** Only `SqlGeoFunctionId.FromWkb`/`AsBinary`; no `FromText`/
   `AsText` in Phase 4 (WKB is the DB wire form, decision 6). WKT helpers can be added later if needed.
3. **Read-back → `byte[]` WKB, no core decode.** The general SQL layer keeps geometry as `byte[]`
   permanently; object encode/decode is the application's responsibility via
   `Gehtsoft.EF.Geo.NetTopologySuite`. **No** WKB→object decode registry in core, at any phase. Future
   reader/`SetParameter` object conveniences are **extension methods in the NTS module**, not core. Phase-4
   round-trip tests assert on `byte[]` WKB equality (no codec needed).
4. **Test fixtures → `Gehtsoft.EF.Test/Geo/{DataManagement,DataSelecting}/`** (namespaces
   `Gehtsoft.EF.Test.Geo.DataManagement` / `.DataSelecting`; default-globbed csproj).
5. **NTS module gains geometry query/parameter extension methods (§G)** — `BindGeometryParam` /
   `GetGeometry` on `SqlDbQuery`, in `Gehtsoft.EF.Geo.NetTopologySuite` (new `ProjectReference` →
   `Gehtsoft.EF.Db.SqlDb`). Shipped API **and** used by the Phase-4 tests for readability. Core stays
   `byte[]`-only.
