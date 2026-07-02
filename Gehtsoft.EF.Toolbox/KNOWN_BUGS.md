# Known bugs / limitations — Gehtsoft.EF.Toolbox

Findings recorded while building integration coverage. These are limitations of the
shipped product, not of the tests.

## Gehtsoft.EF.Serialization

No open issues. (Historical items below are kept for context.)

## Gehtsoft.EF.Mapper

No open issues. (Historical items below are kept for context.)

## Resolved

### ~~`EfMap<,>` / `EntityMapInitializer.ModelToSource` rules undiscoverable by `ContainsRuleFor`/`Find`~~ — FIXED
`Map.ContainsRuleFor(name)` / `Find(name)` resolve a lookup target via `GetTargetByName(name)` and
match it (`IMappingTarget.Equals`) against each stored rule's `Target`. Both accessor `Equals`
implementations short-circuited on `obj.GetType() != this.GetType()`, so an `EntityPropertyAccessor`
never equalled a `ClassPropertyAccessor`, even for the same property. Two consequences:

1. `EfMap.GetTargetByName` returned an `EntityPropertyAccessor` for an entity column, while the only
   public rule-registration paths (`For(name)`/`For(expr)`) build a `ClassPropertyAccessor` — so
   `ContainsRuleFor`/`Find` on an `EfMap` were always false/empty and `MapPropertiesByName` could add
   duplicate rules. (`EfMap` is otherwise unreferenced/experimental.)
2. `EntityMapInitializer.ModelToSource` stores `EntityPropertyAccessor` targets (required —
   `Gehtsoft.EF.Mapper.Validator` recovers `ColumnInfo` metadata via
   `property.Target is EntityPropertyAccessor`), so those rules mapped correctly but were
   undiscoverable by name.

**Fix** (both changes confined to `Gehtsoft.EF.Mapper`; no base-library or public-API change,
validator behavior preserved):
- `EfMap.GetTargetByName` now resolves a column by ID **or** DB name and returns the *base* target
  for the CLR property name (a `ClassPropertyAccessor`), matching what `For(...)` stores — and adds
  lookup by DB column name/ID as a bonus.
- `EntityPropertyAccessor.Equals` (and `object.Equals`/`GetHashCode`) now compare by property
  identity (`Name` + `ValueType`) rather than `ColumnInfo` reference + type, so an
  `EntityPropertyAccessor` matches the equivalent `ClassPropertyAccessor` the lookups build. The
  stored target type is unchanged, so the validator still sees an `EntityPropertyAccessor`.

Verified by `TestEfMapperCoverage` (positive `ContainsRuleFor` assertions incl. DB-column-name and
column-ID lookup) and the full 169-test suite, including `TestModelValidator`/`TestJsConvertor`
which exercise the validator's `ColumnInfo` metadata path. Known limitation: cross-type equality is
one-sided (`ClassPropertyAccessor.Equals`, in the base assembly, cannot recognize an
`EntityPropertyAccessor`); this is sufficient because after the fix every lookup key is a
`ClassPropertyAccessor`.

### ~~Validation attribute code (`WidthCode`) could never be set~~ — FIXED
`ValidatorAttributeBase.WidthCode` was typed `int?`. C# forbids nullable types as attribute
arguments, so `[MustBeShorterThan(WidthCode = 5)]` (and every other `MustBe…` / EF DB attribute)
failed to compile — the attribute-supplied validation code was unreachable and the `WithCode`
branch in `BaseValidator.AddRule` / `EfModelValidator` was dead. Renamed to a plain-`int`
`WithCode` property with an auto-set `bool HasCode` companion (set true whenever `WithCode` is
assigned); consumers now test `HasCode` instead of a null check. Not a breaking change in practice
since the old property could never be assigned. Verified by
`TestValidatorCoverage.MustBeNotEmpty_Attribute_Is_Enforced` (code 42 now flows through) and
`ValidatorAttribute_WithCode_Sets_HasCode`.

### ~~`XmlEntityReader` crashed on indented / pretty-printed XML~~ — FIXED
`XmlEntityReader.Scan` appended every text node to `mStack.Peek()`. The root `<es>` element
is consumed by `MoveToContent()` in the ctor and never pushed onto the stack, so any
insignificant whitespace an indented document places between `<es>` and its first child
arrived while the stack was empty and threw `InvalidOperationException: Stack empty`. The
same applied to stray top-level CDATA/text/entity-reference nodes. The text-node cases now
guard on `mStack.Count > 0` and ignore ownerless top-level nodes. Verified by
`TestSerializationCoverage.XmlReader_Tolerates_Indented_Document` (writes an indented UTF-8
document and reads the full graph back).

### ~~No Binary / JSON storage format~~ — ADDED
The library now ships four `IEntityReader`/`IEntityWriter` pairs: `IO/Db`, `IO/Xml`,
`IO/Binary` and `IO/Json`. All four share one scalar type set (see resolved item 2) and
identify entity types by **EF scope + table name** rather than assembly-qualified type
name — the XML format was migrated to the same scheme. Readers (XML/Binary/JSON) take an
`EntityFinder.EntityTypeInfo[]` to resolve scope/name, mirroring `DbEntityReader`. Round-trip
coverage in `TestSerializationRoundTrip` exercises DB ⇄ XML, DB ⇄ Binary and DB ⇄ JSON.

### ~~2. XML/`TextFormatter` type set narrower than the DB type set~~ — FIXED
`TextFormatter` now supports `long` (type code `q`) and `Guid` (type code `g`) in addition
to the previously-handled types, so the XML/Blob path covers every type the DB path can
store. Verified by `TestSerializationRoundTrip` (the entity graph now carries `long`,
`Guid?` and `float` columns through DB → XML → DB) and the `TextFormatter_RoundTrips_*`
unit tests. The new type codes are additive, so previously-written XML stays readable.

### ~~3. `short` / `float` not auto-detected as entity property types~~ — FIXED in EF 1.9.5.1
EF's `ColumnDiscoverer` now auto-detects `short → DbType.Int16` and `float → DbType.Single`
(the latter mirroring `double`: default size 18 / precision 7). `DbType.Single` was also
added to every driver's DDL `TypeName` mapping (SQLite/MSSQL/MySQL/Oracle/Postgres) plus
`TypeToDb`, `ToDbValue` and the truncation binder. The Toolbox consumes this via the
`Gehtsoft.EF.* 1.9.5.1` packages. Verified in the EF repo by
`Entity/Discovery/SingleAndShortTypeTest` and here by the round-trip entities using bare
`[EntityProperty]` on `short`/`long`/`float`.
