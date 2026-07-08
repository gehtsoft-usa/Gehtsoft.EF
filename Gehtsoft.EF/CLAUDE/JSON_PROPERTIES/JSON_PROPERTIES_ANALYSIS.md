# JSON Entity Properties — Options Analysis

*Research performed 2026-07-08. Companion to the Dynamic-Properties (EAV) work
(`../DYNAMIC_PROPERTIES/`). Implementation not started; this is the information-gathering +
options document that precedes `JSON_PROPERTIES_PLAN.md`. **No decisions are final until the
user confirms them** (see "Decisions — proposed" at the end).*

## Requirements (from the user, 2026-07-08)

A **JSON property** on a Gehtsoft.EF entity — a CLR value serialized to a single JSON column,
deserialized automatically on load, filterable/sortable/projectable/groupable by individual
values inside it.

1. **JSON property (scalar/whole value "translated to string")** — a CLR member whose value is
   serialized to a JSON string column.
2. **JSON object marked up with `System.Text.Json`** — a POCO (with `[JsonPropertyName]` etc.)
   serialized to the same column.
3. **Basic value types only** inside the JSON: `bool`, `short`, `int`, `string`, `float`,
   `double`, `money` (`decimal`), `DateTime`, `byte[]`. **Arrays of primitives** if they fit
   cross-3-platform compatibility.
4. **Nullable option** supported.
5. **Serialization/deserialization is automatic on load/save of the whole field.**
6. **Mass update of individual JSON values is OUT of scope.**

**Driver scope — deliberately three drivers: SQLite, PostgreSQL, Oracle 12+.** Excluded: MSSQL
and MySQL, because on those two `SupportFunctionsInIndexes == false` — indexing an extracted
JSON value there requires materialising an **artificial computed/generated column**, which is
exactly the complexity the user wants to avoid. The three chosen drivers all report
`SupportFunctionsInIndexes == true`, so a JSON value can be indexed with a **function-based /
expression index** and the whole feature stays "function-use only", no schema tricks.

## Relationship to the EAV feature

The EAV analysis (`../DYNAMIC_PROPERTIES/DYNAMIC_PROPERTIES_ANALYSIS.md`) evaluated the very
same storage fork and chose **Option B (EAV side table)** *because* it needed all 5 drivers with
zero dialect work. It listed **Option A (native JSON column, queried in-place)** and documented
its cost: *"5 extraction syntaxes, 3 indexing strategies, all greenfield"* — but that cost
assumed all 5 drivers. **By scoping to the 3 drivers where function indexes work, Option A
collapses to 3 extraction syntaxes and ONE indexing strategy (expression index).** That is what
makes this feature tractable and distinct from EAV. This document is Option A, done right, for 3
drivers.

The two features are complementary, not competing: EAV = per-row ad-hoc bag, all drivers,
typed side-table search. JSON = structured document per column, 3 drivers, native in-place
search. The **query-surface machinery is shared** — see below.

## Codebase facts that shape the design (verified by exploration, 2026-07-08)

*(All paths relative to repo root `Gehtsoft.EF/`. Line numbers as of 2026-07-08.)*

### Value pipeline & the serialization hook — clean and transparent
- A CLR property becomes a column in `ColumnDiscoverer.CreateColumnDescriptor`
  (`Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/ColumnDiscoverer.cs:10-165`). Auto type
  inference runs only when `EntityPropertyAttribute.DbType == DbType.Object` (`:59`); a POCO type
  matches none of the known-type branches, so today it would fall through and misbehave (`:146-162`).
  **This is the single interception point.**
- Every column carries an `IPropertyAccessor` (`QueryBuilder/TableDescriptor.cs:14-44`;
  `ColumnInfo.PropertyAccessor` `:152`). Both write and read funnel through it and use its
  `PropertyType`:
  - **Write:** `UpdateQueryToTypeBinder.BindAndExecuteCore` reads `accessor.GetValue(entity)`
    (`UpdateQueryToTypeBinder.cs:414`), then `SqlQuery.BindParam` → `ToDbValue`
    (`SqlLanguageSpecifics.cs:420-503`) — an unknown CLR type throws `TypeIsUnsupported` (`:501`),
    so the value **must already be a `string`** here.
  - **Read:** `SelectQueryResultBinder.Read` does
    `query.GetValue(colIndex, accessor.PropertyType)` (`SelectQueryResultBinder.cs:335-338`) then
    `accessor.SetValue(entity, r)` (`:343`).
