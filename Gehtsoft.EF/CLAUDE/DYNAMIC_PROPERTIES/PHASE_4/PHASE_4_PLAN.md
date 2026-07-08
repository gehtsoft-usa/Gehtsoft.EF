# PHASE 4 — Dynamic properties in the LINQ query surface

> **Status (2026-07-08): Phase 4a IMPLEMENTED + green on all 6 drivers.** WHERE + projection +
> aggregate over all six value types (incl. bool encode-on-compare and DateTime encode/decode).
> Tests: `Gehtsoft.EF.Test/DynamicProperties/Linq/DynamicPropertiesLinqTest.cs`.
>
> **Two follow-ups added on NG's request (also in 4a):**
> 1. **Whole-entity preload is ON by default.** `GetCollectionOf<T>(bool preloadDynamicProperties =
>    true)` — a whole-entity LINQ select (no projection) loads and attaches each entity's bag
>    (batch for `ToList`, batch-path for `First`); pass `false` to opt out. No-op for types without
>    dynamic properties (`Execute` gates on `HasDynamicProperties`, so non-dynamic entities keep the
>    unchanged `ReadOne` path). Ignored for projections.
> 2. **Discovery guardrail** (`AllEntities.ResolveDynamicProperties`): `[DynamicProperties]` and
>    `IDynamicPropertiesOwner` must be declared together — attribute-without-bag and
>    bag-without-attribute each throw `EfSqlException` at discovery (codes
>    `DynamicPropertiesAttributeWithoutOwner` / `DynamicPropertiesOwnerWithoutAttribute`). Required
>    adding the bag to ~11 attribute-only test entities (TableManagement/Bson/Recognition) — table
>    shape is unaffected (the bag is not a mapped column).
>
> Regression: full DynamicProperties suite (623) + all Entity tests (1565) green.
> **Caveat found:** the compiler reads a `static`-field reference as a `MemberExpression` with a
> null `.Expression` and NREs on it (pre-existing limitation, not specific to dynamic properties) —
> compare against locals/literals, not static fields.
>
> **Phase 4b DONE (all 6 drivers):** ORDER BY and GROUP BY by a dynamic property (incl. the group-key
> projection `g.Key` and aggregates over a dynamic property within a group). The only change was in
> `SelectExpressionCompiler` (the clause collector), which previously dropped non-member-access
> ORDER BY bodies and threw on non-member-access GROUP BY keys — both now also accept a `Call` node
> (the `Get<T>` expression), which then flows through the 4a `ExpressionCompiler` branch. No change
> needed in `QueryableEntityProvider` (the `g.Key` projection is still a member access; the stored key
> *expression* is the `Get` call and flows through `AddGroupBy`/`AddToResultset`). Tests added to
> `DynamicPropertiesLinqTest`. **Deferred:** OrderByDescending/ThenBy (LINQ layer only does OrderBy
> asc), multi-occurrence owners.


*Planned 2026-07-08. Phase 3 (manual free-form query surface: projection / ORDER BY / GROUP BY /
HAVING / aggregate / WHERE-opt, all JOIN-based) is DONE on all 6 drivers. This phase makes a dynamic
property usable from the **entity LINQ** surface (`QueryableEntity<T>` / the `ExpressionCompiler`
clause path).*

Prerequisite reading: `../LINQ_ANALYSIS.md` (feasibility + compiler anchors). This plan supersedes
two of its conclusions after a closer read of the code — see "Revisions to the feasibility doc".

---

## Decisions locked with NG (2026-07-08)

- **D1 — Surface: `e.DynamicProperties.Get<T>("name")`.** Not `SqlFunction.DynamicProperty<T>`.
  It reuses the real runtime bag method (`DynamicPropertyBag.Get<T>`), `T` carries the value type,
  and there is no new public API to document.
