# Geo in LINQ — support analysis

*Analysis 2026-07-22 (branch `geo`, after the entity-level surface Phase 5 landed). Read `STATE.md`,
`PHASE_5/PHASE_5_PLAN.md`, `ENTITY_API_REVIEW.md` first.*

> **✅ IMPLEMENTED (2026-07-22, UNCOMMITTED).** The `byte[]`-operand baseline described below is built: a core
> `SqlSpatial` marker class + a geo branch in `ExpressionCompiler.ProcessCall` (delegating to
> `specifics.GeometryPredicate/GeometryFunction`), plus a two-line fix so `AddOrderBy`/`AddGroupBy` bind the
> operand param. Predicates (Intersects/Contains/Within/Disjoint/Touches/Overlaps/Crosses/SpatialEquals/
> DWithin) and scalars (Area/Length/Distance/X/Y) work in LINQ `Where`/`OrderBy`/`Select`/`GroupBy`. Verified
> DB-free (`GeometryLinqSqlTest`), on live SpatiaLite (`GeoPlaygroundLinqSpatialiteTest` — the playground
> re-expressed in LINQ) and on all 5 server engines (`GeometryLinqAcceptanceTest`, incl. Oracle `Crosses` →
> `FeatureNotSupported`). No driver/SQL changes; the optional NTS-object sugar was NOT built (byte[] operand,
> caller encodes with the codec). Ordering is ascending-only (a pre-existing LINQ-provider limitation, not
> geo-specific). The analysis below is retained as the design record.

## Question

Can a geometry (`[GeometryEntityProperty]`) field be used inside a **LINQ** query — a spatial predicate in a
`.Where(...)`, and a geometry scalar (Area/Length/Distance) in `.Where`/`.OrderBy`/`.GroupBy`/`.Select`?

## TL;DR

**Feasible, moderate effort, no new SQL.** The pure-SQL + entity query layers already render every geo
predicate/scalar (Phase 4 + Phase 5). What's missing is purely the **LINQ front-end**: the expression compiler
has no seam to recognise a geo method call and route it to the geo renderers. Geo must be **hand-wired into
`ExpressionCompiler` exactly as JSON and dynamic properties already are** — there is no registry/attribute
plugin point. The needed column metadata (`column.Geometry.Srid`) is reachable in the compiler today, and the
**whole-entity LINQ read of a geo entity already works** (it rides the Increment-5 geo-aware binder). The one
genuinely new design question is how to keep **core free of NetTopologySuite** while letting the operand be an
NTS object — solved by string-type dispatch + the existing codec abstraction (below).

---

## How LINQ works today (the relevant mechanics)

Entry: `connection.GetCollectionOf<T>()` → `QueryableEntity<T>` (`IOrderedQueryable`) →
`QueryableEntityProvider` (`IQueryProvider`). `CompileToQuery` drives a `SelectExpressionCompiler` over the
operator chain and an `ExpressionCompiler` over each lambda body. Files in
`Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/`.

Two dispatch points, **both closed hard-coded `switch`/`if-else` ladders that throw on anything unknown**:

1. **`SelectExpressionCompiler.Compile`** — outer chain, keyed on `Method.Name`
   (`Where`/`OrderBy`/`GroupBy`/`Select`/`Count`/`Sum`/`Min`/`Max`/`Average`/`Skip`/`Take`/`First`…).
2. **`ExpressionCompiler.ProcessCall`** — method calls inside a lambda, keyed on
   **`Method.DeclaringType` + `Method.Name`**. Recognises the `SqlFunction.*` marker methods
   (`SqlFunctions.cs`), a handful of native CLR methods (`string.ToLower`, `Math.Abs`, `ToString`…), the
   dynamic-property getter, and the JSON path. **Everything else throws
   `"Function {Type}.{Name} is not supported"`.** Each recognised branch emits SQL via
   `mSpecifics.GetSqlFunction(SqlFunctionId.X, args)`.

Key facts that make geo feasible:

- **Column metadata is reachable.** `e => e.Shape` resolves via `mQuery.GetItem(type, member)` to an
  `EntityQueryItem` exposing `item.Column` (`TableDescriptor.ColumnInfo`) and `item.QueryEntity`. The JSON
  branch already reads `item.Column.Json`; a geo branch reads `item.Column.Geometry` (incl. `.Srid`)
  identically. (Compiler currently only *emits* the alias, but the metadata is in hand.)
