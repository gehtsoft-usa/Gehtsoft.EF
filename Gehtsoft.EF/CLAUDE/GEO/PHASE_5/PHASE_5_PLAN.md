# GEO Phase 5 — Entity-level geo query surface (WHERE · modification · SELECT clauses)

*Plan drafted 2026-07-20, **rewritten 2026-07-22** to cover the full entity-level surface (this
supersedes the earlier insert/update-only draft). Read `../GEO_PLAN.md`, `../STATE.md`,
`../PHASE_4/PHASE_4_PLAN.md`, `../ENTITY_API_REVIEW.md`, and `../../ENTITY_WHERE_PROBLEM.md` first.
**All three Gate decisions are LOCKED (2026-07-22 — see "Gate decisions" at the bottom).** Plan is
approved-to-shape; coding proceeds increment-by-increment (below), each ending green, with the per-increment
advance gate. **Commit only when the user asks.** No code has been written yet — awaiting the go-ahead to
start Increment 1.*

---

## 0. Governing conclusion — "we cannot do it purely on SQL wrapping"

The user's framing is correct, and the code map pins down exactly *why* per area. The entity layer is a
**delegate-where-the-abstraction-matches, re-implement-where-it-genuinely-differs** design, not a set of
thin pass-throughs. The three concrete reasons an entity method cannot just forward to the Phase-4
pure-SQL geo surface:

1. **Resolution mismatch (WHERE).** The entity-WHERE resolver `IEntityInfoProvider.Alias(...)`
   (`EntityConditionBuilder.cs:12-13`) returns only `(alias-string, DbType)`. JSON needed nothing more.
   Every Phase-4 pure-SQL geo method is parameterized on `(TableDescriptor.ColumnInfo column,
   QueryBuilderEntity entity)` and reads `column.Geometry.Srid` (`ConditionBuilder.cs:179,240`). So the
   WHERE layer must be taught a **richer resolution** that surfaces the `EntityQueryItem`
   (`.Column` + `.QueryEntity`, already stored at `EntityQueryWithWhereBuilder.cs:17-18`, reachable via
   the `internal FindItem/FindPath` at `:67,:82`). This is new entity-layer plumbing, not a wrap.
2. **Type-system split (WHERE + SELECT + modification operands).** The ergonomic object API (pass an NTS
   `Geometry`/`Point`) **cannot live in core** — core must never reference NTS (hard rule). So the "nice"
   typed API is deliberately split across two assemblies: `byte[]`-WKB entry points in core, NTS-object
   overloads as extensions in `Gehtsoft.EF.Geo.NetTopologySuite` (mirrors `GeometrySqlExtensions`).
3. **Result-shape & mode honesty (SELECT).** Scalar/aggregate projections are **tuples, not typed
   entities**; **GROUP BY is incompatible** with the whole-entity auto-select-all read; and the
   **Wkb vs Native** two-form read is a *mode* of the same select the API must expose. No transparent
   wrap can hide these — the API must present them honestly.

The one genuine exception is the **insert / single-update value round-trip**, which *can* be done as a
pure SQL wrap (Option A below) because the binder is already geo-agnostic and correct. The plan calls
that out explicitly rather than pretending the whole surface is thin.

---

## Shared prerequisite (increment 0 for both WHERE and SELECT) — the resolution seam

Add a `ColumnInfo`+entity-returning resolution to the entity-WHERE provider so geo WHERE methods can
reach `column.Geometry`.

**✅ DECIDED (Gate, 2026-07-22) — P-A: extend `IEntityInfoProvider`** with `bool TryResolveColumn(string
path / (Type, occurrence, name), out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity)`,
implemented on `EntityQueryWithWhereBuilder` by surfacing the existing `FindItem`/`FindPath`. Clean, typed,
testable in isolation; the JSON `Alias` path is untouched. (Rejected P-B — internal cast to the concrete
`EntityQueryWithWhereBuilder`: zero interface change but fragile, assumes the concrete type, and leaks the
concrete builder into the geo path.)

