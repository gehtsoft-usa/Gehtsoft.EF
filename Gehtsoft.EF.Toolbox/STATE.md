# Session state — JS validator converter testing & RuleExecutionSide feature

**Date**: 2026-06-12 · **Branch**: master · **All changes uncommitted** · **Tests: 51/51 pass**
(`dotnet test Gehtsoft.EF.Toolbox.sln`)

## What this work is

Started as "convert the manual `TestJsConvertor` debug harness into real unit tests" and grew
(by explicit decisions from Nikolay) into:

1. **Execution-based test suite** for the C#→JS validation rule converter — generated JS is
   *executed* in Jint and its verdicts compared with server-side `Validate()` (parity testing),
   never compared as text.
2. **`RuleExecutionSide` feature** in `Gehtsoft.Validator` — explicit server/both rule marking,
   fail-loud contract for untranslatable client rules.
3. **`EntityMust` server-side fix** — was broken since inception, exposed by the parity tests.

## Design decisions (Nikolay's, do not relitigate)

- Tests assert **intended** behavior; a product bug must fail the test, never be adapted to.
- `RuleExecutionSide { Server = 1, Both = 3 }` — **no Client-only value** (rejected: would
  require server to skip rules). Value 2 reserved.
- Untranslatable predicate/condition on a client-side rule → `GetJsRules()` **throws**
  `InvalidOperationException` (silent degradation rejected). Escape hatch: `SetSide(Server)`.
- Predicates *declare* their side (`IValidationPredicate.Side`, default-interface-member
  `Both`); `FunctionPredicate`/`IsEnumValueCorrectPredicate`/`DatabasePredicate` declare
  `Server` → backward compatible silent exclusion for delegate/enum/DB rules.
- Builder-time contradiction check: explicit `Both` + server-only predicate throws in
  `SetSide()`/`Must()`/condition setters.
- netstandard2.0 → **netstandard2.1** for Gehtsoft.Validator, Gehtsoft.EF.Validator,
  Gehtsoft.EF.Mapper.Validator (enables default interface members).

## Changed files (this session's part of the working tree)

Product — `Gehtsoft.Validator/`:
- `Rule/RuleExecutionSide.cs` (new) — the enum
- `Predicates/IValidationPredicate.cs` — `Side` + `ParameterIsEntity` DIMs, RemoteScript contract doc
- `Predicates/FunctionPredicate.cs` — `Side => Server`, `ParameterIsEntity => false` (virtual)
- `Predicates/ExpressionPredicate.cs` — overrides: `Side => Both`, `ParameterIsEntity => mParameterIsEntity`
- `Predicates/IsEnumValueCorrectPredicate.cs` — `Side => Server`
- `Rule/ValidationRule.cs` — `ExplicitSide` (internal), `Side` (explicit ?? Validator?.Side ?? Both),
  `IgnoreOnClient` now a shim over `ExplicitSide`; `IValidationRule.Side` DIM
- `Rule/ValidationRuleBuilder.cs` — `SetSide()`, side-consistency checks, all predicate
  assignments routed through `Must()`, `ServerOnly()` = `SetSide(Server)`
- `Rule/GenericValidationRuleBuilder.cs` — typed `SetSide()`; `Otherwise()/Also()` copy `ExplicitSide`
- `Validator/BaseValidator.cs` — **EntityMust fix**: `ValidateOneValue` passes `entity` when
  `rule.Validator.ParameterIsEntity` (sync + async paths)
- `JSConvertor/ConvertToJs.cs` — rewritten scan: skip `Side == Server`, throw on null validator
  script and null condition script (`ConditionScript` helper handles Unless negation safely)
- `Gehtsoft.Validator.csproj` — TFM 2.1

Product — `Gehtsoft.EF.Validator/`:
- `Predicates/DatabasePredicate.cs` — `Side => Server`
- csproj — TFM 2.1 (also `Gehtsoft.EF.Mapper.Validator.csproj`)

Tests — `Gehtsoft.EF.Toolbox.Test/`:
- `JsRuleExecutor.cs` (new) — Jint harness: loads stub.js via
  `ExpressionToJsStubAccessor.GetJsIncludesAsString()`, defines `reference()` with `[index]`
  token support, runs rules per element for `ArrayValidator`, returns `(Path, Message)` list
- `TestJsConvertor.cs` (rewritten) — 17 tests; `AssertClientMatchesServer` helper asserts
  client == server == expected; compiler hooks registered in static ctor (gated by
  `ModelValidator` member names; note: hooks are global statics with no unregister API)
- csproj — added `Jint 4.9.3`

Docs/plan:
- `KNOWN_BUGS.md` (new) — ExpressionToJs 0.2.1: (1) `jsv_match` not null-safe,
  (2) `jsv_and`/`jsv_or` don't short-circuit. Workaround in both cases: guard rules with
  `UnlessValue`/`WhenEntity` conditions.
- `coverage_plan.md` — area #1 (JS converter) marked DONE.

Pre-existing working-tree changes NOT from this session: xUnit.v3 migration edits
(other Test/*.cs files, Mapper/Serialization/TestDatabase csprojs, nuget bats, .gitignore),
`NUNit.Migration.md`.

## Open follow-ups

1. **Authored docs** (`doc/src/validator/*.ds`) don't describe `RuleExecutionSide`, `SetSide`,
   `IValidationPredicate.Side`/`ParameterIsEntity`; `IValidationPredicate.ds` doesn't even
   document `RemoteScript`. Needs a docgen pass.
2. **Release notes / version bump**: behavior change — `GetJsRules()` now throws where it
   silently dropped rules or emitted broken `"!()"` conditions; netstandard2.1 floor.
3. **ExpressionToJs package fixes** (separate repo,
   `/mnt/d/develop/components/Gehtsoft.Tools/Gehtsoft.ExpressionToJs/`): null-safe `jsv_match`,
   short-circuit `&&`/`||` translation; then publish and bump the 0.2.1 reference.
4. `ExpressionPredicate.RemoteScript` caches a failed compile as null and returns it silently
   on a second call (first call throws, second doesn't) — minor quirk, only matters if
   `GetJsRules()` is called twice on the same validator instance after a failure.
5. Remaining coverage_plan areas: #0 Serialization (top priority, shipped, 0%), #2 fluent
   builder (partially covered incidentally by the new tests), #3 EF validator infrastructure.
