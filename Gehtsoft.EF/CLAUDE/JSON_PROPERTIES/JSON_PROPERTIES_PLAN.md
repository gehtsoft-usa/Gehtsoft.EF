# JSON Entity Properties — Implementation Plan (draft, pre-approval)

*Planned 2026-07-08. Options analysis + verified codebase facts: `JSON_PROPERTIES_ANALYSIS.md`.
Implementation NOT started. This plan reflects the **proposed** decisions in that document and
becomes final only after the user confirms the open forks (marker API, index-declaration API,
queryable-type scope). Process mirrors the EAV feature (`../DYNAMIC_PROPERTIES/`): overall plan
approved first, then a per-phase plan approved before each phase is coded.*

## Context

A **JSON property** is an entity member whose CLR value (a primitive, a `byte[]`, a primitive
array, or a `System.Text.Json`-marked POCO) is serialized to a **single string column** on the
entity's own table and deserialized automatically on load. Individual values inside the document
are filterable/sortable/projectable/groupable via native per-driver JSON functions, and can be
**indexed per value path**. Scope: **SQLite, PostgreSQL, Oracle 12.2+ (18c)** only (the drivers where
`SupportFunctionsInIndexes == true`, so an extracted value is indexable with a function/expression
index — no computed/generated columns). This is **Option A** (native JSON, queried in place) from
the EAV analysis, made tractable by the 3-driver scope.

*Oracle floor is **12.2+/18c** (128-char identifiers): the auto-derived JSON index name
`<table>_<col>_<path>_<type>` can exceed the 30-char limit of Oracle 11g/12.1, which are retired
(NG, 2026-07-08). Index naming goes through the new overridable
`SqlDbLanguageSpecifics.IndexName(table, logical)` (default `<table>_<logical>`); at 128 chars it
stays a plain reversible concat with no hashing.*

## Decisions (all confirmed with the user 2026-07-08)

1. Storage = one plain string column (TEXT/text/clob); JSON-ness only in query/index expressions.
2. Load/save = a decorating `JsonPropertyAccessor` (`System.Text.Json`), transparent to all CRUD;
   `System.Text.Json` package added to `Gehtsoft.EF.Db.SqlDb` (user manages packaging).
3. Mass **update** of individual JSON values is out of scope; whole-field update supported.
4. Query surface mirrors the shipped `DynamicPropertyOf<T>` (`JsonValueOf<T>(column, path, type)`).
5. Index change detection = new per-driver *enumerate-indexes* + reconcile diff in `UpdateTables`.
6. **Marker API = dedicated `[JsonEntityProperty]`** attribute (carries JSON-only options:
   nullable, serializer options) — the general `EntityPropertyAttribute` is left untouched.
7. **Index declaration = per-path attribute** `[JsonIndex("$.path", DbType, Unique=…)]` on the
   JSON property (repeatable). An index targets **one primitive value path**, not the whole
   column. The internal index-field model is extended to carry a JSON path + target type.
8. **Queryable / indexable value types = primitives only**: `string`, integer types, reals
   (`float`/`double`/`decimal`/money), `bool`, `DateTime`. A path may reach a primitive **inside a
   nested object** (`"$.address.zip"`) — that is queryable/indexable. **No** indexing/querying of
   arrays, `byte[]`, or a whole nested object (those remain whole-field load/save only).
   `bool`/`DateTime` cross-driver extract+compare conventions are still validated by per-driver
   round-trip tests in Phase 4 before they ship (prudence, not a scope question).

## Column / storage shape

- On the **owner entity's own table** (no side table), one column per JSON property:
  `TypeName(DbType.String, size=0)` → SQLite `TEXT`, Postgres `text`, Oracle `clob`. Nullable per
  the property's nullable option. No DB "JSON type", no `IS JSON` constraint in v1 (Oracle
  `JSON_VALUE`/`JSON_EXISTS` work on VARCHAR2/CLOB without it — to be re-confirmed in Phase 1).
- CLR `null` ⇔ SQL `NULL` (never the text `"null"`). Distinct from a JSON `null` inside a document.

## Query translation (shared with EAV machinery)