- ⇒ **A decorating accessor `JsonPropertyAccessor : IPropertyAccessor`** whose
  `PropertyType => typeof(string)`, `GetValue` = serialize the real value, `SetValue` = deserialize
  the string into the real POCO, makes **both directions work with zero changes** to binders,
  `SqlQuery`, or `SqlLanguageSpecifics`. Install it (and force `DbType.String`, `Size = 0`) in
  `ColumnDiscoverer` when the JSON marker is present. This is the mechanism for requirement 5
  (and covers 1 + 2 identically).
- **No converter/serializer abstraction exists today** (`EntityPropertyAttribute` has no such
  hook; a solution-wide search found none). `System.Text.Json` is **not** referenced by any
  library project. Both `Gehtsoft.EF.Entities` and `Gehtsoft.EF.Db.SqlDb` target `netstandard2.0`
  — which does **not** ship `System.Text.Json` in-box, so a `System.Text.Json` NuGet
  **PackageReference is a prerequisite** (build/packaging change → the user manages packaging).

### Storage type — store JSON as a plain string column
- Per-driver `TypeName(DbType.String, size=0, ...)`: SQLite → `TEXT`
  (`SqliteLanguageSpecifics.cs:10-37`); Postgres → `text` (`PostgresLanguageSpecifics.cs:9-71`);
  Oracle → `clob` (`OracleDbLanguageSpecifics.cs:11-68`). All three JSON function sets operate on
  a plain string/CLOB (SQLite `json_extract` on TEXT; Oracle `JSON_VALUE` on VARCHAR2/CLOB in
  12c+; Postgres via a `::jsonb` cast in the expression). So **no new DB "JSON type" is needed**;
  the column is an ordinary unbounded string. This is exactly what the accessor produces on write
  (a plain string parameter — no per-driver parameter-type juggling), and JSON-ness is applied
  **only in query/index expressions**. (jsonb as a stored column type was considered and rejected:
  it forces per-driver parameter typing on write and buys nothing here, since expression indexes
  materialise the extracted value anyway.)

### Dialect / JSON-function mechanism — greenfield, small
- `enum SqlFunctionId` (`SqlLanguageSpecifics.cs:821-850`, 27 values) + `virtual
  GetSqlFunction(SqlFunctionId, string[] args)` (`:520-622`, default returns `null` =
  unsupported), overridden per driver. Casts (`ToInteger`/`ToDouble`/…) already emit
  `CAST(...)`/`STRFTIME`/`EXTRACT` per driver — the exact mechanism a JSON target-type cast
  reuses. There is **no JSON function id today** (confirmed: zero `json_extract`/`jsonb`/`->>`/
  `JSON_VALUE` hits in production code).
- Per-driver JSON extraction we will emit:
  | Driver | Extract expression | Notes |
  |---|---|---|
  | SQLite | `json_extract(col, '$.path')` | JSON1 built into modern Microsoft.Data.Sqlite — **no UDF needed** |
  | Postgres | `(col::jsonb #>> '{path,parts}')` or `(col::jsonb ->> 'key')` | cast text→jsonb inline; `#>>`/`->>` return text |
  | Oracle 12+ | `JSON_VALUE(col, '$.path' RETURNING …)` / `JSON_EXISTS` | works on VARCHAR2/CLOB |
  Extraction returns **text**; the target-type `CAST` is layered on top (numeric/decimal); bool
  and DateTime need conventions (see "Type support" below).
- **Injection seams (all already used by the shipped EAV `DynamicPropertyOf<T>` code):**
  - WHERE/HAVING: `SingleConditionBuilder.Raw(expr, dbType).Is(op).Value(v)` and the `Wrap(…)`
    extension over `GetSqlFunction` (`ConditionBuilder.cs:332-378`).
  - Projection: `SelectQueryBuilder.AddExpressionToResultset(expr, dbType, isAggregate, alias)`
    (`:235-248`), entity-level `SelectEntitiesQueryBase.AddExpressionToResultset(...)` (`:760`).
  - ORDER BY / GROUP BY: `AddOrderByExpr(expr, dir)` (`:455-463`), `AddGroupByExpr(expr)`
    (`:465-474`). **`BuildGroupBy` matches on the exact expression string** (`:637-681`;
    `AllNonAggregatesInGroupBy` true for Postgres/Oracle) — so the JSON extraction string **must
    be byte-identical** everywhere it appears. Build it once, cache, reuse (the EAV
    `DynamicPropertyJoin.ColumnAlias` pattern, `DynamicPropertyProjection.cs:61-82`).
  - Read-back: `SelectEntitiesQueryBase.BindOneDynamic` reads a projected expression by index and
    converts via `mResultsetTypes[i]` (`:581-601`); a decode registry
    (`mDynamicPropertyColumns`) already exists for encoded values — the same pattern serves JSON
    bool/DateTime decode if needed.
  - Column-alias resolution inside an extension: `condition.BaseQuery.ConditionQueryBuilder
    .Alias(columnId, out DbType)` → `"entityN.col"` (the FTS/EAV pattern) — wrap it as
    `json_extract(entityN.col, '$.path')`.
