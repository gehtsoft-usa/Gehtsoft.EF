# Coverage improvement plan — Gehtsoft.EF.Toolbox

Built fresh from `coverage.xml` re-collected against `Gehtsoft.EF.Toolbox.sln` **after** the
Gehtsoft.ExpressionToJs 0.3.1 upgrade and the `ValidationExpressionCompiler` refactor (51 tests pass).
Scope: the `Gehtsoft.*` production modules (third-party instrumentation filtered out).

## Executive summary

| | |
|---|---|
| Overall line coverage | **78.55 %** (2142 / 2727 lines) |
| Partially covered | 76 lines |
| Target | 90 % → **312 more lines** to cover |
| Production modules | 5 instrumented + **1 with no coverage at all** (Serialization) |

Per-module standing (worst first):

| Module | Line % | Lines to 90 % |
|---|---|---|
| `Gehtsoft.EF.Serialization` | **none collected** | — (no tests exist) |
| `Gehtsoft.EF.Validator` | 69.33 % | 49 |
| `Gehtsoft.Mapper` | 76.34 % | 99 |
| `Gehtsoft.EF.Mapper` | 77.78 % | 22 |
| `Gehtsoft.Validator` | 81.03 % | 114 |
| `Gehtsoft.EF.Mapper.Validator` | 81.05 % | 27 |
| `Gehtsoft.Validator.JSConvertor` (subset) | **94.78 %** | — |

## Verdict on this session's changes (ExpressionToJs 0.3.2 refactor)

**The refactor is well covered — no regression, no new blind spots of substance.** The
`Gehtsoft.Validator.JSConvertor` area was the best-covered in the solution at **94.78 %** in the last
collection:

| Type | Line % | Uncovered |
|---|---|---|
| `ConvertToJsExtension` | 98.15 % | 0 |
| `ValidationExpressionCompiler` | 94.12 % | 1 line + 1 partial |
| `GlobalCallHandlers.TryTranslate` | 90.00 % | 0 + 1 partial |
| `GlobalMemberHandlers.TryTranslate` | 80.00 % | 1 line + 1 partial |

> Numbers above are from the coverage run taken at the 0.3.1 state. The subsequent 0.3.2 upgrade
> **removed** the `RenameValueParameter` / `ParameterRenamer` workaround (value rules now bind via the
> plain `Map(_ => true, p => "value", …)` recipe), so that 100 %-covered nested visitor no longer
> exists — the area only got simpler. Re-collect to refresh the exact percentages.

The 17 `TestJsConvertor` tests exercise value rules (incl. array index), `MapReference` (entity rules)
and both global custom handlers, all verified differentially against Jint. What remains uncovered is
exactly the **defensive / never-produced** code:

- **`ValidationExpressionCompiler` ctor, lines 14–15** — the `throw` for a lambda with 0 or >2
  parameters. `ExpressionPredicate` only ever builds single-parameter rules, so this is unreachable in
  normal use.
- **`GlobalMember/CallHandlers.TryTranslate` tails** — the "no registered handler matched → return
  false" path when the static list is non-empty but no entry claims the node.

### Suggested tests for the refactor (small, high-value)

1. **Bad arity guard** — `new ValidationExpressionCompiler(threeParamLambda)` should
   `Throw<ArgumentException>` ("only one or two parameters"). Covers ctor 14–15.
2. **Custom handler that declines** — register an `AddCustomCall` / `AddCustomMemberAccess` returning
   `null` for the node under test and assert the built-in translation still wins. Covers the
   `TryTranslate` "no match" tail and locks in the "custom first, then built-in" priority the refactor
   preserves.

## Biggest gaps by functional area (ranked)

### 1. `Gehtsoft.EF.Serialization` — shipped, **0 % covered** *(top priority)*
**What's at risk**: this assembly is published to consumers but has **no tests and isn't even
instrumented** (zero references from the test project). Any serialization regression ships undetected.
**Suggested scenarios**: round-trip serialize → deserialize representative model graphs (nested
objects, collections, nullable/enum fields, null values) and assert structural equality; cover the
public entry points and at least one error path (malformed input). This single area moves the needle
more than any percentage tuning elsewhere.

### 2. `Gehtsoft.EF.Mapper` — 79.51 % (155 uncovered)
**What's at risk**: object-to-object / entity mapping is core plumbing many features depend on.
- `EfMap<TSource,TDestination>` — **0 %** (21 lines): the EF map entry type is entirely untested.
- `PropertyMapping<TSource,TDestination>` — 79 % (23): fluent builders `From`/`To`/`When`/`WithFlags`/
  `Otherwise` each miss their ~4-line body.
- `MapPropertyAttribute` 46 %, `MapExtension` 50 %, `MappingAction` 54 %.
**Suggested scenarios**: configure a map fluently (`From(...).To(...).When(...).WithFlags(...)`), map a
populated source to a destination, and assert each configured property/flag/conditional took effect;
add an `EfMap` round-trip against a test entity.

### 3. `Gehtsoft.Validator` — 84.44 % (137 uncovered)
- `ValidationFailure` — **32 %** (21): the failure value object (paths, messages, formatting) is barely
  exercised despite being the user-visible result.
- `GenericValidationRuleBuilder<TE,TV>` — 85 % (21), `BaseValidator` 89 % (11),
  `ValidationRuleCollection` **0 %** (7), `AlwaysPredicate` **0 %** (6).
**Suggested scenarios**: validate a model that fails several rules and assert the full
`ValidationFailure` set (paths + messages); drive the generic builder through array/entity targets.

### 4. `Gehtsoft.EF.Validator` — 69.04 % (lowest module, 56 uncovered)
- `EfPredicateFactory` — 63 % (21): the factory translating EF metadata into predicates.
- `ValidatorConnectionFactory` **0 %** (12), `EntityPropertyTarget` **0 %** (11),
  Decimal/Number range predicates ~68 %.
**Suggested scenarios**: build an `EfModelValidator` over an entity with size/precision/range metadata
and validate values that violate each constraint (drives the factory + range predicates + property
target end-to-end against a test DB).

### 5. `Gehtsoft.EF.Mapper.Validator` — 81.33 % (26 uncovered)
- `RuleBuilderExtension` 82.65 % (20 + 14 partial): `AddUnique`, `AddValidDbSizeRule`,
  `AddValidDbValueRange`, `AddExists` DB-backed builders.
**Suggested scenarios**: an EF-backed validator using uniqueness/exists rules against a seeded test
database (insert a duplicate → expect a failure; valid row → pass).

## Quick wins
133 functions miss only **1–5 lines** each (271 lines total) — mostly single error/guard branches or
one-line fluent returns. High-leverage clusters: the `PropertyMapping` fluent setters (5 × ~4 lines),
`DefaultEfValidatorMessageProvider` (2 lines), and assorted `Validate(...)`/attribute one-liners. A
focused pass here is the cheapest path toward the 90 % target after the Serialization work lands.

## Recommended order

1. **Serialization** — biggest risk (shipped, untested); also adds a brand-new instrumented module.
2. **`Gehtsoft.EF.Validator`** (lowest %) + **`Gehtsoft.EF.Mapper`** core types (`EfMap`, `PropertyMapping`).
3. **`ValidationFailure`** and validator builders.
4. The two **refactor guard tests** above (cheap, closes our own change to ~100 %).
5. **Quick-wins sweep** to clear the remaining 1–5-line branches up to 90 %.