- New `SqlFunctionId.JsonValue` (+ possibly `JsonExists`), rendered per driver in `GetSqlFunction`:
  `json_extract(col,'$.p')` (SQLite) / `(col::jsonb #>> '{p}')` (Postgres) /
  `JSON_VALUE(col,'$.p' RETURNING …)` (Oracle). Path emitted as a **literal** (SQLite/Oracle can't
  bind it), so it passes through the existing `ContainsScalar()` / `FormatValue` quote guards —
  path validation/escaping is part of the design (esp. Oracle `EXECUTE IMMEDIATE '...'`).
- `JsonValueOf<T>(string column, string path, EfType type)` builds ONE canonical, cached
  extraction+cast string (the `DynamicPropertyJoin.ColumnAlias` pattern) and routes it through:
  WHERE/HAVING `Raw(expr, dbType).Is(op).Value(v)`; projection `AddExpressionToResultset`;
  `AddOrderByExpr` / `AddGroupByExpr` — **byte-identical string everywhere** (GROUP BY matches by
  string). Read-back reuses the `mResultsetTypes` / decode-registry path.
- Semantics: absent/`null` path → SQL `NULL` → excluded from predicate, driver NULL ordering
  (documented, as EAV).

## Testing model (mirrors EAV — two tiers, every slice)

- **Deep / debug tier — SQLite only.** White-box: the column exists with the right type; each JSON
  **index** exists/absent via `DoesObjectExist(...,"index")`; the exact **generated extraction
  SQL** (parsed to AST via `.ParseSql()`, never string `Contains`); round-trip document contents.
- **Acceptance tier — the 3 drivers.** `[Theory][MemberData(nameof(ConnectionNames))]` +
  `IClassFixture<SqlConnectionFixtureBase>` (template `SqlDb/FtsTest.cs`); SQLite always on,
  Postgres/Oracle per local config. Assert observable behaviour (values, counts, exact DateTime),
  not SQL text.
- Tests under `Gehtsoft.EF.Test/JsonProperties/{Entities,TableManagement,DataManagement,DataSelecting}`
  (test csproj uses default compile items — no `<Compile Include>` needed, per EAV experience).

## Delivery — phases (finish-before-advance; each phase planned in `PHASE_N/` then approved)

Two human gates per the EAV working agreement: (1) approving each phase's plan, (2) advancing to
the next phase. The sequence follows the user's 8-step ordering.

> **Prerequisite — ✅ DONE (2026-07-08):** the general index-reconciliation fix in
> `../INDEX_RECONCILIATION_PROBLEM.md` has landed (all 3 stages, full suite green). `UpdateTables`
> now reconciles indexes (add/owned-drop/change) via `GetTableIndexes` + `CompositeIndex.ExcludeFor`.
> JSON's index handling (Phase 2) is now just a consumer/extension of it.