- ⇒ **Query surface is a near-mechanical adaptation of the shipped `DynamicPropertyOf<T>`
  feature**: introduce a `JsonValueOf<T>(column, path, type)` analog that produces one canonical
  extraction+cast string and routes it through the identical five seams.

### Indexing — emission is mostly there; the FIELD MODEL and CHANGE DETECTION are not
- Composite/expression indexes are declared **only** via a class implementing
  `ICompositeIndexMetadata` (`Metadata/ICompositeIndexMetadata.cs:9`) referenced from
  `[Entity(Metadata = typeof(...))]`; instantiated in `StandardEntityDiscoverer.cs:39`, stored on
  `TableDescriptor.Metadata`. Emitted at CREATE TABLE time
  (`CreateTableBuilder.PrepareQuery` → `HandleCompositeIndex`/`HandleCompositeIndexColumns`,
  `:90-145`) and by the standalone `CreateIndexBuilder` (`:73-90`).
- `CompositeIndex.Field` = `(SqlFunctionId? Function, string Name, SortDir)`
  (`Metadata/CompositeIndex.cs:24-32`). A function field renders
  `GetSqlFunction(fn, [columnName])` — **one function of one column, no argument channel for a
  JSON path.** ⇒ the index field model must be **extended to carry a JSON path** (and target
  type) — the single real gap on the emission side. `SupportFunctionsInIndexes` is already `true`
  on all three targets.
- **THE HARD PART — schema-update index change detection does not exist:**
  - `CreateEntityController.UpdateTables` (`CreateEntity/CreateEntityController.cs:291-436`)
    **never enumerates, adds, or drops indexes.** Its own doc comment (`:30-34`) explicitly
    disclaims *"A new index is added via `ICompositeIndexMetadata`"* as a case it does not
    recognise. It only adds missing **columns** (`schema.Contains(table, column)`), drops
    `[ObsoleteEntityProperty]` columns, and reconciles the EAV side **table** — never that
    table's indexes.
  - `connection.Schema()` returns **tables + views + columns only — no indexes**
    (`SqliteConnection.SchemaCore` / `PostgresConnection` / `OracleConnection`).
  - `DoesObjectExist(table, name, "index")` can probe **one named** index on all three drivers
    (`pg_indexes` / `SQLITE_MASTER type='index'` / `ALL_INDEXES`), but there is **no
    "enumerate all indexes on a table" API**. A pure probe cannot discover an index whose
    declaration was *deleted* (its name is no longer known).
  - `GetCreateIndexBuilder` (`SqlDbConnection.cs:211`) / `GetDropIndexBuilder` (`:370`) exist and
    are driver-overridable (Postgres overrides drop) but are **not wired into the controller** —
    only tests use them.
  - ⇒ To answer the user's "**how do we recognise when the set of JSON indexes changed?**" we
    must add **new machinery**: (a) a per-driver *enumerate indexes on a table* helper, plus
    (b) a reconcile pass in `UpdateTables` that diffs the entity's declared JSON-index set against
    the DB's actual indexes (keyed by a deterministic name) and issues create/drop. This is the
    single largest new subsystem in the plan (its own phase). A deterministic name prefix
    (`<table>__json_…`) makes both the diff key and the "is this one of ours" filter unambiguous
    — mirroring the fixed-`TableSuffix` trick EAV used for orphan detection.

## Type support — two tiers