The SELECT layer needs **no** new resolver — it already has `GetReference`/`ResolveJsonColumn`
(`SelectEntitiesQueryBase.cs:501-506`) which return `ColumnInfo`+entity. Reuse it verbatim (a
`ResolveGeoColumn` twin).

---

## Area 1 — Entity WHERE clause

### (1) Interface & contract (how we test it)

Mirror JSON's `JsonPropertyOf` shape exactly. New members on `SingleEntityQueryConditionBuilder`
(`EntityQueryConditionBuilder.cs`) + fluent extension entry points on `EntityQueryConditionBuilder`,
in a new `GeoPropertyConditionBuilder.cs` twin of `JsonPropertyConditionBuilder.cs`:

- **Core (byte[]-WKB, in `Gehtsoft.EF.Db.SqlDb`):**
  - `GeoPredicateOf(string name, SqlGeoPredicateId op, byte[] operandWkb, Type entityType = null, int occurrence = 0, double distance = 0)` and the `GeoPredicateOf<T>(name, ...)` / member-expression `GeoPredicateOf<T>(Expression<Func<T,object>>, ...)` overloads.
  - Subquery-operand form: `GeoPredicateOf(string name, SqlGeoPredicateId op, AQueryBuilder nativeSubquery, ...)`.
  - `GeoScalarOf(string name, SqlGeoFunctionId op, ...)` returning a comparable single-builder (so callers chain `.Gt(500)` etc. through the existing fluent API).
- **NTS-module ergonomics (in `Gehtsoft.EF.Geo.NetTopologySuite`):**
  `GeoPredicateOf<T>(this EntityQueryConditionBuilder, Expression<Func<T,object>>, SqlGeoPredicateId op, NetTopologySuite.Geometries.Geometry operand, ...)` — encodes the operand to WKB via the codec (as `GeometrySqlExtensions.BindGeometryParam` does) then calls the core byte[] overload.

**Contract / tests:**
- **DB-free AST** (`Geo/DataSelecting/GeometryEntityPredicateSqlTest.cs`, via `DummyDbSpecifics` +
  `SupportsGeometrySpec`): build an entity `.Where.GeoPredicateOf(...)`, `PrepareQuery`, `.ParseSql()` the
  WHERE, assert the rendered predicate is `ST_Intersects(<alias>.shape, ST_GeomFromWKB(@p, 4326))` (and the
  MSSQL `... = 1` / Oracle `SDO_RELATE ... <> 'FALSE'` shapes via the respective specifics), that the SRID
  came from `column.Geometry.Srid`, and that the operand param is bound to the WKB. Native-subquery form:
  assert the operand is the raw subquery with **no** `FromWkb` wrap.
- **SpatiaLite behavioural** (`GeometryEntityPredicateSpatialiteTest.cs`): the playground tasks re-expressed
  through the entity API — city→containing state (`Contains`), track→crossed states (`Intersects`),
  `DWithin` — assert the same entity result sets the pure-SQL playground already asserts.
- **Acceptance** (`GeometryEntityPredicateAcceptanceTest.cs`, 5 engines): topological `Intersects` + a
  `GeoScalarOf(Area).Gt(...)` filter over the SRID-0 planar technique; assert row counts. Oracle `Crosses`
  asserts `FeatureNotSupported`.

### (2) Delegate or re-implement

**Delegate the rendering, re-implement the resolution.** Once the shared prerequisite yields
`(ColumnInfo, QueryBuilderEntity)`, the geo cores call straight into the Phase-4
`ConditionBuilder.GeoPredicate/GeoScalar(op, column, entity, parameterName, distance)` (bound-param and
subquery overloads both exist: `ConditionBuilder.cs:165,195,226`). The entity method sets the rendered
expression directly via a `SetGeoSide`-equivalent, **bypassing the raw-scalar guard**, exactly as
`SetJsonSide` does (`EntityQueryConditionBuilder.cs:163-165`). The operand value is bound internally under
a generated parameter name — the user never manages `@p`.

### (3) Why