- **D2 — Translation: reuse the Phase-3 JOIN.** The new compiler branch calls
  `DynamicPropertyProjection.EnsureJoin(select, …)` on the **live** `SelectEntitiesQueryBase` and
  emits `join.ColumnAlias`. **Not** a fresh correlated sub-query. (The feasibility doc's "sub-query,
  not JOIN" was based on a wrong premise — see below.)
- **D3 — Scope of this phase = WHERE + projection (`Select`) + aggregate, over ALL SIX value types**
  (`string / int / long / double / bool / DateTime`). This folds the feasibility doc's "4a" and "4c"
  together: type completeness across the clauses that pass through cleanly, including the
  encode/decode work bool+DateTime need.
- **D4 — Deferred to a later phase (4b): ORDER BY and GROUP BY.** They are blocked *upstream* of the
  compiler (in `SelectExpressionCompiler`, the clause collector) and need separate surgery there and
  in `QueryableEntityProvider` — see "Why ORDER BY / GROUP BY are out of scope".
- **D5 — Occurrence 0 only** (`e.DynamicProperties…`, the sole owner in the lambda). Multi-occurrence
  (array-indexed owners) deferred; not expressible naturally in the LINQ surface anyway.

---

## Revisions to the feasibility doc

1. **The JOIN *does* fit LINQ.** The doc claimed "the compiler builds one scalar fragment per
   subexpression and never mutates the FROM/join set." The `ExpressionCompiler` is in fact
   constructed with the **live `SelectEntitiesQueryBase`** (`EntityQueryLinqExtension.cs:48,77,93`),
   so the new branch can call the existing `EnsureJoin` and mutate the FROM set exactly like the
   manual API. Reusing the JOIN inherits Phase-3's join dedup, value-column mapping, and 6-driver-
   tested SQL — strictly better than a second sub-query dialect.

2. **The two "wrinkles" nearly collapse.** For `string/int/long/double`, projection decode and
   comparison encoding are both **no-ops**: `GetValue(i, declaredType)` → `TranslateValue`
   (`SqlQuery.cs:679`) reads `v_str`/`v_int`/`v_real` directly, and the CLR constant compares
   directly. Only **bool** (0/1) and **DateTime** (UTC ticks) need encode-on-compare and
   decode-on-read. Because NG chose full type coverage (D3), this phase carries that bool/DateTime
   work — but it is a single, well-contained mechanism (see below), not the open-ended risk the doc
   implied.

3. **New blocker the doc missed: `SelectExpressionCompiler` filters ORDER BY / GROUP BY to
   member-access bodies** *before* the `ExpressionCompiler` ever runs. See D4.

---

## Surface & tree shape (D1)

```csharp
q.Where (e     => e.DynamicProperties.Get<string>("color") == "red");
q.Select(e     => new { e.Id, Color = e.DynamicProperties.Get<string>("color") });
// aggregate:
q.Select(e     => new { Total = SqlFunction.Sum(e.DynamicProperties.Get<double>("price")) });
```

`e.DynamicProperties.Get<T>("x")` compiles to:

```
MethodCall  DynamicPropertyBag.Get<T>        // DeclaringType == typeof(DynamicPropertyBag), IsGenericMethod
 ├─ Object:  Member  .DynamicProperties       // Member == IDynamicPropertiesOwner.DynamicProperties
 │            └─ Parameter e                   // owner  → entity type + PK alias (occurrence 0)
 └─ Arg[0]:  Constant "x"                      // property name
```

---

## The `ExpressionCompiler` branch (core of the phase)

**Placement — critical.** Add the branch at the **very top of `ProcessCall`
(`ExpressionCompiler.cs:465`), before the argument-visit loop (line 474) and before the
"all args are params ⇒ evaluate locally" short-circuit (line 696).** Otherwise:
- visiting the object `e.DynamicProperties` falls through `IsQueryPath` (no such column) and is
  mis-treated as a bound parameter, and
- the line-696 short-circuit would try to run `e.DynamicProperties.Get("x")` **in memory** against a
  null bag → NRE.

Branch guard: `callNode.Method.DeclaringType == typeof(DynamicPropertyBag) &&
callNode.Method.Name == nameof(DynamicPropertyBag.Get) && callNode.Method.IsGenericMethod`.

Steps:
1. Object must be `MemberExpression`(member = `DynamicProperties`) over a `ParameterExpression`
   → entity `Type`, occurrence `0`. (Reject other shapes with a clear message.)
2. `T = callNode.Method.GetGenericArguments()[0]`; `underlying = Nullable.GetUnderlyingType(T) ?? T`;
   map to `DynamicPropertyValueType` (bool→Boolean, int→Integer, long→Long, double→Real,
   string→String, DateTime→DateTime). Reject unsupported `T`.
3. Read the name constant from `Arg[0]`.
4. Require `mQuery is SelectEntitiesQueryBase select` — else throw
   *"dynamic properties are only supported in LINQ SELECT queries"* (LINQ queryable is select-only,
   so this never triggers in practice; it guards the UPDATE/DELETE-condition reuse of the compiler).
5. `join = DynamicPropertyProjection.EnsureJoin(select, entityType, name, 0, valueType)`.
6. `res.Expression.Append(join.ColumnAlias)`; **tag `res` with the `DynamicPropertyValueType`** (new
   nullable field on `Result`, e.g. `DynamicPropertyType`) so `ProcessBinary` and the read path can
   encode/decode. Leave `IsParameterExpression == false`.

One branch → WHERE, `Select` projection and aggregate all work, because each routes the compiled
string through its existing sink and references the same joined column (dedup handled by
`EnsureJoin`).

---

## bool / DateTime — the encode/decode mechanism (D3)

Stored form: bool → `0/1` in `v_int`; DateTime → **UTC ticks** in `v_int`
(`DynamicPropertiesValueMapper.Encode/Decode`).

### Encode-on-compare (WHERE)
`ProcessBinary` (`ExpressionCompiler.cs:290`): when **one** operand's `Result` is tagged
`Boolean`/`DateTime` and the other is a param/constant `Result`, the constant must be bound in the
**encoded** form (`true→1L`, `date→UTC ticks`). Implementation:
- Add `EncodeAs` (nullable `DynamicPropertyValueType`) to `ExpressionCompiler.ExpressionParameter`.
- In `ProcessBinary`, set `EncodeAs` on the sibling param when the tagged operand is bool/DateTime.
- In `EntityQueryLinqExtension.BindExpressionParameters` (`:23`), after the value is resolved
  (constant or compiled closure), if `EncodeAs` is set, replace it with
  `DynamicPropertiesValueMapper.Encode(value).Value` before `BindParam`. Covers both literal
  constants and captured variables. (Root cause of the doc's "wrinkle 2" — the `value.GetType()`
  bind at `:42` — is fixed exactly here.)
- `== null` keeps its existing `IS NULL` rewrite (the null branch at `:346` runs before any encode).

### Decode-on-read (projection)
The LINQ read path (`QueryableEntityProvider.CreateType` `:344`, `ReadOneValue` `:339`) calls
`GetValue(i, declaredType)` and does **not** consult Phase-3's decode registry
(`mDynamicPropertyColumns`, read only by `BindOneDynamic`, which is the *entity*-materialization
path — mutually exclusive with the projection path in `CompileToQuery`). So:
- When the LINQ `AddToResultset` (`EntityQueryLinqExtension.cs:46`) sees a `Result` tagged with a
  `DynamicPropertyType`, register the resultset index in `mDynamicPropertyColumns` (reuse the
  existing private `AddDynamicPropertyColumn` bookkeeping, or expose an internal shim).
- Make `CreateType` / `ReadOneValue` consult that registry: registered index ⇒
  `DynamicPropertiesValueMapper.Decode(type, raw)`; otherwise the plain `GetValue`.
- For `string/int/long/double` this decode is a safe identity/normalization; it is *required* for
  DateTime (ticks→DateTime) and bool.

### Aggregate + type (watch item)
Mirror Phase-3's `AddDynamicPropertyToResultset(AggFn…)` (`SelectEntitiesQueryBase.cs:300`):
`Count` → `int`, no decode; other aggregates decode as the property type. **Explicitly test `Avg`**
of an integer/long property (result is fractional — must surface as `double`, not decode-truncated).
The LINQ aggregate type flows from `SqlFunction.Avg`'s generic arg (`SelectExpressionCompiler.cs:293`),
so confirm the projected type there and decode accordingly.

---

## Why ORDER BY / GROUP BY are out of scope (D4)

`SelectExpressionCompiler` (the clause *collector*, runs before `ExpressionCompiler`) filters bodies:
- **OrderBy** (`SelectExpressionCompiler.cs:86`): only `NodeType == MemberAccess` bodies are kept.
  A `Get<T>(…)` body is a **Call** → the order-by is **silently dropped**.
- **GroupBy** (`:119,138`): only member-access / new-of-members keys → a `Get<T>` key **throws**
  "Only member access is supported in group by key".
- `QueryableEntityProvider`'s group-key wiring (`:224-247`) is likewise member-access-shaped.

Supporting them means teaching all three sites to accept the dynamic-property Call shape — separate
surgery on the central LINQ path. Deferred to Phase 4b. WHERE / `Select` / aggregate are unaffected
(`SelectExpressionCompiler` passes those bodies through whole — `:67,148,178`).

---

## Semantics & documented limits

- **Absent property = SQL NULL**, not `default(T)`. LEFT JOIN yields NULL for owners without the
  property; `==`/`>`/`<` filters drop them, `!= x` also drops them (3-valued logic), `== null`
  matches them. This differs from the in-memory `bag.Get<T>` (which returns `default(T)`); document
  it. (Same behavior as Phase-2/3.)
- `(owner, name)` API-uniqueness (not DB-enforced) ⇒ the LEFT JOIN does not fan rows in practice —
  same caveat the feature already carries.
- Nullable `T` (`Get<int?>`) maps by underlying type.
- Occurrence 0 only (D5).

---

## Files touched

- **`Linq/ExpressionCompiler.cs`** — new top-of-`ProcessCall` branch; `Result.DynamicPropertyType`
  tag; `ExpressionParameter.EncodeAs`; encode wiring in `ProcessBinary`.
- **`Linq/EntityQueryLinqExtension.cs`** — `BindExpressionParameters` encode step; `AddToResultset`
  registers the decode index when the `Result` is tagged.
- **`Linq/QueryableEntityProvider.cs`** — `CreateType` / `ReadOneValue` consult the decode registry.
- **`EntityQuery/SelectEntitiesQueryBase.cs`** — expose an internal shim to register a projected
  dynamic-column index from the LINQ path (reuse `AddDynamicPropertyColumn` / `mDynamicPropertyColumns`).
- **Reuse unchanged:** `DynamicPropertyProjection.EnsureJoin`, `DynamicPropertiesValueMapper`,
  `DynamicPropertyValueType`, `DynamicPropertiesTableBuilder`.

## Tests (`Gehtsoft.EF.Test/DynamicProperties/Linq/`, all 6 drivers)

New namespace `Gehtsoft.EF.Test.DynamicProperties.Linq`, a sibling of the existing
`DynamicProperties.{Entities,TableManagement,DataManagement,DataSelecting}` (the test csproj
auto-includes new files — no `Compile Include`). Mirror the style of `Entity/Linq/LinqOnDB_Select.cs`
and reuse the dynamic-properties test entities/fixtures the Phase-2/3 selecting tests already use.
Per value type (string/int/long/double/bool/DateTime), and asserting
against the in-memory snapshot the way the existing LINQ tests do:
- **WHERE — string** (the primary EAV filter; these all compose over the emitted join column):
  `== "x"`, `!= "x"`, `== null` / `!= null` (present/absent), `SqlFunction.Like(…, "r%")`,
  `.StartsWith("r")`, `.ToUpper()`/`.ToLower()` in a comparison, and `.Length > n`
  (the `.Length` member routes through the `string`-member branch, `ExpressionCompiler.cs:166`,
  which visits the inner `Get<string>` and emits the column).
- **WHERE — numeric/date**: `==`, `!=`, `>`/`<`/`>=`/`<=`; `== null` / `!= null`.
- **WHERE — bool**: `== true` / `== false` (exercises the encode-on-compare path).
- **Projection**: single-value select and anonymous-type select; confirm bool/DateTime decode.
- **Aggregate**: `Sum`/`Min`/`Max` per numeric type; `Avg` of int/long → double (decode watch item);
  `Count`.
- **Regression guard**: a spread of existing non-dynamic LINQ queries (blast-radius check on the
  shared `ExpressionCompiler` / `ProcessBinary`).
- **Negative**: unsupported `T`; dynamic property in a non-SELECT compiler context throws clearly.
- Assert generated SQL via AST parsing where shape matters (per house rule), behavioural checks
  otherwise.

---

## Gates (per phase process)

1. **This plan** — approve before any code. ← we are here.
2. **Advance gate** — after the plan is approved, before moving on / committing.

Out of this phase, tracked separately: **Phase 4b** (ORDER BY / GROUP BY),
multi-occurrence owners.
