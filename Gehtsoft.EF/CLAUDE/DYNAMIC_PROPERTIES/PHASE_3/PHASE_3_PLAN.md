# PHASE 3 — Dynamic properties in the free-form query surface (`SelectEntitiesQueryBase`)

*Planned 2026-07-06. Phase 2 (CRUD + WHERE-filter + load) is DONE on all 6 drivers.
This phase makes a dynamic property a **projectable / orderable / groupable / aggregatable /
having-filterable** value in a custom entity query, and optimizes the existing WHERE path when
the property has already been joined.*

Goal (NG's list for this phase):

1. `AddDynamicPropertyToResultset` — **join** the side table and add the value field to the
   resultset, including under an aggregate function.
2. Add a dynamic property to `ORDER BY` (requires it to be in the resultset/join first).
3. Add a dynamic property to `GROUP BY` (requires the join first).
4. Add a dynamic property to `HAVING` (requires the join first).
5. Optimize `WHERE`: if the property is already joined into the query, filter it **directly**
   (`dp.v_int > @v`) instead of the `owner IN (SELECT …)` correlated sub-query.

---

## Approach change from the rev-2 root plan: JOIN, not correlated sub-query

`DYNAMIC_PROPERTIES_PLAN.md` (rev 2, §"Query translation") sketched SELECT/ORDER BY/GROUP BY as a
**correlated scalar sub-query** per clause. **This phase supersedes that with a JOIN** (NG's
explicit design in item 1: *"joins table and adds field"*). Rationale:

- A projection needs the value *in the FROM* anyway; one `LEFT JOIN` produces the column once and
  ORDER BY / GROUP BY / HAVING / WHERE can all reference the **same** column — no repeated
  sub-query, no repeated `name` parameter.
- Item 5 (the WHERE optimization) is only expressible with a join: once the value column is in the
  FROM, WHERE filters it directly.

Phase 2's WHERE-only path stays as the fallback (no join present).

### The join shape

For a property `p` of owner `T` (occurrence `k`):

```sql
LEFT JOIN <table>_props dpN
       ON dpN.owner = <ownerAlias>.<pk>
      AND dpN.name  = @nameParam
```

- `LEFT` so owners without the property still appear (matches EAV "absent ⇒ NULL" semantics; a
  `> / = / <` filter on the joined column then drops them, exactly like the `IN` sub-query did).
- The value **column** (`v_str` / `v_int` / `v_real`) is chosen from an **explicit type** the
  caller passes — there is no CLR operand to infer it from (this is the hard part NG flagged).
- `(owner, name)` is API-unique (change-tracking upserts), so the join does not fan rows out in
  practice. Not DB-enforced ⇒ documented v1 limit (a duplicate name row would multiply the owner
  row). Same caveat the whole feature already carries.

---

## Public API — where each piece lives

All new query-surface API goes on **`SelectEntitiesQueryBase`**
(`Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityQuery/`). The join bookkeeping + expression building goes
into a new sibling helper so `SelectEntitiesQueryBase.cs` stays lean:

- **New file** `EntityQuery/DynamicPropertyProjection.cs` — `internal` helper that owns the join
  registry and builds/records one join. (Sibling of `DynamicPropertyConditionBuilder.cs`.)
- **New public enum** — the explicit value-type selector. **Promote the existing internal
  `DynamicPropertyValueType`** (`String/Integer/Long/Real/Boolean/DateTime`, in
  `DynamicPropertiesValueMapper.cs`) **to `public`** and reuse it as the caller-facing type. It
  already encodes both the column (String→`v_str`; Integer/Long/Boolean/DateTime→`v_int`;
  Real→`v_real`) and the decode rule (ticks→DateTime, 0/1→bool, int vs long). No new enum needed.
  **(D1 — decided)** enum goes `public`; **no** public `Decode` helper — decode stays internal to
  the read path.

### Methods on `SelectEntitiesQueryBase`

```csharp
// 1 — project a dynamic property (establishes the join, adds the column to the resultset)
public void AddDynamicPropertyToResultset<T>(string name, DynamicPropertyValueType type,
                                             string alias = null, int occurrence = 0);

// 1 — aggregated projection
public void AddDynamicPropertyToResultset<T>(AggFn aggregation, string name,
                                             DynamicPropertyValueType type,
                                             string alias = null, int occurrence = 0);

// 2 — order by (join must exist; else throw a clear "add to resultset first")
public void AddDynamicPropertyToOrderBy<T>(string name, SortDir direction = SortDir.Asc,
                                           int occurrence = 0);

// 3 — group by (join must exist)
public void AddDynamicPropertyToGroupBy<T>(string name, int occurrence = 0);
```

Non-generic `Type`-taking overloads mirror the existing `AddToResultset(Type, …)` family for
symmetry.

### HAVING (item 4)

HAVING filters an **aggregate** of the joined column. Reuse the existing `Having` builder
(`EntityQueryConditionBuilder`) + its `Sum()/Min()/Max()/Avg()/Count()` wrappers rather than
inventing a parallel DSL. Provide one entry point that resolves the join and seeds the left side:

```csharp
// returns a SingleEntityQueryConditionBuilder positioned on the joined column,
// so callers write: q.HavingDynamicPropertyOf<T>("price").Sum().Gt(100)
public SingleEntityQueryConditionBuilder HavingDynamicPropertyOf<T>(string name, int occurrence = 0);
```

Internally this does `Having.Raw(<joined column alias>, <dbType>)` after looking up the join. It
composes with the existing aggregate-wrap + comparison extensions — no new operator surface.

### WHERE optimization (item 5)

In `DynamicPropertyConditionBuilder.Apply` (Phase 2): before building the `owner IN (SELECT …)`
sub-query, encode the operand (`DynamicPropertiesValueMapper.Encode(value)`) to learn its value
type/column, then ask the query (if it is a `SelectEntitiesQueryBase`) for a join keyed
`(T,name,occurrence,operandType)`. Because the registry key now includes the type (D3), the lookup
inherently matches the operand's column. On a hit, emit the predicate directly against the joined
alias:

```sql
dpN.v_int > @v          -- instead of  entity0.id IN (SELECT owner FROM …_props WHERE …)
```

- No join for the operand's `(name,occurrence,type)` ⇒ **fall back** to the Phase-2 sub-query
  (safe, always correct). This covers "property not joined at all" and "joined only under a
  different type".
- LEFT-join + direct filter has the same "absent/wrong-type ⇒ excluded" semantics as the sub-query
  for the positive operators; documented.

The registry lookup is exposed to the condition builder via an `internal` method on
`SelectEntitiesQueryBase`, e.g. `internal bool TryGetDynamicPropertyJoin(Type, string name, int occ,
DynamicPropertyValueType type, out DynamicPropertyJoin join)`.

---

## Mechanism details

### Establishing the join (in `DynamicPropertyProjection`)

1. `EntityDescriptor d = AllEntities.Get(typeof(T))`; `TableDescriptor side = d.DynamicPropertiesTable`
   (throw `InvalidOperationException` if the entity owns no property set — mirror the condition
   builder's message).
2. `QueryBuilderEntity owner = query.FindType(typeof(T), occurrence)` — the owner's builder entity
   (its alias).
3. `QueryBuilderEntity dp = query.SelectBuilder.AddTable(side, side[OwnerColumnId],
   TableJoinType.Left, owner, d.TableDescriptor.PrimaryKey)` — emits `dp.owner = owner.pk`.
4. Name filter on the join: `dp.On.And().Property(side[NameColumnId], dp).Eq().Parameter(nameParam)`;
   bind `nameParam` on the outer `SqlDbQuery` via `query.mQuery.BindParam<string>(nameParam, name)`
   using `query.NextParam` for a unique name.
5. Column pick: `side[ValueColumnId(type)]` (reuse/extract the `ValueColumnId` switch already in
   `DynamicPropertyConditionBuilder`; move it to a shared spot so both callers use one copy).
6. Record `DynamicPropertyJoin { QueryBuilderEntity Dp, ColumnInfo ValueColumn, DynamicPropertyValueType Type }`
   in a `Dictionary<(Type,string,int,DynamicPropertyValueType), DynamicPropertyJoin>` held by the
   query. **(D3 — decided)** the value **type is part of the key**, so the same property may be
   projected under two types in one query (two distinct joins, e.g. once as `Integer`, once as
   `String`). A repeat `AddDynamicProperty…` with the **same** `(T,name,occurrence,type)` reuses the
   existing join (idempotent — don't double-join); a different type adds a second join.

### Referencing the joined column

`string expr = query.SelectBuilder.GetAlias(join.ValueColumn, join.Dp)` → e.g. `entity7.v_int`.
Feed it to the existing raw-expression sinks already present on `SelectEntitiesQueryBase`:
`AddExpressionToResultset(expr, isaggregate, dbType, clrType, alias)` / `AddOrderByExpr` /
`AddGroupByExpr` / `Having.Raw`. Aggregates wrap via `Specifics.GetAggFn(aggregation, expr)` (same
call `SelectQueryBuilder.AddToResultset(AggFn,…)` uses).

### Decode of the projected value (the encoded-storage problem)

Stored values are **encoded**: DateTime→ticks (Int64), bool→0/1 (Int64). A raw `SELECT v_int`
returns a `long`; the caller expects `DateTime`/`bool`.

**(D4 — decided)** the aggregate runs against the **storage column** (ticks in `v_int`, the raw
`v_real`), and **decode happens at read time, driven by the declared property type** — uniformly
for plain projection *and* aggregates. So a `DateTime` property aggregated with `Min`/`Max`/`Sum`/
`Avg` comes back decoded to `DateTime`; a `Boolean` property decodes to `bool`; numeric types pass
through. Plan:

- Register, per resultset index, the declared `DynamicPropertyValueType`. In `BindOneDynamic` (the
  `ReadOneDynamic`/`ReadAllDynamic` path), when an index is a dynamic-property column read the raw
  value and run `DynamicPropertiesValueMapper.Decode(type, raw)`. This is the only edit to the read
  path — a parallel `Dictionary<int, DynamicPropertyValueType>` consulted before the existing
  `mResultsetTypes` branch.
- **`Count` is the one exception**: it yields a row count (`int`), not a value — no decode, matching
  the base framework's `AddToResultset(AggFn.Count, …) → typeof(int)`.
- The `mResultsetTypes` CLR entry is still set so a caller reading via `mQuery.GetValue(i, type)`
  gets a sane type; the decode hook is what makes `ReadOneDynamic` correct.
- Decode stays **internal** (D1) — no public `Decode` on the enum.

---

## Testing (both tiers, per the feature's testing model)

Tests live under `Gehtsoft.EF.Test/DynamicProperties/DataSelecting/` (csproj auto-includes the
folder — no `<Compile Include>` needed for this tree). Assert SQL **via the AST** (`.ParseSql()`),
never string `Contains`; prefer behavioural checks. New files:

- **Deep / SQLite** `DynamicPropertiesProjectionTest.cs`:
  - resultset join emits one `LEFT JOIN …_props … ON owner=pk AND name=@p` (assert on parsed AST:
    join type, on-condition operands, single join per repeated call).
  - each value type lands in the right column; DateTime round-trips **exactly** (ticks decode),
    bool decodes to `true/false`.
  - aggregate projection wraps the joined column in the agg fn; `Count`→int, `Min/Max` decode,
    `Sum/Avg` numeric.
  - ORDER BY / GROUP BY expr references the joined alias (AST); "not joined yet" throws.
  - WHERE optimization: with a join present, the WHERE AST references the joined column directly and
    contains **no `IN` sub-query**; without a join, the Phase-2 `IN` sub-query is still emitted;
    column mismatch falls back to the sub-query.
- **Acceptance / all drivers** `DynamicPropertiesProjectionAcceptanceTest.cs`
  (`[Theory][MemberData(ConnectionNames)]` + `IClassFixture`): seed owners with mixed props;
  assert projected values, ordered results, grouped counts/sums, having-filtered groups, and that
  the optimized WHERE returns the same rows as the Phase-2 sub-query form. SQLite always on; other
  drivers per local config.

---

## Risks / edge cases to keep in view

- **Two projections of the same property under different types** on one query → two joins, keyed by
  type (D3). Each carries its own `name` parameter and alias; tests must confirm both resolve and
  neither WHERE optimization crosses wires.
- **GROUP BY a projected non-aggregate dynamic property** must group by the same expression string
  the resultset uses (it does — both come from `GetAlias`).
- **Aggregate + GROUP BY interplay**: unchanged from the framework's existing rules; we only supply
  expressions.
- **Parameter binding site**: the `name` parameter binds on the outer query (the join lives in the
  main statement), unlike Phase 2's sub-query which also bound on the outer query — same mechanism,
  no new reader/transaction concern.

---

## Gate (per the phased working agreement — do not code before explicit go)

Decisions locked (NG, 2026-07-06):

- **D1** — `DynamicPropertyValueType` becomes `public`; **no** public `Decode` helper (decode stays
  internal to the read path).
- **D2** — HAVING via `HavingDynamicPropertyOf<T>(name)` seed + reuse of the existing `Having`
  builder and its aggregate-wrap/comparison extensions.
- **D3** — registry key **includes the value type** — the same property may be projected under two
  types in one query (two joins).
- **D4** — aggregates run on the storage column; **decode at read time by the declared type** for
  all aggregates and plain projection, uniformly. `Count` is the exception → `int`, no decode.
- **D5 (revised 2026-07-06, NG)** — implementation order: resultset (with aggregate) → **WHERE
  optimization** → ORDER BY / GROUP BY → HAVING. WHERE moved ahead of ORDER BY/GROUP BY: it is the
  only item touching existing code (`DynamicPropertyConditionBuilder`), so doing the risky
  integration early — while the join machinery is fresh — de-risks the rest and proves the
  registry-lookup path the others reuse. Each step ships deep (AST) + acceptance (all-driver) tests
  as it lands.

### Status

**Step 1 — DONE on SQLite (2026-07-06).** `DynamicPropertyValueType` promoted to `public`; new
`DynamicPropertyProjection.cs` (join registry + `DynamicPropertyJoinKey` container per NG — no raw
4-tuple); `SelectEntitiesQueryBase.AddDynamicPropertyToResultset<T>` (plain + aggregate, + `Type`
overloads); decode hook in `BindOneDynamic` (decode by declared type; `Count`→int). Tests:
`DynamicPropertiesProjectionSqlTest` (deep/AST) + `DynamicPropertiesProjectionTest` (acceptance).
Full DP suite 350/350 on SQLite. `AggFn.Count` → `COUNT(DISTINCT …)` (framework-consistent); the
test SQL grammar (`SqlTest.g4`) was extended to accept `<aggr>(DISTINCT field)` (quantifier carried
on the `AGGR_FUNC` node's value, not as a child, so arg-navigation helpers are unaffected), so Count
is now covered in the deep tier too. All-driver run + commit pending NG regression (same cadence as
Phase 2).

**Step 2 (WHERE optimization) — DONE on all drivers (2026-07-06).** `DynamicPropertyConditionBuilder.Apply`
now, when the query is a `SelectEntitiesQueryBase` with a matching-typed join already present
(`TryGetDynamicPropertyJoin`), filters the joined column directly (`dp.v_str = @p`) instead of
`owner IN (SELECT …)`; otherwise falls back to the Phase-2 sub-query (covers "not projected" and
"projected under a different type"). Order matters: the projection must precede the WHERE call for
the optimization to fire (the condition is built eagerly). Tests added to the deep AST file
(direct-vs-subquery, type-mismatch fallback) and the acceptance file (optimized == sub-query result,
all drivers). Full DP suite 372/372.

**Step 3 (ORDER BY / GROUP BY) — DONE on all drivers (2026-07-06).**
`AddDynamicPropertyToOrderBy<T>(name, type, dir, occ)` and `AddDynamicPropertyToGroupBy<T>(name,
type, occ)` (+ `Type` overloads) reference the joined column via `RequireDynamicPropertyJoin` →
`AddOrderByExpr` / `AddGroupByExpr`. **Per NG: the property must already be projected** (same
name+type+occurrence) — no auto-join; a missing join throws `InvalidOperationException`. Deep AST
tests (order/group reference the joined column; not-projected and wrong-type throw) + acceptance
(integer asc/desc with the optimized WHERE dropping the null owner to avoid driver NULL-ordering
dependence; group-by count-per-value, counting owner ids since `Count` of the value is
`COUNT(DISTINCT)`). Full DP suite 395/395.

**Step 4 (HAVING) — DONE on all drivers (2026-07-06).** `HavingDynamicPropertyOf<T>(name, type, occ)`
(+ `Type` overload) resolves the join via `RequireDynamicPropertyJoin` (must be projected first) and
seeds a `SingleEntityQueryConditionBuilder` on the joined column with the value column's DbType, so
the existing aggregate-wrap + comparison DSL composes: `HavingDynamicPropertyOf<T>("price",
Real).Sum().Gt(100.0)`. Comparison is against the stored (encoded) column — numeric/string compare
directly; DateTime/Boolean compare against the encoded form (documented on the method). Deep AST
(HAVING references `SUM(dp.v_int)`, not-projected throws) + acceptance (group by color, `SUM(size)`,
HAVING > 30 keeps only the red group). Full DP suite 403/403.

### Phase 3 query surface COMPLETE

All of NG's items land: projection (+aggregate), WHERE optimization, ORDER BY, GROUP BY, HAVING —
all on the JOIN, all green on every driver (403/403).

**Docs — tutorial DONE (2026-07-06).** Two articles added to `doc1/src/ns/sqltutorialsen.ds` under
the `tutorialsen` group: `tutorialen_dynprops1` (Dynamic (Per-Row) Properties — modelling, side
table, bag CRUD, save/load) and `tutorialen_dynprops2` (Querying — WHERE `DynamicPropertyOf`,
projection/aggregate/ORDER BY/GROUP BY/HAVING on `SelectEntitiesQueryBase`). `doc.bat`'s `MakeDoc`
builds clean, link-integrity passes. The new dynamic-property API symbols are shown in `[c]` code
font (not `[clink]`) because `src/raw` is a stale 2021 generation that predates them; a
`prepare.bat` regen would add their reference pages and let the code-font mentions become `[clink]`
links.

Remaining before phase close: confirm XML doc comments on the new public API (mostly present) and
NG's full-regression + commit (with `version.proj`). No further coding gates open on the query surface itself.