The pure-SQL cores already render every dialect (MSSQL/Oracle/OGC) and already own `column.Geometry.Srid`;
re-deriving that at the entity layer would duplicate Phase-4. The **only** thing genuinely missing is the
richer property resolution, because JSON's alias-only resolver structurally can't carry the geometry
metadata. Core-can't-reference-NTS forces the object overloads into the NTS module. Both are "re-implement"
because the abstraction differs, not because the SQL differs.

---

## Area 2 — Entity modification queries

Scope: `InsertEntityQuery`, `UpdateEntityQuery` (single, by-id), `DeleteEntityQuery`,
`MultiDeleteEntityQuery` (mass delete by predicate), `MultiUpdateEntityQuery` (mass update).

### (1) Interface & contract (how we test it)

**No new public API for insert/single-update/delete** — the goal is that `SaveEntity(entity)` and
`connection.GetUpdateEntityQuery(type)` round-trip a geometry property transparently (both a `byte[]`
property and an NTS-object property; nullable → SQL NULL). Mass-delete gains geo purely by reusing Area 1's
entity WHERE. Mass-update of a geo **field** stays out of scope (decision 12); mass-update of a non-geo
field under a geo WHERE filter works via Area 1.

**Contract / tests:**
- **DB-free AST** (`Geo/DataManagement/GeometryEntityInsertUpdateSqlTest.cs`): build the entity INSERT and
  single UPDATE; `.ParseSql()`; assert the geo column's VALUES/SET side is `ST_GeomFromWKB(@shape, srid)` /
  `geometry::STGeomFromWKB(...)` / `SDO_UTIL.FROM_WKBGEOMETRY(@shape)` while non-geo columns stay plain
  parameters. Assert `null` geometry emits a plain bound-NULL parameter (verify each engine's constructor is
  **not** invoked on NULL, or wrap-of-NULL is NULL-safe — document which).
- **SpatiaLite round-trip** (`GeometryEntityRoundTripSpatialiteTest.cs`): entity insert → read back (Phase-4
  pure-SQL select output-wrap + `GetGeometry`, since entity SELECT is Area 3) → assert geometry equal within
  tolerance; update the geometry → re-read → assert. `byte[]` property, NTS-object property, nullable→null.
- **Acceptance** (`GeometryEntityRoundTripAcceptanceTest.cs`, 5 engines): 2-D point on a non-indexed generic
  geo column, SRID 0 planar technique; entity insert → read-back → entity update → re-read.
- **Mass delete** (`GeometryEntityMassDeleteSpatialiteTest.cs` + acceptance): `MultiDelete<T>().Where
  .GeoPredicateOf(Intersects, region)`; assert the surviving rows. Confirms the geo predicate flows through
  the mass-delete WHERE with no value-wrap.

### (2) Delegate or re-implement

- **Insert/single-update value-wrap — delegate to the pure-SQL wrap hooks.** `InsertQueryBuilder` already
  has `SetColumnValueExpressions` + `{param}` `ParameterToken` (`InsertQueryBuilder.cs:82,93-101,135-139`);
  `UpdateQueryBuilder` has `AddUpdateColumnExpression` (`UpdateQueryBuilder.cs:54-64`). The binder
  (`UpdateQueryToTypeBinder.BindAndExecuteCore:399-438`) is already correct: it binds the accessor's `byte[]`
  under the column param name and does `BindNull` for null — **no binder change**. The only decision is
  **where the `FromWkb` wrap is injected**:
  - **✅ DECIDED (Gate, 2026-07-22) — Option A: auto-wrap in the pure-SQL builders, keyed on
    `column.Geometry != null`,** as an additive fallback that an explicit `SetColumnValueExpressions`/
    `AddUpdateColumnExpression` still overrides. Entity builders stay zero-touch (like JSON insert/update),
    hand-built pure-SQL inserts over a geo table become correct for free, and it mirrors the *established
    metadata-driven emission* already in `InsertQueryBuilder.PrepareQuery` for autoincrement columns
    (`:112,:130` — `info.Autoincrement → ExpressionForAutoincrement`); a geo wrap keyed on `info.Geometry`
    is the identical pattern, one column-metadata flag beside another.
  - **Build it bottom-up (user directive):** implement + verify the auto-wrap **at the pure-SQL layer first**
    (`InsertQueryBuilder`/`UpdateQueryBuilder`), prove it produces the correct per-driver SQL and round-trips
    values — *including a null geometry* (see NULL note below) — on every engine, and *only then* let the
    entity insert/update path rely on it.
  - (Rejected Option B — inject from the entity builders only: keeps Phase-4 pure-SQL frozen but splits
    geo-awareness across two layers and forces the entity UPDATE builder to re-implement the
    `AddUpdateAllColumns` loop.)
  - **NULL is a pre-existing, builder-wide concern (not A/B, not entity-specific):** the SQL text is built
    once at `PrepareQuery`, values bind later, so a wrapped geo column always emits `FromWkb(@p, srid)` and a
    null value becomes `@p = NULL`. Phase 4 never actually bound a null geometry (`GeometryValueWrapTest.cs:28`
    only marks the column `Nullable = true`), so `FromWkb(NULL) → NULL` safety is **unverified per engine**.
    Increment 1 verifies it at the pure-SQL layer; special-case the wrap emission only if an engine rejects a
    null argument.
