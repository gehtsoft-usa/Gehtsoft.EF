# Dynamic Properties in LINQ — feasibility analysis

*Analyzed 2026-07-06. Not started; no gate opened. LINQ support was explicitly out of scope for
v1 (`DYNAMIC_PROPERTIES_PLAN.md` §"Out of scope / v2"). This doc assesses whether/how it can be
added and at what cost.*

## How the LINQ layer works today

- `EntityQueryLinqConnectionExtension` → `QueryableEntityProvider` turns a LINQ expression tree into a
  `SelectEntitiesQuery`/`SelectEntitiesQueryBase` and executes it.
- Each clause lambda is compiled to a **raw SQL expression string** and fed to the same builder sinks
  the manual API uses:
  - `Where`   → `ExpressionCompiler` → `Where.Add(LogOp.And, expr)`
  - `OrderBy` → `AddOrderBy(expression, dir)`
  - `GroupBy` → `AddGroupBy(expression)`
  - `Select`  → `SelectExpressionCompiler` → `AddToResultset(expression, alias)` (bodies compiled via
    `ExpressionCompiler`; see `EntityQueryLinqExtension` line ~50).
- **`ExpressionCompiler`** (864 lines, central to every LINQ query) walks the tree:
  - a **member access** on an entity property resolves to its column alias via `IsQueryPath` →
    `mQuery.GetReference(...)`;
  - the **bare entity parameter** `e` resolves to that entity's **PK column alias**
    (`entityN.pk`) — see the `ExpressionType.Parameter` branch (line ~208);
  - a **method call** goes to `ProcessCall`, which special-cases
    `Method.DeclaringType == typeof(SqlFunction)` (line ~494) to force server-side translation.
- **`SqlFunction`** is the established extension mechanism: static marker methods
  (`Upper`, `Like`, `Sum/Min/Max/Avg`, `Count`, `In/NotIn/Exists`, `Value`, …) recognized by name in
  `ProcessCall`. The pure "server-only" ones throw `InvalidOperationException` if executed locally.
- Parameters: constants become bound params; **the DbType is taken from `value.GetType()`**
  (`EntityQueryLinqExtension` line ~42) — there is no per-parameter type override hook.

## The fit: correlated scalar subquery, not the Phase-3 JOIN

Phase 3 implemented dynamic properties in the manual query surface with a **LEFT JOIN** to the side
table. That does **not** map onto LINQ: the compiler builds one scalar SQL fragment per subexpression
and never mutates the FROM/join set mid-expression.

But a dynamic property reads perfectly as a **correlated scalar subquery**:

```sql
(SELECT tp.<value_col> FROM <owner>_props tp WHERE tp.owner = <entityAlias>.<pk> AND tp.name = @n)
```

— a scalar expression that composes with any operator and any clause. This is exactly the rev-2
approach Phase 3 set aside in favour of the JOIN. So **LINQ support is a third translation of the
same concept**, and it slots into the compiler with essentially one new recognized function.

## Proposed surface

A marker `SqlFunction.DynamicProperty<T>(entity, "name")` (throws locally, like `Sum`/`Value`); `T`
selects the value column + type:

```csharp
q.Where(e   => SqlFunction.DynamicProperty<string>(e, "color") == "red");
q.OrderBy(e => SqlFunction.DynamicProperty<double>(e, "income"));
q.GroupBy(e => SqlFunction.DynamicProperty<string>(e, "occupation"));
q.Select(e  => new { e.Id, Color = SqlFunction.DynamicProperty<string>(e, "color") });
// aggregate: SqlFunction.Sum(SqlFunction.DynamicProperty<double>(e, "income"))
```

(An alternative surface — recognizing `e.DynamicProperties.Get<T>("x")` / an indexer — reads more
naturally but needs special-casing in member resolution before the normal column lookup; the
`SqlFunction` route matches the existing idiom and is simpler. Recommend `SqlFunction.DynamicProperty`.)

## Implementation sketch

One branch in `ExpressionCompiler.ProcessCall` for `Method.Name == "DynamicProperty"`:
1. `Visit(arg0)` (the entity parameter) → the owner **PK alias** expression.
2. Read the name constant from `arg1`; bind it as a param (add to `Result.Params`).
3. Map `T` → value column id + DbType (reuse Phase-3's `DynamicPropertyProjection.ColumnIdFor` /
   the value-type mapping).
4. Emit the correlated subquery string into `Result.Expression` with a fresh side-table alias.

Because Where/OrderBy/GroupBy/Select all route the compiled string through the same sinks, **one
branch covers every clause**. Aggregates work by nesting inside `SqlFunction.Sum(...)` etc., which
already wrap their argument's expression.

## Two real wrinkles

### 1. Read-side decode (projection)
A projected dynamic property arrives **encoded** (DateTime→ticks, bool→0/1). Phase 3 solved this in
`SelectEntitiesQueryBase.BindOneDynamic` with a per-column decode registry. The LINQ materialization
path (`QueryableEntityProvider.CreateType`/`ReadOneValue`/anonymous-type binding) is **different
code** and would need its own decode-by-declared-`T` hook (or a way to register the compiled column's
type so the existing registry applies). Needed only for `Select` projections; filtering/sorting/
grouping don't read the value back.

