# GEO — entity-level API review (research note, 2026-07-20)

*Design review of the geo entity-query surface (Phases 5–7), informed by the practical select
playground (`Gehtsoft.EF.Test/Geo/GeoPlaygroundSpatialiteTest.cs`) and the pure-SQL surface committed
in Phase 4 + the native-form/subquery-operand amendment. Answers three questions: (1) which special
interfaces are needed at entity level, (2) what works transparently, (3) which logical conflicts survive
or cannot ride the underlying SQL layer. **Not yet turned into phase plans — resume here next session.***

## What each practical request exercises

| Request | SQL surface used | Result shape |
|---|---|---|
| which state is a city in | WHERE `Contains(shape,@p)` | entities (States) |
| states by area | project `ST_Area` + ORDER BY it | tuples (abbrev + number) |
| track crosses state | WHERE `Intersects(shape,@p)` | entities |
| nearest city | ORDER BY `Distance(center,@p)` + limit | tuples |
| within distance | WHERE `DWithin` | entities |
| states per region, avg area | GROUP BY mapped col + `AVG(ST_Area)` | aggregate rows |
| crossed-by-subquery (new) | WHERE `Intersects(shape,(subquery→native))` | entities |

All six were validated live on SpatiaLite in the playground, so the pure-SQL layer already covers every
practical request; Phases 6–7 are thin wrappers + two wiring pieces + honest result-shape handling.

## 1) Special interfaces needed at entity level

Thin "resolve-property-then-delegate" wrappers, mirroring JSON's `EntityQueryConditionBuilder.JsonPropertyOf`
and `SelectEntitiesQueryBase.AddJsonValueToResultset`:

- **Entity WHERE predicates/scalars** — `EntityQueryConditionBuilder.GeoPredicateOf(property, op, operand[, distance])`
  and `GeoScalarOf(property, fn, …)`, string-property + member-expression forms → delegate to
  `ConditionBuilder.GeoPredicate`/`GeoScalar`. (Requests 1, 3, 5.)
- **Entity scalar projection + order/group/agg** — `SelectEntitiesQueryBase.AddGeometryScalarTo{Resultset,OrderBy,GroupBy}`
  (+ `AggFn` overload) → delegate to `SelectQueryBuilder.AddGeometryScalarTo*`. (Requests 2, 6, nearest.)
- **Project the geometry value into a tuple** — `AddGeometryValueToResultset(property, GeometryValueForm)` →
  the output-wrap; must expose the `Wkb`/`Native` form (see §3).
- **Comparison-geometry channel** — predicate entry points take the operand *by value* and bind it internally
  under a generated param name (user never manages `@p`). **Core takes `byte[]` WKB; the ergonomic
  `NetTopologySuite.Geometry` overloads live in the NTS module** (core must never reference NTS — hard rule),
  exactly like `GeometrySqlExtensions.BindGeometryParam`.

Two internal **wiring** pieces (not new public API but required):
- **Write:** inject the `ST_GeomFromWKB` wrap into entity INSERT / single-UPDATE (Phase 5).
- **Read (whole entity):** the whole-entity resultset builder (`SelectEntitiesQueryBase.AddToResultset(entityType)`
  loop, ~line 147) must detect a geometry column and wrap it in `ST_AsBinary` so the decorating accessor
  receives portable WKB. **A geo branch JSON never needed** — JSON's DB column is a plain string, but a raw
  geometry column is engine-specific (SpatiaLite modified-WKB) and cannot be read raw.

## 2) What works transparently (no geo-specific user code)

- All non-geo columns, everywhere.
- **Whole-entity INSERT / single-UPDATE incl. the geometry value** — `UpdateQueryToTypeBinder` already binds
  the accessor's `byte[]`; once the `FromWkb` wrap is wired, `SaveEntity(state)` just works. Nullable geometry
  ↔ SQL NULL automatic.
- **Reading whole entities incl. geometry** — once the read-wrap is wired, `State.Shape` is populated (NTS
  `Geometry` for an object property, `byte[]` for a `byte[]` property); accessor decodes. Both shapes transparent.
- **Filter / ORDER BY / GROUP BY on *mapped scalar attributes*** — sharp contrast: **"states by area" using the
  stored `AREA` column is 100% transparent, zero geo API.** Only `ST_Area(shape)` (the computed value) needs the
  projection API. Same for grouping by `REGION`.

## 3) Logical conflicts / limits that survive or can't ride the SQL layer

**Result-shape tension (must be surfaced in the API):**
- Scalar/aggregate projections ("states by `ST_Area`", "`AVG(area)` per region", "nearest") are **tuples/aggregate
  rows, not typed entities** — read via the query reader by alias (as the playground does). The entity API cannot
  pretend they materialize entities.
- **GROUP BY is incompatible with the auto-select-all-columns whole-entity read** (every selected column must be
  grouped or aggregated) → grouped/aggregate geo queries must use explicit projection, never `AddToResultset(entityType)`.

**Mode conflict (the Wkb/Native split, now at entity level):**
- Reading a geometry *for the client* (WKB) vs using an entity's geometry *as a server-side subquery operand*
  (native) are **different modes of the same select**; the entity API must expose the form. The subquery-operand
  request needs a way to build an entity select that projects **one** geometry column in **native** form — the
  ordinary whole-entity/WKB read cannot serve as the operand.

**Structural:**
- **Core can't reference NTS** → the ergonomic object-based entity API (pass an NTS `Point`, predicates typed on
  `Geometry`) must live in the **NTS module as extensions**; the core entity API is `byte[]`-only. The "nice" API is
  deliberately split across two assemblies.
- **Entity WHERE welded to one statement** (`ENTITY_WHERE_PROBLEM.md`): entity **mass-delete / mass-update by a
  spatial filter** inherits the EAV/JSON text-splice workaround; the clean fix (detachable, copyable predicate) is
  still deferred.

**Capability/semantic non-uniformity (SQL layer can't hide these):**
- **Oracle `Crosses` and `IsEmpty` throw** — API advertises 8 uniform predicates but Oracle honors 7; runtime
  `FeatureNotSupported`, not a compile-time signal.
- **Planar semantics** — `Distance`/`Area`/`Length` on lon/lat are planar (degrees / deg²), not meters ("within
  ~44 km" is expressed as `DWithin(0.4°)`); `geography` is out of scope. Ordinal "nearest" is correct, metric
  distances are not available.

**Genuinely NOT implementable via the underlying SQL layer** (deliberately absent there — decision 12 — so no
entity wrapper can conjure them):
- **Spatial aggregates** (`ST_Union`/`Collect`/`Extent`): can GROUP BY region + `AVG(area)`, but **"the merged
  boundary of each region" is not expressible.**
- **Mass UPDATE of a geo field.**
- **ORDER BY / GROUP BY on a raw geometry value.**

## Bottom line / next-session actions

- Phases 6–7 = thin entity wrappers + the two wiring pieces (write `FromWkb`, read `ST_AsBinary`) + honest
  result-shape handling. Only new *capability* work = object-typed ergonomics in the NTS module.
- Irreducible gaps = the three decision-12 exclusions + planar-vs-metric semantics.
- **TODO next session:** fold this into the Phase 5 plan (add "result shape & two-form read" + NTS-module
  ergonomic API to the gate) and draft Phase 6/7 plans from §1. Phase 5 gate decision still pending
  (value-wrap placement: Option A auto-wrap vs Option B entity-layer — leaning B per the value-source-form
  reasoning in `PHASE_5/PHASE_5_PLAN.md`).