- **Delete by PK — nothing to do** (`DeleteEntityQuery` binds PK only, `:22`).
- **Mass delete — delegate to Area 1.** `MultiDeleteEntityQuery` (non-dynamic-property case) renders the
  `DeleteQueryBuilder` WHERE through the same `ConditionBuilder` (`MultiDeleteEntityQuery.cs:64,112`); a geo
  predicate flows through unchanged. **Re-implement nothing**, but honor the **entity-WHERE-welding**
  constraint (`ENTITY_WHERE_PROBLEM.md`): the EAV cascade path already materializes ids for dynamic-property
  filters — a geo filter on a plain entity is a single-statement delete and is fine; a geo filter combined
  with an EAV cascade inherits the existing workaround, no new geo work.
- **Mass update of a geo field — explicitly not implemented** (decision 12).

### (3) Why

This is the one area that genuinely *can* ride SQL wrapping: the binder already presents WKB `byte[]` and
handles NULL, so only the SET/VALUES constructor text is missing. Option A keeps a single source of truth
("a geometry column is constructor-wrapped") and makes the entity write path as thin as JSON's; Option B is
the alternative if we want Phase-4 pure-SQL byte-frozen. Mass delete needs no geo code of its own because
the predicate is Area 1's; the welding problem is pre-existing and unchanged by geo.

---

## Area 3 — Select query clauses (resultset · order by · group by · having)

### (1) Interface & contract (how we test it)

Mirror JSON's select methods on `SelectEntitiesQueryBase` (all resolve via `GetReference`/a `ResolveGeoColumn`
twin, then delegate):

- `AddGeometryValueToResultset(property, GeometryValueForm form = Wkb, alias, occurrence)` — projects the
  geometry value; **must expose the `Wkb`/`Native` form** (Native = server-side subquery operand).
- `AddGeometryScalarToResultset(SqlGeoFunctionId op, property, ...)` + the `AggFn` aggregate overload —
  Area/Length/Distance/etc as a **tuple** column.
- `AddGeometryScalarToOrderBy(op, property, SortDir, ...)` — "nearest" = order by `Distance`.
- `AddGeometryScalarToGroupBy(op, property, ...)`.
- **Whole-entity read (the wiring piece):** the `AddToResultset(entityType)` loop
  (`SelectEntitiesQueryBase.cs:141-156`) must detect a geometry column and project it via
  `AddGeometryValueToResultset(..., Wkb)` (i.e. `ST_AsBinary`) instead of the plain column, so the decorating
  `GeometryPropertyAccessor` receives portable WKB.
- **HAVING: no dedicated geo method** — mirror JSON, which has none. Geo aggregate HAVING goes through the
  generic `Having` + `SingleEntityQueryConditionBuilder.Raw(...)` on the aggregated expression. Document it.
