# PHASE 9 — JSON values in the entity LINQ surface

*Planned 2026-07-08. The manual query surface (Phases 4–7) and docs (Phase 8) are DONE and committed
(`45b81d6`). This phase makes a JSON value usable from the **entity LINQ** collection
(`connection.GetCollectionOf<T>()`), the same step EAV took in `../DYNAMIC_PROPERTIES/PHASE_4/`.
This is a **plan only** — no code until the start gate is approved.*

Prerequisite reading: `../../DYNAMIC_PROPERTIES/PHASE_4/PHASE_4_PLAN.md` (the EAV precedent) and
`../../DYNAMIC_PROPERTIES/LINQ_ANALYSIS.md` (compiler anchors). All line numbers below are current as
of this analysis (post-`45b81d6`); search by symbol if they drift.

---

## The essential difference from EAV (why this is not a copy-paste)

| | Dynamic properties (EAV) | JSON properties |
|---|---|---|
| LINQ surface | `e.DynamicProperties.Get<T>("name")` — a **method call** | `e.Profile.Address.State` — a **member-access chain** (and `e.Profile.Scores[0]` — an array index) |
| Recognized in | `ExpressionCompiler.ProcessCall` (`IsDynamicPropertyGet`, `ExpressionCompiler.cs:480-514`) | the **`MemberExpression` branch** (`ExpressionCompiler.cs:186-199`) + `IsQueryPath` (`:413-476`), plus a new terminal `ArrayIndex`/`get_Item` case in `Visit` |
| SQL shape | a **LEFT JOIN** to the side table (`DynamicPropertyProjection.EnsureJoin`) emitting `dp.v_int` | an **expression on the owning column**: `JsonExtract(alias, "$.a.b[0]", dbType)` — no join |
| Whole-entity read | needs a **preload** of the bag (`preloadDynamicProperties`) | **already works** — the `JsonPropertyAccessor` deserializes the whole document through the normal binder; no preload analog |
| Read-back decode | every type is stored encoded (ticks/0-1) → registry decode | only **DateTime** (stored as ISO text) and **bool** need read-back handling; numeric/string pass through |

The manual surface built this session already resolves the member/array chain to `(property, "$.path",
leafType)` in `EntityQueries/EntityQuery/JsonExpressionParser.cs` and emits the extract via
`SqlDbLanguageSpecifics.JsonExtract`. Phase 9 reuses **both** — the LINQ compiler is a third caller of
the same primitives (manual-string form and manual-expression form being the first two).

---

## Decisions to confirm with NG (open forks)

- **D1 — Surface = direct member/array access, no marker method.** `e.Profile.Address.State`,
  `e.Profile.Scores[0]`. This is the natural CLR shape, needs no new public API, and matches the
  expression form already shipped for the manual API (`JsonPropertyOf<T>(e => e.Profile.Age)`).
  *(Recommended. Alternative — a `SqlFunction.JsonValue<T>(e, "$.path")` marker — is more code and
  less natural; reject unless NG prefers it.)*
- **D2 — Translation = emit `JsonExtract` on the resolved column alias.** No join; reuse
  `mSpecifics.JsonExtract(alias, jsonPath, dbType, forDdl:false)`. Tag the `Result` with the target
  `DbType` (new `Result.JsonValueType`).
- **D3 — Value-type scope for v1 = string, all integer types, real/decimal/money, `bool` AND
  `DateTime`. `bool` and `DateTime` are covered, not deferred (NG, 2026-07-08)** — a JSON object has a
  boolean/date field and it must round-trip and compare correctly on every driver. The per-driver
  differences (SQLite JSON `true`→`1`, Postgres `→boolean`, Oracle `→'true'/'false'` text; DateTime as
  ISO text everywhere) are exactly what the driver specifics already know, so they are handled by a
  **per-driver JSON value codec** (see D4), not punted. `byte[]`, whole nested objects and whole arrays
  remain non-extractable; a single primitive **array element** IS supported (D5).