| CLR type | Whole-field (serialize/load) | Individual-value query / index |
|---|---|---|
| `string` | ✅ | ✅ (text compare) |
| `short`,`int`,`long` | ✅ | ✅ (cast to integer) |
| `float`,`double`,`decimal`(money) | ✅ | ✅ (cast to real/decimal) |
| `bool` | ✅ | ⚠️ convention needed (JSON `true`/`false` vs SQLite `json_extract`→`1/0`; Postgres jsonb bool; Oracle) |
| `DateTime` | ✅ (ISO-8601 via `System.Text.Json`) | ⚠️ compare as **ISO-8601 string** (lexical == chronological for a fixed UTC format) |
| `byte[]` | ✅ (base64 string) | ❌ whole-field only |
| arrays of primitives | ✅ (if cross-driver, requirement 3 "consider") | ❌ whole-field only (v1) |
| nested object / POCO | ✅ | value-by-value only via explicit path; nested object as a whole ❌ |

**Whole-field serialization supports every requested type** (it is just `System.Text.Json`).
**Individual-value querying/indexing is the narrower, well-defined subset** (scalars with a clean
cross-3-driver extract+cast). `byte[]`, arrays and whole nested objects are load/save only. The
`bool` and `DateTime` querying conventions are the two items to pin during Phase 4 (WHERE) with
per-driver round-trip tests before promising them.

## Nullable (requirement 4)

- A **nullable JSON property** whose CLR value is `null` ⇒ the accessor returns `null` ⇒
  `BindNull` ⇒ the column is SQL `NULL` (not the JSON text `"null"`). On read, SQL `NULL` ⇒ CLR
  `null`. Clean and distinct from a JSON `null` literal *inside* a stored document.
- A **missing/`null` path inside** a stored document: extraction yields SQL `NULL` ⇒ the value
  drops out of predicates (`NULL {op} x` → UNKNOWN) and sorts per the driver's NULL ordering —
  same semantics the EAV feature already documents.

## Out of scope (v1) / v2

- **Mass update of individual JSON values** (requirement 6, explicit). Whole-field update (replace
  the entire document) is fully supported via normal `UpdateEntityQuery`.
- Mass **delete** *is* in scope (WHERE on a JSON value in a `DELETE`) and is **simpler than EAV's**:
  the predicate is a function on the entity's own column (`json_extract(owner.data,'$.p') > 18`),
  so it lives directly in the `DELETE` with no side-table cascade and none of the EAV
  "one-predicate-two-statements" problem (`../ENTITY_WHERE_PROBLEM.md`). MySQL's self-reference
  limit doesn't apply (no self sub-query).
- Querying inside arrays / nested-object-as-whole / JSON containment operators.
- MSSQL / MySQL (would need computed/generated columns).
- LINQ `ExpressionCompiler` support for JSON paths (v2, same seam as EAV).
- Mongo/Bson (excluded, like EAV).

## Decisions (all confirmed with the user, 2026-07-08)

1. **Storage = one plain string column** on the entity table (TEXT/text/clob), JSON-ness applied
   only in query/index expressions.
2. **Serialization hook = a decorating `JsonPropertyAccessor`** installed at discovery;
   `System.Text.Json`; automatic for load/save; add the `System.Text.Json` package to
   `Gehtsoft.EF.Db.SqlDb` (user manages packaging).
3. **Marker API = a dedicated `[JsonEntityProperty]` attribute** (carries JSON-only options:
   nullable, serializer options); the general `EntityPropertyAttribute` is left untouched.
4. **JSON-index declaration = per-path attribute** `[JsonIndex("$.age", DbType.Int32, Unique=…)]`
   on the JSON property (repeatable). An index targets **one primitive value path** ("per Json
   object prop"), not the whole column; the internal index-field model is extended to carry a
   path + target type.
5. **Index change detection = new per-driver *enumerate-indexes* helper + a reconcile diff in
   `UpdateTables`**, keyed by a deterministic JSON-index name prefix. (The only robust answer;
   documented as the feature's largest new subsystem.)
6. **Query surface = a `JsonValueOf<T>(column, path, type)` family** mirroring the shipped
   `DynamicPropertyOf<T>`, routed through the identical WHERE/projection/ORDER BY/GROUP BY/HAVING
   seams; one canonical cached extraction string per (column, path, type).
7. **Queryable / indexable value types = primitives only** — `string`, integer types, reals
   (`float`/`double`/`decimal`/money), `bool`, `DateTime`. A path may reach a primitive **inside a
   nested object**; that primitive is queryable/indexable. **No** indexing/querying of arrays,
   `byte[]`, or a whole nested object (whole-field load/save only). `bool`/`DateTime` cross-driver
   conventions are validated by per-driver round-trip tests in Phase 4 before they ship.

→ Implementation plan: `JSON_PROPERTIES_PLAN.md`.
