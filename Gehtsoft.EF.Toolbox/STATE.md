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
3. Remaining Serialization coverage (~17%): `FileBlobAccessor`, the Stream/StringWriter ctors,
   cancellation-token + frame-paging paths, and guard clauses. Optional JSON indentation option
   and a large-blob path for Binary were offered but not implemented.
4. Carried over from prior session: docgen pass for `RuleExecutionSide`/`SetSide`; ExpressionToJs
   package fixes (separate repo).
