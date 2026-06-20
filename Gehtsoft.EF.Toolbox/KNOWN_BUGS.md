# Known bugs / limitations — Gehtsoft.EF.Toolbox

Findings recorded while building integration coverage. These are limitations of the
shipped product, not of the tests.

## Gehtsoft.EF.Serialization

No open issues. (Historical items below are kept for context.)

## Resolved

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
