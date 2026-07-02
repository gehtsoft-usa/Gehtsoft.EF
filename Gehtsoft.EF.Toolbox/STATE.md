# Session state — Gehtsoft.EF.Serialization coverage, type-set unification, Binary/JSON formats

**Date**: 2026-06-20 · **Branch**: master · **Changes uncommitted** · **Tests: 78/78 toolbox pass**
(`dotnet test Gehtsoft.EF.Toolbox.sln`). EF-repo tests: `SingleAndShortTypeTest` 4/4 + no
regression in discovery/create suites.

Spans **two repos**: `Gehtsoft.EF.Toolbox/` (Serialization + tests) and the sibling EF core
`Gehtsoft.EF/` (consumed by the Toolbox via NuGet, now **1.9.5.1** on myget gehtsoft-public).

## What this work is

1. **Integration coverage for `Gehtsoft.EF.Serialization`** (was 0%, top coverage-plan item).
   `TestSerializationRoundTrip.cs`: an Account / AccountType / Transaction graph exercising
   aggregation, plain reference and self-reference, plus every supported primitive, round-tripped
   through **DB ⇄ XML ⇄ Binary ⇄ JSON** (DB = in-memory SQLite). Coverage now ~83%.
2. **One scalar type set across DB / XML / Binary / JSON** (Nikolay: "all three must support the
   same set of types"):
   - `TextFormatter` gained **`long`** (code `q`) and **`Guid`** (code `g`) — additive, so old
     XML stays readable.
   - EF core gained **`float`→DbType.Single** and **`short`→Int16** auto-detect, plus
     `DbType.Single` in every driver's DDL `TypeName` (float treated exactly like double).
3. **Two new serializers**: `IO/Binary` and `IO/Json`. All non-DB formats now identify entity
   types by **EF scope + table name** (not AssemblyQualifiedName — XML migrated too) and their
   readers take `EntityFinder.EntityTypeInfo[]` like `DbEntityReader`.

## Design decisions (Nikolay's, do not relitigate)

- DB/XML/Binary/JSON must support the **same scalar set**: string, bool, short, int, long, double,
  float, decimal, DateTime, byte[], Guid, enum.
- `float` is **not** a DB limitation — SQLite stores REAL fine; EF simply lacked the mapping.
  Fixed by mirroring `double` everywhere (same SQL type per driver).
- Serialized streams must **not** contain assembly-qualified type names → scope + table name,
  resolved against a caller-supplied `EntityTypeInfo[]` (consistent with `DbEntityReader`). This
  is an intentional **breaking change** to the XML format and `XmlEntityReader` ctor signature.
- Binary stores blobs **inline** (length-prefixed); XML/JSON use `IBlobAccessor` (base64 default).
- JSON uses **System.Text.Json 9.0.10** (clean net8.0 + netstandard2.0; 10.x targets net10).

## Changed / new files

EF core — `Gehtsoft.EF/` (shipped as 1.9.5.1):
- `Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/ColumnDiscoverer.cs` — auto-detect
  `short→Int16`, `float→Single` (default size 18 / precision 7, like double)
- `Gehtsoft.EF.Db.SqlDb/SqlLanguageSpecifics.cs` — `float→DbType.Single` in `TypeToDb` + `ToDbValue`;
  `Single` grouped with `Double` in Sql92 `TypeName`
- `…SqliteDb / MssqlDb / MysqlDb / OracleDb / PostgresDb` `*LanguageSpecifics.cs` — `DbType.Single`
  grouped with `DbType.Double`
- `Gehtsoft.EF.Db.SqlDb/UpdateQueryToTypeBinder.cs` — `Single` truncation mirroring `Double`
- `Gehtsoft.EF.Test/Entity/Discovery/SingleAndShortTypeTest.cs` (new) — mapping, discovery,
  "Single mirrors Double across all 5 drivers", SQLite float/short round-trip

Serialization — `Gehtsoft.EF.Toolbox/Gehtsoft.EF.Serialization/`:
- `IO/TextFormatter.cs` — added `long` (`q`) and `Guid` (`g`) Format/Parse + codes
- `IO/EntityTypeResolver.cs` (new) — scope+name → descriptor from `EntityTypeInfo[]`
- `IO/EntityMaterializer.cs` (new) — instance rebuild (defaults, FK stub, enum/nullable convert)
- `IO/Binary/BinaryFormatter.cs`, `BinaryEntityWriter.cs`, `BinaryEntityReader.cs` (new)
- `IO/Json/JsonEntityWriter.cs`, `JsonEntityReader.cs` (new)
- `IO/Xml/XmlEntityWriter.cs` + `XmlEntityReader.cs` — emit/resolve scope+name (`<t s n>`),
  reader ctors take `EntityTypeInfo[]`
- `Gehtsoft.EF.Serialization.csproj` — EF refs → 1.9.5.1, added System.Text.Json 9.0.10

Tests / docs — `Gehtsoft.EF.Toolbox/`:
- `Gehtsoft.EF.Toolbox.Test/TestSerializationRoundTrip.cs` (new) — 4 round-trip facts + scalar
  theories for TextFormatter (long/Guid/float) and BinaryFormatter
- `Gehtsoft.EF.Toolbox.Test/Gehtsoft.EF.Toolbox.Test.csproj` — ProjectReference to
  Gehtsoft.EF.Serialization; SqliteDb ref → 1.9.5.1
- `KNOWN_BUGS.md` — all serialization items resolved (only historical context remains)

## Open follow-ups

1. **Version bump + package** `Gehtsoft.EF.Serialization`: the XML format change (scope/name) and
   `XmlEntityReader` ctor signature change are **breaking**. (Packaging is Nikolay's process.)
2. Other Toolbox projects (Mapper, Validator, TestDatabase) still reference EF **1.9.5**; they
   unify up to 1.9.5.1 without conflict — bump for consistency if desired.
3. Remaining Serialization coverage: **done — module now 96.19%** (was ~80%). New file
   `Gehtsoft.EF.Toolbox.Test/TestSerializationCoverage.cs` (38 tests) covers `FileBlobAccessor`,
   the Stream/StringWriter/FileStream ctors, cancellation + DB frame-paging, all writer guard
   clauses, Binary/Text codec error codes, `EntityTypeResolver` failures, malformed JSON/Binary
   input, and the XML default-value / max-properties paths. **Bug found & fixed**:
   `XmlEntityReader.Scan` threw "Stack empty" on indented/pretty-printed XML (whitespace before
   the first child of `<es>`) — text-node cases now guard on `mStack.Count > 0`; logged in
   KNOWN_BUGS.md. The ~10 still-uncovered lines are defensive/unreachable (a nullable-boxing
   branch, a `Convert.ChangeType` fallback that a matched type set never hits, an in-entity
   too-many-properties throw shadowed by the type-def throw, and one recursion-descent
   cancellation line). Optional JSON indentation option and a large-blob Binary path still not
   implemented.
5. **Toolbox module coverage (one module at a time):**
   - **Gehtsoft.Mapper: 76.34% → 93.81%** — `TestMapperCoverage.cs` (24 tests): interface-typed
     fluent overloads, `MappingAction` predicate helpers, both property/action collections (incl.
     the unused non-generic one + explicit interface members), filtered `MapPropertiesByName`,
     positional `MapPropertyAttribute` ctors, `ClassToModelInitializer` error/skip branches, map
     guards, `Map` equality/`Find`, `ValueMapper` edges. Remaining ~5 lines are defensive/dead.
   - **Gehtsoft.Validator: 80.88% → 90.21%** — `TestValidatorCoverage.cs` (15 tests): fluent
     `Null/DoesNotMatch/PhoneNumber/CreditCardNumber` builders + predicates, `Otherwise()` branches,
     entity-level `When` overloads, Always/Never predicates, `MustBeNotEmpty` attribute rule,
     `BaseValidator` enumeration, extra `ValidationFailure` ctors, unused rule/predicate collections'
     read surface. Remaining ~99 lines are mostly mechanical accessors, JS-gen catch blocks, and
     `internal` mutators on unused collections (test project has no InternalsVisibleTo).
     **Bug found & fixed**: `ValidatorAttributeBase.WidthCode` was `int?` (illegal as an attribute
     argument, so attribute-supplied rule codes were unreachable). Renamed to plain-`int` `WithCode`
     with an auto-set `bool HasCode`; `BaseValidator` + `EfModelValidator` updated to test `HasCode`.
     Logged in KNOWN_BUGS.md; verified by the `MustBeNotEmpty`/`WithCode` tests.
   - **Gehtsoft.EF.Mapper: 77.78% → 97.11%** — `TestEfMapperCoverage.cs` (13 tests): `EfMap<,>`
     ctor (entity + non-entity destination) and `GetTargetByName` override (lookup by property name,
     DB column name and column ID; non-column base fallthrough; missing-name); `EntityMapInitializer`
     `SourceToModel`/`ModelToSource` guard, skip and not-found branches (entity-type mismatch throw,
     `[DoNotAutoMap]` model property and `[DoNotAutoMap]` column skips, name-not-found throw,
     missing-`[MapEntity]` throw); the null-obj guards of `EntityPrimaryKeySource`/`ModelPrimaryKeySource`.
     Remaining ~5 lines are mechanical ctors/accessors.
   - **Defect found AND fixed** (`ContainsRuleFor`/`Find` couldn't discover `EfMap` column rules nor
     `EntityMapInitializer.ModelToSource` auto-rules, because `EntityPropertyAccessor` never equalled
     the `ClassPropertyAccessor` the lookups build): (a) `EfMap.GetTargetByName` now resolves a column
     by ID/DB-name and returns the base `ClassPropertyAccessor` target for the CLR property;
     (b) `EntityPropertyAccessor.Equals`/`GetHashCode` now compare by property identity (Name+ValueType)
     so it matches the equivalent `ClassPropertyAccessor`. Stored target type unchanged, so
     `Gehtsoft.EF.Mapper.Validator` (which reads `ColumnInfo` off `property.Target is EntityPropertyAccessor`)
     is unaffected — confirmed by the full 169-test suite incl. `TestModelValidator`/`TestJsConvertor`.
     Both changes confined to `Gehtsoft.EF.Mapper`; no base-lib/public-API change. See KNOWN_BUGS.md.
     **Watch-out captured**: the validator bridge depends on the target being an `EntityPropertyAccessor`
     — do not swap it for `ClassPropertyAccessor` (an earlier attempt broke 8 validator tests).
   - **Gehtsoft.EF.Validator: 69.33% → 95.38%** — `TestEfValidatorCoverage.cs` (10 tests): the
     stand-alone `EfPredicateFactory.GetPredicates` helper (all column kinds incl. both enum branches),
     `AddDbValidation` non-nullable-enum branch, `ValidatorConnectionFactory` (sync/async delegate
     wrapper, both ctors, all Get overloads), `Number`/`DecimalPropertyRangePredicate.Validate`
     (null/in-range/out-of-range/int-conversion) + their `RemoteScript`, `DatabasePredicate`
     owned-connection dispose branch + null `RemoteScript`/`Server` side (via a NeedToDispose=true
     factory around `IsUniquePredicate`), `DefaultEfValidatorMessageProvider` fallbacks (unknown
     language → en, unknown code → -1), and the otherwise-unused `EntityPropertyTarget`. Remaining
     ~7 lines are unreachable defensive code: the `Nullable.GetUnderlyingType(value.GetType())` branch
     in the two range predicates can never fire because a boxed `Nullable<T>` unboxes to its
     underlying type. Reuses `TestEntityValidators` entities/`DummySqlLanguageSpecifics`.
   - **Gehtsoft.EF.Mapper.Validator: 81.05% → 95.75%** — `TestEfMapperValidatorCoverage.cs` (8 tests):
     the `EfModelValidator` ctor attribute-`WithCode` branches (all four Must* attributes), the
     `ValidateModel` throw (no `[MapEntity]`) + aspNet/null-message-provider rule-building path,
     every `RuleBuilderExtension` guard throw (no `[MapEntity]` → ArgumentException; unmapped
     property → InvalidOperationException, ×4 rules), `AddUnique`'s `UnlessValue` branch, the
     `GetLanguageSpecifics`/`GetConnectionFactory` null-return paths (rule used on a plain
     `AbstractValidator`, not an `EfModelValidator`), and the two obsolete `MustBeUnqiue` overloads.
     Reuses `TestModelValidator` entities/models/`DummySqlSpecifics`. Remaining ~1 line + 12 partials
     are OR-branch alternatives (`AnsiString`/`StringFixedLength` DbType variants, accessor
     type-check false sides) — defensive, not chased. No product bugs found.
   - **All four modules the user asked about are done.** Overall toolbox line coverage **94.22%**
     (187 tests). Lowest remaining module is `Gehtsoft.Validator` at 89.92% (base library, larger).
6. Carried over from prior session: ~~docgen pass for `RuleExecutionSide`/`SetSide`~~ **DONE** —
   `doc/src/validator/Gehtsoft.Validator.RuleExecutionSide.ds` (new enum doc), `SetSide` member added
   to `ValidationRuleBuilder.ds` (+ `ServerOnly` cross-link), and a new "Two-side validation" article
   (`validationarticle8`) in `doc/src/validator/index.ds`. Docs build clean (only pre-existing
   unknown-key warnings; none from the new content). NOTE: on Linux/WSL the docgen build fails at
   project load because `project.xml` uses Windows `\` folder paths (`src\validator`); validated
   locally via temporary backslash-named symlinks (removed afterward) — build normally on Windows.
   Still open: ExpressionToJs package fixes (separate repo).