- **D4 — bool/DateTime handled by a per-driver value codec on `SqlDbLanguageSpecifics`.** Two virtual
  hooks alongside the existing `JsonExtract`:
  - `object JsonEncodeValue(DbType type, object clrValue)` — turn a CLR comparison value into the form
    `JsonExtract` yields for that driver+type, so `WHERE extract = @p` matches. Default = identity;
    `bool` → SQLite `1/0`, Postgres bool as-is, Oracle `'true'/'false'`; `DateTime` → the ISO-8601
    string System.Text.Json produces (serialize, strip quotes) so it equals the stored text and sorts
    chronologically.
  - `object JsonDecodeValue(DbType type, object dbValue)` — the inverse for projection read-back:
    `bool` from `1/0` / real bool / `'true'/'false'`; `DateTime` by parsing the ISO string; numeric and
    string pass through.

  This codec is the **single source of truth** and is wired into **both** the LINQ path (this phase)
  **and** the already-shipped manual `.Value()` / `AddJsonValueToResultset` paths, so manual bool/DateTime
  comparisons — which today only work for the types the shipped tests exercised — become correct too.
  (`JsonRealLifeTest` deliberately avoided bool and used `DbType.String`+ISO for DoB; after this phase
  add manual bool + native-DateTime cases to lock it in on all 3 drivers.)
- **D5 — Array elements (`e.Profile.Scores[0]`) usable in `Where` and `Select` only** (NG,
  2026-07-08); **not** in `OrderBy`/`GroupBy`. So a new terminal `ArrayIndex`/`get_Item` case is added
  in `ExpressionCompiler.Visit` (for the WHERE/Select scalar path), but `SelectExpressionCompiler`'s
  OrderBy/GroupBy accept-lists are left unchanged.
- **D6 — Occurrence 0 only** (the sole `e` in the lambda), matching EAV D5.

---

## Tree shapes to recognize

```
e => e.Profile.Age                       Member(Age) → Member(Profile) → Parameter e
e => e.Profile.Address.State             Member(State) → Member(Address) → Member(Profile) → Parameter e
e => e.Profile.Scores[0]                 BinaryExpression(ArrayIndex, Member(Scores)→Member? , Const 0)
e => e.Profile.DoB                       Member(DoB) → Member(Profile) → Parameter e   (DateTime → D4)
```

Recognition rule: walk down the member chain; the **innermost member that resolves (single-hop) to an
entity column whose `ColumnInfo.Json != null`** is the JSON property. The members peeled off above it
(and any array index) form the JSON path; the **outermost member's CLR type** is the leaf type.

---

## The `ExpressionCompiler` hook (core of the phase)

Add a helper `TryResolveJsonPath(MemberExpression node, out InQueryName column, out string jsonPath,
out Type leafType)` used by the `MemberExpression` branch:

1. In `Visit`'s `MemberExpression` branch (`ExpressionCompiler.cs:186-199`), **before** the current
   `IsQueryPath`/constant fallthrough, call `TryResolveJsonPath`.
2. `TryResolveJsonPath` peels outer members, and for the remaining inner `MemberExpression` calls the
   existing single-hop resolver (`IsQueryPath` on the inner) to get the `EntityQueryItem`. If that
   item's `Column.Json == null`, return false (normal path handles it — a real FK chain, etc.).
3. If it IS a JSON column: build `"$." + joined peeled member names` (with `[i]` for an array index),
   `leafType` = the outermost member's type, `dbType` = override for `DateTime`→`String` else
   `mSpecifics.TypeToDb(Nullable-unwrapped leafType)`.
4. Emit `res.Expression.Append(mSpecifics.JsonExtract(column.Alias, jsonPath, dbType, false))` and set
   `res.JsonValueType = dbType` (+ remember the CLR leaf type for read-back). Return the `Result`.

Reuse the path/leaf-type walking logic already in `JsonExpressionParser` (refactor it to accept "stop
at this inner boundary" so both the manual `JsonPropertyOf<T>(e=>…)` and this compiler share one
walker — avoid a second copy).

**Array element** (`e.Profile.Scores[0]`): `Visit` has **no terminal `ArrayIndex`/`get_Item` case
today** (agent-confirmed — `Visit` `:99-235` lacks it; a bare array index hits `ProcessBinary` default
throw). Add a terminal `ExpressionType.ArrayIndex` case (and the `get_Item` call shape) that, when the
array expression resolves into a JSON path, appends `[const]` to the path and continues as a JSON leaf.

**Aggregates**: in `ProcessCall`'s `SqlFunction.Sum/Min/Max/Avg` branch (`ExpressionCompiler.cs:796-819`)
propagate the inner operand's tag exactly as EAV does (`res.JsonValueType = argResults[0].JsonValueType`)
so `SqlFunction.Sum(e.Profile.Income)` stays typed; `Count` stays untagged (int).

---

## bool / DateTime — the per-driver codec in action (D4)