- **NTS-module ergonomics:** object-typed operand overloads for the scalar/predicate operands live in the
  NTS module, as in Area 1.

**Contract / tests:**
- **DB-free AST** (`GeometryEntityProjectionSqlTest.cs`): assert `ST_Area(<alias>.shape)` projection, ORDER BY
  `ST_Distance(...)`, GROUP BY `ST_Area(...)` are **byte-identical** to their resultset render (the Phase-4
  `GeometryScalarExpr` single-render guarantee, needed for GROUP-BY matching); assert whole-entity read wraps
  the geo column in `ST_AsBinary` while non-geo columns stay plain; assert `Native` form emits the raw column
  (no `ST_AsBinary`), `DbType.Object`.
- **SpatiaLite behavioural** (`GeometryEntityProjectionSpatialiteTest.cs`): the playground's read tasks via
  the entity API — states by `ST_Area` DESC (values match source `AREA`), nearest-by-`Distance` top-N, GROUP
  BY region + `AVG(ST_Area)` — and whole-entity read of an entity with a geometry column (assert `Shape`
  populated). Native-form subquery-operand: entity select projecting one geometry `Native`, fed as a Area-1
  `GeoPredicateOf` subquery operand.
- **Acceptance** (`GeometryEntityProjectionAcceptanceTest.cs`, 5 engines): Area/Length/Distance values +
  GROUP BY count/avg, SRID-0 planar technique.

### (2) Delegate or re-implement

- **Per-property clause methods — delegate.** Resolve `(ColumnInfo, entity)` via `GetReference` (already
  ColumnInfo-returning here — no new resolver), then call the Phase-4
  `SelectQueryBuilder.AddGeometryValueToResultset` / `AddGeometryScalarTo{Resultset(+AggFn),OrderBy,GroupBy}`
  (`SelectQueryBuilder.cs:520-664`). Register CLR result types in `mResultsetTypes` exactly as the JSON
  methods do (`:553-585`) so the reader returns the right scalar type; `Count` → `typeof(int)`.
- **Whole-entity read loop — re-implement a small geo branch.** The current loop
  (`AddToResultset(entityType)`, `:141-156`) has no geometry awareness; add: if `ci.Geometry != null`, call
  `AddGeometryValueToResultset(..., Wkb)` for that column instead of the plain per-column `AddToResultset`.
  The read-back decode already works transparently — `GeometryPropertyAccessor.SetValue`
  (`GeometryPropertyAccessor.cs:44-53`) runs `FromWkb`, invoked by `SelectQueryToTypeBinder.Read:220` because
  the accessor's `PropertyType` is `byte[]`. **No decode-registration table needed** (unlike JSON, which
  registers a decode step) — the accessor is self-decoding.
- **Result shape — re-implement honesty (not code, contract).** Scalar/aggregate projections and GROUP BY
  queries are read via the query reader by alias, **not** materialized as entities; the API must not offer an
  `AddToResultset(entityType)` path on a grouped query (every selected column must be grouped/aggregated).
  Documented + enforced by test.
- **HAVING — nothing new (delegate to generic).**

### (3) Why

The pure-SQL geo select surface is complete and its single-render `GeometryScalarExpr` already guarantees
GROUP-BY match; the entity per-property methods are true thin wrappers because `GetReference` already carries
the `ColumnInfo`. The whole-entity loop is the one re-implementation, and it's tiny — a branch that swaps a
plain column projection for the `ST_AsBinary` form so the existing accessor can decode. The result-shape
rules are re-implemented as API contract because a tuple/aggregate genuinely is not a typed entity and the
SQL layer cannot pretend otherwise.

---

## Cross-cutting constraints carried into all three areas

- **NTS-module split** — object-typed operands (NTS `Geometry`) always via `Gehtsoft.EF.Geo.NetTopologySuite`
  extensions; core stays `byte[]`-only.
- **Oracle `Crosses`/`IsEmpty` throw** `FeatureNotSupported` at runtime — advertised uniformly, honored 7/8;
  assert in acceptance.