### 2. Value encoding for `bool` & `DateTime` (the hard part)
LINQ binds the compared constant as its CLR type (`value.GetType()`). For **string/int/long/double**
the value maps directly to the column and comparisons are correct. For **bool** (0/1) and
**DateTime** (ticks) the subquery yields an encoded `int64` while the constant binds as bool/DateTime
→ type mismatch / wrong result. The scalar subquery cannot reach across the operator to encode the
other operand. Options:

- **A — restrict v1 to string/int/long/double.** Document bool/DateTime as unsupported in LINQ (the
  four direct types cover most real filters, sorts and aggregates). Lowest effort, no correctness risk.
- **B — encode in the subquery output.** Not portable (ticks→SQL datetime differs per driver; no
  portable SQL bool). Rejected.
- **C — pattern-match the comparison** in `ProcessBinary`: when one side is
  `DynamicProperty<DateTime|bool>`, encode the other side's constant (ticks / 0-1) at bind time.
  Correct semantics, but invasive (touches the generic binary/parameter path) and only works when the
  other side is a constant/param.
- **D — typed helpers** (`DynamicPropertyDate`, dedicated compare helpers). More surface.

Recommendation: **A now, C later** if demand appears.

## Effort / risk

- **Core** (WHERE / ORDER BY / GROUP BY / aggregate for the four direct types): moderate — one
  focused branch in `ExpressionCompiler` + tests. Caution: `ExpressionCompiler` is central to *every*
  LINQ query, so strong regression tests around it are mandatory (blast radius).
- **Projection decode**: extra work in the LINQ read path (wrinkle 1).
- **bool/DateTime**: a separate, larger step (wrinkle 2, option C).

## Recommendation

Feasible and reasonably clean via a single `SqlFunction.DynamicProperty<T>` marker + one
`ProcessCall` branch, reusing the **correlated-subquery** form (the JOIN doesn't fit LINQ). Treat as a
**Phase 4, plan-first**, scoped initially to **WHERE / ORDER BY / GROUP BY / aggregates over
string/int/long/double**; carry **projection-decode** and **bool/DateTime encoding** as explicit,
separately-gated sub-tasks. Do not code before a start gate.

## Concrete anchors (so we don't re-map the compiler next time)

All under `Gehtsoft.EF.Db.SqlDb/EntityQueries/Linq/` unless noted. Line numbers are approximate
(as of 2026-07-06 / v1.9.7) — search by symbol if they've drifted.

- **`ExpressionCompiler.cs`** (the WHERE/scalar compiler; central to *every* LINQ query):
  - `Visit(Expression)` ~L88 — the dispatch: `Constant` ~L90, `Member` ~L115, `Binary` ~L190,
    `MethodCall` → `ProcessCall` ~L204, **`ExpressionType.Parameter` ~L208** (bare `e` →
    `entityAlias.pk`, exactly the owner reference the correlated subquery needs).
  - `Result` inner class ~L23 — carries `Expression` (SQL string) + `Params` (bound params);
    ctor `Result(specifics, name, value)` ~L35 emits `@leqN` and records the param.
  - `ProcessBinary` ~L290 — builds `(left op right)`; **the place option C would encode a
    bool/DateTime constant** when the sibling operand is a `DynamicProperty<…>` call.
  - `IsQueryPath` ~L400 — entity-property → column resolution (dynamic props are *not* here; they
    must come through `ProcessCall`).
  - **`ProcessCall` ~L465**, `SqlFunction` dispatch guard `Method.DeclaringType == typeof(SqlFunction)`
    **~L494** — **add the `nameof(SqlFunction.DynamicProperty)` branch here**. The `In`/`Value`
    branches (~L502–L690) show how to embed a sub-query string and bind params from inside a call.
- **`SqlFunctions.cs`** — add `public static T DynamicProperty<T>(object entity, string name) =>
  ThrowNotUseLocally();` (marker style, like `Sum`/`Value`, L43/L134).
- **`EntityQueryLinqExtension.cs`** — param binding **L42** `BindParam(..., value, value.GetType())`
  (⇒ no encoding hook — root cause of wrinkle 2); compiled-expression → resultset **L50**
  `AddExpressionToResultset(result.Expression, result.HasAggregates, DbType.Object, expr.Type, alias)`.
- **`QueryableEntityProvider.cs`** — clause routing: `OrderBy` **~L193** (`AddOrderBy(expr, dir)`),
  `GroupBy` **~L207** (`AddGroupBy(expr)`), `Select` **~L316** (`AddToResultset(expr, "value")`),
  `Where` **~L324** (`Where.Add(LogOp.And, expr)`); **read/materialization** `ReadOneValue` ~L339,
  `CreateType` ~L344 (**where projection-decode / wrinkle 1 must hook**).
- **Reuse from Phase 3** (`EntityQueries/`): `DynamicPropertyProjection.ColumnIdFor(type)` (value
  column id per `DynamicPropertyValueType`), `DynamicPropertiesValueMapper.Encode/Decode`,
  `DynamicPropertiesTableBuilder` column-id/name constants, `EntityDescriptor.DynamicPropertiesTable`
  (the synthesized side-table descriptor + its `owner`/`name` columns and PK).
- **Test entry points**: `Gehtsoft.EF.Test/Entity/Linq/` (`LinqUnit.cs`, `LinqOnDB_Select.cs`,
  `SelectCompiler.cs`) — existing LINQ tests to mirror for the new ones.