Mirror EAV's encode-on-compare / decode-on-read mechanism, but the encode/decode values come from the
per-driver `JsonEncodeValue`/`JsonDecodeValue` codec (D4), so bool's driver-specific representation is
handled without any driver-specific logic leaking into the compiler.

- **Encode-on-compare (`Where`).** In `ProcessBinary` (`ExpressionCompiler.cs:301-305` area), when one
  operand is a `Result` tagged with a `JsonValueType` that needs encoding (`bool`/`DateTime`) and the
  sibling is a const/param, mark the sibling (new `ExpressionParameter.JsonEncodeAs = dbType`). At bind
  time in `EntityQueryLinqExtension.BindExpressionParameters` (`:23-48`, the seam EAV's `EncodeAs`
  uses), replace the value with `specifics.JsonEncodeValue(dbType, value)` before `BindParam`. `== null`
  keeps the existing `IS NULL` rewrite. For `bool`, the extraction DbType is `Boolean` and the encoded
  literal matches per driver; for `DateTime`, the extraction is forced to `String` and the value encodes
  to the ISO string.
- **Decode-on-read (`Select`).** Route JSON-tagged projections through a new
  `SelectEntitiesQueryBase.AddJsonExpressionToResultset(expr, isAgg, dbType, clrLeafType, alias)` that
  registers the resultset index in a JSON decode registry (analog to `mDynamicPropertyColumns`).
  `QueryableEntityProvider.CreateType`/`ReadOneValue` (`:351-368`, `:344-349`) consult it and call
  `specifics.JsonDecodeValue(dbType, raw)` for a registered column; numeric/string decode is a
  pass-through (`GetValue(i, clr)`). Wire it in `EntityQueryLinqExtension.AddToResultset` (`:50-59`) next
  to the existing `DynamicPropertyType` branch.

For string / integer / real / decimal there is **no** encode or decode — the value maps directly (this
is the majority path, verified as a pass-through, not special-cased).

---

## ORDER BY / GROUP BY

Good news vs EAV: `SelectExpressionCompiler` **already accepts `MemberAccess` bodies** for OrderBy
(`SelectExpressionCompiler.cs:89-90`) and GroupBy keys (`:126-127`) — and JSON access IS member access —
so `OrderBy(e => e.Profile.Income)` / `GroupBy(e => e.Profile.Address.State)` flow through to the
`ExpressionCompiler` hook with **no clause-collector change** (EAV needed to add `Call` acceptance; we
do not). Verify the group-key projection (`g.Key`) re-emits the JSON expression in
`QueryableEntityProvider` (the group-key wiring the EAV plan touched at its `:224-247`).

**Array elements in ORDER BY / GROUP BY are OUT (D5).** An array-element body (`e => e.Profile.Scores[0]`)
is an `ArrayIndex`, which `SelectExpressionCompiler` drops (OrderBy) / rejects (GroupBy) today — and we
leave it that way: array elements are usable in `Where`/`Select` only. `SelectExpressionCompiler` is
therefore **not changed** in this phase.

---

## Whole-entity reads — nothing to do

A `GetCollectionOf<Person>().Where(...).ToList()` (no `Select`) already materializes `Person` with
`Profile` deserialized, because the JSON column is a normal column read through the `JsonPropertyAccessor`
wired in Phase 3. There is **no bag to preload**, so no `preloadJson` flag and no change to
`SelectEntitiesQuery`/`GetCollectionOf`. (Call this out in the tests with a whole-entity round-trip.)

---

## Files touched

- **`Linq/ExpressionCompiler.cs`** — `TryResolveJsonPath` + JSON branch in the `MemberExpression` case;
  new terminal `ArrayIndex`/`get_Item` case in `Visit`; `Result.JsonValueType` tag;
  `ExpressionParameter.JsonEncodeAs` + set it in `ProcessBinary`; aggregate tag propagation.
- **`Linq/EntityQueryLinqExtension.cs`** — `AddToResultset` routes JSON-tagged results to
  `AddJsonExpressionToResultset`; `BindExpressionParameters` applies the DateTime→ISO-string encode.
- **`Linq/QueryableEntityProvider.cs`** — `CreateType`/`ReadOneValue` consult the JSON decode registry;
  confirm `g.Key` re-emission.
- **`SqlLanguageSpecifics.cs` + the 3 driver specifics** — add the `JsonEncodeValue`/`JsonDecodeValue`
  codec (default identity in the base; `bool`/`DateTime` overrides per driver, next to the existing
  `JsonExtract` overrides).