- **Planar, not metric** — `Distance`/`Area`/`Length` on lon/lat are degrees/deg² (`DWithin(0.4°)`), not
  meters; `geography` out of scope.
- **Genuinely absent (decision 12, no wrapper can conjure)** — spatial aggregates (`ST_Union`/`Collect`/
  `Extent`), mass UPDATE of a geo field, ORDER BY / GROUP BY on a raw (non-scalar) geometry value.

## Phase / increment plan (each ends green; commit only when the user asks)

- **Increment 1 — Area 2 value-wrap, PURE-SQL LAYER FIRST (Option A, bottom-up):** add the
  `column.Geometry != null` auto-wrap fallback to `InsertQueryBuilder.PrepareQuery` and
  `UpdateQueryBuilder.AddUpdateColumn`, emitting `mSpecifics.GeometryFunction(FromWkb, {param}/@col, srid)`.
  Verify at the pure-SQL layer: DB-free AST (exact per-driver SQL, explicit `SetColumnValueExpressions` path
  still overrides → re-run the Phase-4 exact-SQL suite to prove no drift) **+ SpatiaLite/5-engine value
  round-trip including a null geometry** (closes the pre-existing NULL gap). Entity path is NOT relied upon
  yet.
- **Increment 2 — Area 2 entity insert/update round-trip:** confirm `SaveEntity`/`GetUpdateEntityQuery`
  round-trip a geometry property (byte[] property + NTS-object property + nullable→null) purely by inheriting
  increment 1's auto-wrap (entity builders zero-touch). SpatiaLite round-trip + 5-engine acceptance.
- **Increment 3 — resolution seam** (prereq P-A): extend `IEntityInfoProvider` +
  `EntityQueryWithWhereBuilder`; unit-test the resolver returns `(ColumnInfo, entity)` for a geo property;
  full suite green (no SQL drift).
- **Increment 4 — Area 1 WHERE**: core byte[] `GeoPredicateOf`/`GeoScalarOf` + subquery overload; NTS-module
  object overloads; DB-free AST + SpatiaLite + acceptance; then mass-delete-by-geo tests.
- **Increment 5 — Area 3 SELECT clauses**: per-property projection/order/group + whole-entity read branch +
  Native form + HAVING-via-generic; DB-free AST + SpatiaLite + acceptance; document result-shape rules.
- **★ ENTITY SURFACE COMPLETE** when all three areas are green on SpatiaLite + all 5 server engines.

## Gate decisions — ✅ ALL LOCKED (2026-07-22)

1. **Value-wrap placement → Option A** (pure-SQL auto-wrap keyed on `column.Geometry`, additive fallback,
   explicit expression still overrides), **built bottom-up: prove it at the pure-SQL layer first** (incl. the
   null round-trip) before the entity path relies on it. Justified by the autoincrement metadata-driven
   precedent in the same insert loop.
2. **Resolution seam → P-A** (extend `IEntityInfoProvider` with a `(ColumnInfo, QueryBuilderEntity)`-returning
   resolver; JSON alias-only path untouched).
3. **Scope → confirmed:** mass-update of a geo field stays out (decision 12); HAVING has no dedicated geo API
   (generic `Having` + raw aggregated expression only).

Both framework limitations are honored throughout: (i) main EF package holds **no** driver knowledge — all
per-driver geo SQL is a `SqlLanguageSpecifics.GeometryFunction`/`GeometryPredicate` override in the driver
assembly; (ii) **no hand-written SQL** — every wrap/predicate is emitted through the query builder +
specifics.

## Testing model (mirrors Phase 4 throughout)

- DB-free AST (`.ParseSql()`, exact render) — runs everywhere, via `DummyDbSpecifics` + `SupportsGeometrySpec`.
- SpatiaLite behavioural — real datasets in `Gehtsoft.EF.Test/GeoTestData/` and the playground entities.
- Acceptance — every live server engine (`[Theory]` over connection names), SRID-0 planar technique.
- Tests under `Gehtsoft.EF.Test/Geo/{DataManagement,DataSelecting}/` (default compile items).