- **The render target already exists.** `SqlLanguageSpecifics.GeometryPredicate(GeoPredicateRequest)` /
  `GeometryFunction(GeoFunctionRequest)` (Phase 4) render every dialect. A geo branch calls these instead of
  `GetSqlFunction`. **No new SQL, no driver changes.**
- **Precedent = JSON + dynamic properties.** Both are column-type-specific LINQ features added by
  (a) a `Result` tag field, (b) a recogniser in `Visit`/`ProcessCall` (`TryResolveJson`,
  `ProcessDynamicPropertyGet`), (c) matching `Add*Expression*` write-side branches in
  `EntityQueryLinqExtension`. Geo follows the same three-part shape.

---

## The "NTS in core" concern — mostly a non-issue if the operand is `byte[]`

`ExpressionCompiler` / `SqlFunctions.cs` live in **core `Gehtsoft.EF.Db.SqlDb`**, which never references
`Gehtsoft.EF.Geo.NetTopologySuite`. That looks like a problem only if the marker method's operand is an NTS
`Geometry`. It is **not** a problem if the caller passes WKB `byte[]` and encodes at the call site:

```csharp
var point = new Point(-74, 40.7) { SRID = 4326 };
var hits  = q.Where(s => SqlSpatial.Contains(s.Shape, codec.ToWkb(point))).ToList();
```

With this shape everything stays trivial and NTS-free in core:

- **`SqlSpatial` lives in core**, its methods take `byte[]`, and the compiler dispatches on plain
  `typeof(SqlSpatial)` — the same mechanism as `SqlFunction`, **no string/namespace matching**.
- **The operand needs no special handling.** `codec.ToWkb(point)` is an all-constant subexpression (`codec`
  and `point` are captured locals), so the compiler's existing *"all arguments are constants ⇒ evaluate the
  subtree locally"* path runs it and yields a ready `byte[]`, bound as a normal `Binary` parameter. The geo
  branch only wraps that param in `FromWkb(@p, column.Geometry.Srid)`.
- **The only NTS touch is `codec.ToWkb(point)` at the call site** — app code, where NTS already lives. Core
  sees a `byte[]`, never an NTS type.

So the geo branch's job shrinks to: recognise `SqlSpatial.*` (by `typeof`), resolve `s.Shape` → column (to
read `column.Geometry.Srid`, the way `TryResolveJson` reads `column.Json`), and emit
`specifics.GeometryPredicate/GeometryFunction`. No geometry-type detection, no codec call inside the compiler,
no NTS reference. The feature stays entirely behind the `byte[]` abstraction.

**Optional sugar (deferrable):** an NTS-object overload `SqlSpatial.Contains(s.Shape, point)` — dropping the
explicit `codec.ToWkb(...)` — would have to live in the NTS module, which *would* reintroduce
`typeof`-vs-FullName-string dispatch and a geometry-constant encode step in the compiler. That's the only place
the "core can't reference NTS" tension bites, and it buys just a few characters of call-site sugar. **Recommend
shipping the `byte[]`-operand form first; treat the NTS-object overload as optional/later.**

---

## Proposed API shape (illustrative, not final)