- **`EntityQuery/SelectEntitiesQueryBase.cs`** — JSON decode registry + `AddJsonExpressionToResultset`
  internal shim + `TryGetJsonColumn`; reuse the existing `ClrTypeOfJson`. Also route the manual
  `AddJsonValueToResultset` DateTime/bool read-back and the manual `.Value()` WHERE encode through the
  same codec so the manual API's bool/DateTime become correct (D4 "single source of truth").
- **`EntityQueries/EntityQuery/JsonExpressionParser.cs`** — refactor the member/array walker to a shared
  "walk from inner boundary" helper (used by the manual expression API and the compiler).
- **`SelectExpressionCompiler.cs` — NOT changed** (D5: no array element in ORDER BY/GROUP BY; member
  access is already accepted there).
- **Reuse unchanged:** `SqlDbLanguageSpecifics.JsonExtract`, `TypeToDb`.

## Blast radius

`ExpressionCompiler` is on the path of **every** LINQ query, and the new code sits in the
`MemberExpression` branch — the hottest node. The JSON branch must be strictly additive: it only
triggers when an inner member resolves to a column with `Column.Json != null`; every existing shape
(plain property, FK chain, `DateTime.Year`, `string.Length`, occurrence array-index-into-entity) must
be byte-for-byte unchanged. A broad regression of the existing `Entity/Linq/*` suite is mandatory.

## Tests (`Gehtsoft.EF.Test/JsonProperties/Linq/`, 3 drivers, `-mssql,-mysql`)

New namespace `Gehtsoft.EF.Test.JsonProperties.Linq` (test csproj auto-includes). Reuse the `Customer`
entity shape from `JsonRealLifeTest`. Assert against an in-memory snapshot the way the EAV
`DynamicPropertiesLinqTest` does; check generated SQL via AST where shape matters (house rule).

- **WHERE**: numeric `>=`/`==`/`!=`; string `==`/`!=`/`== null`; **bool `== true`/`== false`** (the
  codec, all 3 drivers); nested sub-object (`e.Profile.Address.State == "CA"`); array element
  (`e.Profile.Scores[0] >= 10`); **DateTime** range via the ISO-string codec; composition with
  normal-column predicates and `And`/`Or`.
- **Select**: single value and anonymous type mixing a normal column + JSON values; confirm bool and
  DateTime decode back correctly and numeric/string pass through; `Scores[0]` projected.
- **OrderBy / GroupBy**: order by a JSON numeric; group by a JSON sub-object string with `g.Count()` /
  `SqlFunction.Sum(e.Profile.Income)`. (Array elements NOT sortable/groupable — a negative test asserts
  the documented limit.)
- **Aggregate**: `Sum/Min/Max/Avg` of a JSON numeric; `Count`.
- **Whole-entity**: `GetCollectionOf<Customer>().Where(json predicate).ToList()` returns entities with
  the document deserialized (no preload flag).
- **`JsonRealLifeTest` extension (NG, 2026-07-08):** demonstrate **bool** (`Married`) and **native
  `DbType.DateTime`** (`DoB`) in the same three-form style as the other queries. Entity string +
  expression forms encode/decode automatically via the codec; the pure-SQL form binds a codec-encoded
  value (`specifics.JsonEncodeValue(DbType.Boolean, true)` etc.) since pure SQL is the low-level layer
  with no auto-hook. This replaces the current DoB-as-`DbType.String` workaround with the native form.
- **Regression**: a spread of existing non-JSON LINQ queries (blast-radius guard).
- **Negative**: unsupported leaf type; array element in OrderBy/GroupBy; a JSON member used where the
  compiler isn't a select context.

## Semantics & documented limits

- Absent/`null` JSON value = SQL NULL: `==`/`>`/`!=` drop the row, `== null` selects it (3-valued
  logic) — differs from reading the deserialized object in memory. Document it (same as the manual API).
- Occurrence 0 only (D5). Static-field operands NRE in the compiler (pre-existing limitation — compare
  against locals/literals).
- JSON values are usable in `GetCollectionOf<T>` lambdas; **mass UPDATE of individual JSON values stays
  out of scope** (whole-document update only) — unchanged from the feature scope.

## Gates (per phase process)

1. **This plan** — approve before any code. ← we are here (start gate).
2. **Advance gate** — after the plan is approved and the phase is green, before committing/moving on.

Deferred out of this phase (tracked): array elements in `OrderBy`/`GroupBy` (D5 — WHERE/Select only);
multi-occurrence owners; JSON values inside `Update`/`Delete` LINQ set/predicate beyond what the manual
MassDelete already covers.