- **Phase 0 — prerequisites & foundation.** `System.Text.Json` package reference (user approves);
  `[JsonEntityProperty]` + repeatable `[JsonIndex]` attributes; `JsonPropertyAccessor`
  (serialize/deserialize, `PropertyType=string`); `ColumnDiscoverer` interception (force
  `DbType.String`, size 0, install accessor); recognition + declared-index list on
  `EntityDescriptor`; Mongo/Bson exclusion. Deep tests: accessor round-trip for every
  requirement-3 type + nullable + a marked-up POCO + a primitive array; `[JsonIndex]` parsing
  (path + type, including a nested-object path). *(User's step 1 — entity-level support.)*
- **Phase 1 — table create with JSON indexes.** Column DDL on all 3 drivers; the index-field model
  extended to carry a JSON path+type; JSON expression indexes emitted at CREATE TABLE
  (`CreateTableBuilder`) with a deterministic name prefix. Deep: column present, each declared
  JSON index present (`DoesObjectExist`), Oracle `JSON_VALUE`-on-CLOB indexability confirmed.
  Acceptance: create/drop across 3 drivers. *(User's step 2.)*
- **Phase 2 — table update / index reconciliation** *(depends on the PREREQUISITE fix)*. The
  general index-reconciliation machinery — per-driver enumerate-indexes helper + `UpdateTables`
  add/drop diff — is **hoisted out of this feature** into a standalone prerequisite that must land
  first: see `../INDEX_RECONCILIATION_PROBLEM.md` (the defect that `UpdateTables` reconciles no
  indexes at all today). This phase then only **extends** it for JSON: add a path+type-carrying
  field to the index model and confirm JSON expression indexes (reserved `<table>_<col>_<pathslug>`
  names) are added/dropped by the shared reconciler. Deep: gained/removed JSON index →
  created/dropped; unchanged → no-op; non-JSON indexes untouched. Acceptance: add/remove a JSON
  index across the 3 drivers. *(User's step 3 — "how to recognise when the index set changed":
  answered by the prerequisite's enumeration + name-keyed diff.)*
- **Phase 3 — CRUD (Insert/Update/Delete/MassDelete).** Whole-document insert/update/delete via the
  transparent accessor (largely free from Phase 0); MassDelete with a JSON-value WHERE (predicate
  on the owner's own column — no cascade). Deep: row contents per type, null handling, MassDelete
  SQL (AST). Acceptance: full-type round-trip incl. nullable + POCO + array; MassDelete by JSON
  value on 3 drivers. *(User's step 4.)*
- **Phase 4 — WHERE on individual values.** `JsonValueOf<T>(...).Eq/Gt/…`, `JsonExists`/`JsonIsNull`.
  **Pin the `bool` and `DateTime` extract+compare conventions here** with per-driver round-trip
  tests; ship if green, else defer with a KNOWN_ISSUES note (decision 8 keeps them in scope).
  Deep: extraction+cast SQL (AST), absent-path→NULL→excluded, nested-object path. Acceptance: all
  operators × primitive types, two JSON values in one query, composition with `Property()`, count
  compatibility. *(User's step 5.)*
- **Phase 5 — Select / Count.** Whole-field select (already works via accessor) + count with JSON
  WHERE. Deep: document decodes on read; count value. Acceptance: select-with-filter + count on 3
  drivers. *(User's step 6.)*
- **Phase 6 — projection of individual values (SelectBase).** Project an extracted JSON value into
  the resultset (`AddExpressionToResultset` + decode type); ORDER BY on it. Deep: projection SQL
  (AST) + decoded value + order correctness. Acceptance: per-driver projected values. *(Step 7.)*
- **Phase 7 — GROUP BY / aggregate on individual values.** Group by an extracted value; aggregate
  (`COUNT/SUM/AVG/MIN/MAX`) over one; HAVING on the aggregate. Deep: group-by/having SQL (AST,
  byte-identical expression reuse). Acceptance: grouped counts/sums on 3 drivers. *(Step 8.)*
- **Phase 8 — docs.** docgen pages + XML doc comments on all new public API (XML-doc generation on).

## Constraints / conventions (same as EAV)

- Both `Gehtsoft.EF.Entities.csproj` and `Gehtsoft.EF.Db.SqlDb.csproj` use
  `EnableDefaultCompileItems=false` — **every new .cs needs an explicit `<Compile Include>`**;
  the `System.Text.Json` PackageReference is the one deliberate packaging change (user approves it).
- Tests assert INTENDED behaviour; product bugs → a `KNOWN_ISSUES.md` in this folder, tests never
  adapted. Test SQL via AST (`.ParseSql()`), never string `Contains`; prefer behavioural checks.
- `ArgumentNullException.ThrowIfNull(x, nameof(x))`; never `replace_all` for constant extraction.
- No LINQ in product or test code (explicit loops, eager/O(1)).

## Open risks to close during phase planning

- **R1 (F3):** `bool`/`DateTime` cross-driver extract+compare — validate before promising (Phase 4).
- **R2:** Oracle path-literal escaping through `EXECUTE IMMEDIATE '...'` double-quoting (Phase 1/4).
- **R3:** Postgres `text→jsonb` inline cast cost + expression-index match (index expression must
  equal the query expression byte-for-byte) — confirm the index is actually used (Phase 2/4).
- **R4:** `System.Text.Json` on `netstandard2.0` — package version + trimming/AOT implications.
- **R5:** primitive-array cross-driver querying — keep arrays whole-field only in v1 unless a
  clean 3-driver story emerges.