Marker class `SqlSpatial` in **core** with `byte[]` operands (caller encodes with the app's codec). The
methods just throw "server-side only" like `SqlFunction`.

```csharp
var q = connection.GetCollectionOf<PgState>();

// predicate in Where (operand is byte[]; codec.ToWkb runs locally at compile time)
var ncStates = q.Where(s => SqlSpatial.Contains(s.Shape, codec.ToWkb(cityCentre))).ToList();
var corridor = q.Where(s => SqlSpatial.Intersects(s.Shape, codec.ToWkb(track))).ToList();
var near     = q.Where(c => SqlSpatial.DWithin(c.Center, codec.ToWkb(track), 1.0)).ToList();

// scalar in Where / OrderBy / Select
var big      = q.Where(s => SqlSpatial.Area(s.Shape) > 500).ToList();
var nearest  = cities.OrderBy(c => SqlSpatial.Distance(c.Center, codec.ToWkb(probe))).Take(3).ToList();
var areas    = q.Select(s => new { s.Abbrev, Area = SqlSpatial.Area(s.Shape) }).ToList();

// group-by aggregate
var perRegion = q.GroupBy(s => s.Region)
                 .Select(g => new { Region = g.Key, Avg = g.Average(s => SqlSpatial.Area(s.Shape)) });
```

(Optional later sugar in the NTS module: `SqlSpatial.Contains(s.Shape, cityCentre)` dropping the explicit
`codec.ToWkb(...)` — see the design note above; not required for a first cut.)

**Whole-entity LINQ read already works.** `q.Where(...).ToList()` returning whole `PgState` entities
materialises through `SelectEntityQueryBuilder.CreateBinder`, which Increment 5 made geo-aware (geo columns
projected via `ST_AsBinary`, decoded by the accessor). So reading geo entities via LINQ needs nothing new —
**only the geo *expressions* in Where/OrderBy/etc. need the compiler work.** *(Confirm with a quick test:
`GetCollectionOf<GeoEntity>().Where(e => e.Id > 0).ToList()` and assert the geometry property is populated.)*

---

## Work required (mirrors the JSON/dynamic-property precedent)

1. **Marker methods** — `SqlSpatial` in the NTS module (predicates: Intersects/Within/Contains/Disjoint/
   Equals/Touches/Overlaps/Crosses/DWithin; scalars: Area/Length/Distance/X/Y/…). Optionally a core `byte[]`
   twin. Bodies just throw "server-side only" (like `SqlFunction`).
2. **`ExpressionCompiler`** — a `Result.GeoValueType`-style tag + recognisers:
   - in `ProcessCall`: detect the geo marker (by FullName string), resolve the geo column via
     `mQuery.GetItem(...).Column.Geometry`, encode the operand via `GeometryCodecs.Resolve()`, and emit
     `mSpecifics.GeometryPredicate(...)` (predicate → complete boolean condition) or `GeometryFunction(...)`
     (scalar → operand for a following comparison / projection).
   - geometry `ConstantExpression` handling (codec encode → Binary param).
3. **`EntityQueryLinqExtension`** — geo branches in `AddToResultset`/`AddOrderBy`/`AddGroupBy` (like the JSON
   `AddJsonExpression*` branches) so a projected/ordered/grouped geo scalar renders byte-identically.
4. **`SelectExpressionCompiler`** — no change for predicates in `Where` (bodies pass through). A top-level
   geo scalar aggregate would need a branch only if used as a bare terminal operator.
5. **Tests** — DB-free compile assertions (mirror `Entity/Linq/SelectCompiler.cs`) + a live SpatiaLite +
   5-engine acceptance run; ideally re-express the entity playground tasks in LINQ.

**Effort:** comparable to the JSON LINQ work — bounded to `ExpressionCompiler.cs` + `EntityQueryLinqExtension.cs`
+ a new marker class + tests. No driver/pure-SQL changes.

## Constraints & out-of-scope (unchanged from the entity surface)

- Oracle `Crosses`/`IsEmpty` throw `FeatureNotSupported` at render time (would surface as the LINQ query
  throwing on Oracle — advertise/test).
- Planar, not metric (Distance/Area on lon/lat are degrees).
- Decision-12 gaps remain absent (spatial aggregates ST_Union/Collect/Extent, mass geo UPDATE, ORDER/GROUP BY
  on a raw geometry value).
- **Result-shape rule** carries over: a LINQ `.Select` projecting a geo scalar/aggregate returns a
  tuple/anonymous type, never the whole entity (the compiler already builds a generic select for projections).

## Recommendation

Support is worth adding and is a clean mirror of the existing JSON/dynamic-property LINQ features, with **no
new architectural tension** once the operand is `byte[]` (caller-encoded): a core `SqlSpatial` marker class
dispatched by `typeof`, exactly like `SqlFunction`. The much-discussed "core can't reference NTS" issue only
appears if we add the optional NTS-object overload, which can be deferred. If approved, phase it as: (1)
`SqlSpatial` marker class + predicate recogniser in `ExpressionCompiler.ProcessCall` + Where tests; (2) scalar
recogniser + OrderBy/GroupBy/Select write-side branches; (3) live SpatiaLite + 5-engine acceptance + a LINQ
re-expression of the playground; (4) optional NTS-object sugar overload.
