# Dynamic Entity Properties ("Property Set") — Implementation Plan (rev 2)

*Planned 2026-07-03. Options analysis + decision record: `DYNAMIC_PROPERTIES_ANALYSIS.md`
(see "Decisions — REVISION 2"). Implementation not started.*

## Context

Per-row dynamic properties on Gehtsoft.EF entities: a flat bag of named values of simple
types (string, integer/long, double, datetime, boolean), settable/gettable by name,
filterable/sortable in queries, on all 5 SQL drivers, at both the SQL-builder and
Entity-Query-Builder levels.

**Storage: supplemental EAV table**, one per entity table (`<table>_props`), typed value
columns — pure SQL92, **zero per-driver LanguageSpecifics changes**.

## Confirmed decisions

1. **Storage = EAV side table** (Option B in analysis).
2. **No catalogue / registration-free** (Option A). Store any name/type per row. `WHERE`
   picks the value column from the **operand's CLR type**; `ORDER BY`/`SELECT`/aggregation
   take an **explicit `EntityPropertyType`**. Mixed types across rows are allowed: a query
   targets one value column, so rows storing that name in a different column read `NULL`
   there and drop out of the predicate — no enforcement, free from the EAV layout.
3. **Save = change-tracking** (partial-load safe): bag records touched + removed names; saver
   upserts changed rows by `(owner, name)`, deletes tombstoned ones, touches nothing else.
4. **Loading = opt-in** per query (`IncludeDynamicProperties()`) + standalone
   `LoadDynamicProperties`.
5. **Indexes = three static composite indexes** `(name, v_str)`, `(name, v_int)`,
   `(name, v_real)` created with the table — every property is efficiently queryable/sortable.
6. **3 value columns**: `v_str` (String), `v_int` (Int64: Integer, Boolean 0/1, DateTime
   ticks UTC), `v_real` (Double). Exactly one non-null per row. Rationale unchanged from
   analysis (MSSQL fixed-width NULL waste; ticks exact for date `Eq`/sort).

## Table shape — `<owner_table>_props`

| ColumnInfo.ID | Column | DbType | Notes |
|---|---|---|---|
| `Id` | `id` | Int64 | PK, autoincrement |
| `Owner` | `owner` | copy of owner PK type | not null, `ForeignTable`=owner → real FK (+ auto FK index) |
| `Name` | `name` | String(NameSize=64) | not null |
| `PropType` | `prop_type` | Int32 | not null; drives decode |
| `StringValue` | `v_str` | String(StringValueSize=256) | nullable |
| `IntValue` | `v_int` | Int64 | nullable; Integer / Boolean(0/1) / DateTime(ticks) |
| `RealValue` | `v_real` | Double | nullable |

**Index set** (composite indexes via a private `ICompositeIndexMetadata` on the synthesized
`TableDescriptor`):
- `(owner, name)` — the correlated-subquery lookup key (`WHERE owner=? AND name=?`).
- `(name, v_str)`, `(name, v_int)`, `(name, v_real)` — cross-owner value search / semi-join
  form; make every property fast regardless of type.
- **Plus** a single-column FK index on `owner` that `TableDdlBuilder` auto-creates because
  `owner` has `ForeignTable` set. The `(owner, name)` composite supersedes it for lookups, but
  it is still emitted (the framework doesn't suppress it) — so the props table has **5**
  indexes total. The PHASE_1 deep test pins this exact set (names + columns) via
  `DoesObjectExist`; if the auto FK index turns out redundant/undesirable, decide there.

`(owner, name)` uniqueness is API-enforced (change-tracking upserts by that key); no DB unique
constraint (documented v1 limit). Encode/decode in ONE place: internal `PropertySetValueCodec`
(`Encode(EntityPropertyType,object)→(column,value)`, `Decode(...)`), used by saver, loader,
query bind, and resultset decode.

## Components (registration-free — trimmed from rev 1)

**`Gehtsoft.EF.Entities/PropertySet/`** (driver-neutral):
- `EntityPropertyType.cs` — enum `{String, Integer, Double, DateTime, Boolean}` (Integer = long).
- `EntityProperty.cs` — `{Name, PropertyType, Value, T GetValue<T>()}` (checked conversion).
- `EntityPropertySet.cs` — the bag: `Set`(null ⇒ remove; nulls never stored)/typed overloads,
  `Get<T>`/`TryGet`/`Contains`/`Remove`/`Clear`/`Count`/indexer/`IEnumerable<EntityProperty>`;
  **change tracking**: per-name dirty set + removed (tombstone) set + `AnyModified`; loader
  populates and clears tracking; saver consumes and clears it.
- `DynamicPropertiesAttribute.cs` — marks the owner; options `NameSize=64`,
  `StringValueSize=256`, `TableSuffix="_props"` (short — Oracle 30-char ids).
- `IEntityPropertySetOwner.cs` — `{ EntityPropertySet DynamicProperties { get; } }`.

*(No registry, no declaration attribute/type, no Changed event.)*

**`Gehtsoft.EF.Db.SqlDb/PropertySet/`**:
- `PropertySetValueCodec.cs`.
- `PropertySetTableProvider.cs` — `GetPropertyTable(EntityDescriptor)` synthesizes + caches
  the `TableDescriptor` via `EntityDescriptor.SetTag`; throws `DynamicPropertiesNotSupported`
  if no `[DynamicProperties]` / no PK. Entity-free raw overload too.
- `PropertySetConnectionExtension.cs` — `CreateDynamicPropertiesTable` /
  `DropDynamicPropertiesTable` (+Async, existence-checked). *(No schema-update/cleanup: indexes
  are static, nothing to sync.)*
- `PropertySetSaver.cs` (change-tracking), `PropertySetLoader.cs` (opt-in, chunked IN at 500),
  `PropertySetQueryHelper.cs` (raw recipe: value-subquery / exists-subquery builders),
  `PropertySetQueryExtension.cs` (fluent DSL).
- Errors in `EfSqlException.cs`: `DynamicPropertiesNotSupported` (+ `DynamicPropertyTypeMismatch`
  only if we later add optional strictness — not needed for v1).

## Query translation (registration-free)

- **WHERE**: `DynamicProperty(name)` returns a small wrapper; the operator's operand type
  picks the value column + DbType (int/bool/DateTime→`v_int`, string→`v_str`, double→`v_real`;
  bool→0/1, DateTime→ticks at bind). Correlated scalar subquery on the left (FTS pattern):
  `(SELECT tp.v_int FROM doc_props tp WHERE tp.owner=entity0.id AND tp.name=:p) > :v`.
  Owner PK alias via `condition.BaseQuery.ConditionQueryBuilder.Alias(pk.ID, out _)`; name/value
  params via `BaseQuery.NextParam` + `BaseQuery.Query.BindParam`. All operators/And/Or/groups
  and `Value()` typing work unchanged. `HasDynamicProperty`/`HasNoDynamicProperty` → `Exists`.
- **ORDER BY / resultset / aggregation / group by / having**: explicit `EntityPropertyType`
  selects the column; same subquery string fed to `AddOrderByExpr` / `AddExpressionToResultset`
  / group-by-expr / having-expr. Resultset values arrive **encoded** (ticks / 0-1) — decode via
  a public `PropertySetValueCodec`-backed helper or `EntityProperty.GetValue<T>`.
- Semantics: `.IsNull()` = absent; wrong-type query = `NULL` = excluded; sort of a name stored
  under another type = `NULL` ordering (driver NULLS FIRST/LAST — documented).

## Save / Load / CRUD integration

- **Save** (`PropertySetSaver`): for a dirty bag, `UPDATE`/`INSERT` each touched entry by
  `(owner,name)` (type change updates in place, nulling other value columns), `DELETE` each
  tombstoned name; clean bag ⇒ no-op. Auto-hook in virtual `ModifyEntityQuery.Execute/Async`
  after PK is populated, when type has `[DynamicProperties]` and entity is
  `IEntityPropertySetOwner`. `DeleteEntityQuery` deletes props rows before the owner row.
  Standalone `SaveDynamicProperties`/`DeleteDynamicProperties` (+Async). Ambient transaction
  covers all statements (framework never auto-opens one).
- **Load** (`PropertySetLoader`): `SELECT owner,name,prop_type,v_str,v_int,v_real WHERE owner
  IN (…chunk 500…)`; decode; map by owner PK; clear bag + reset tracking. `IncludeDynamicProperties()`
  on select batches after the read loop — needs new `internal void CloseReader()` on `SqlDbQuery`
  (open reader blocks a 2nd query on MSSQL w/o MARS). `ReadOne()`+flag not batched (documented).

## Testing model (P1 — two tiers, applied to EVERY slice)

- **Deep / debug tier — SQLite only.** White-box: asserts the actual `_props` table + indexes
  exist (`DoesObjectExist`), the exact **generated SQL** (subquery/order-by/aggregate text),
  and **row-level table contents** (which value column, encoded value) after save/load.
- **Acceptance tier — all drivers.** Black-box round-trips via
  `[Theory][MemberData(nameof(ConnectionNames))]` (`SqlConnectionSources.SqlConnectionNames`) +
  `IClassFixture<SqlConnectionFixtureBase>`; SQLite always on, others per local config.
  Template: `Gehtsoft.EF.Test/SqlDb/FtsTest.cs`. Assert observable behavior (results, counts,
  exact DateTime via ticks), not SQL text.

Tests live in `Gehtsoft.EF.Test/Entity/PropertySet/` (+ explicit csproj `<Compile Include>`).

## Delivery — capability slices (P2, finish-before-advance)

Each slice ships its deep + acceptance tests and must be green before the next starts.

**Process (see also the working agreement):** every slice is planned first in its own folder
`CLAUDE/DYNAMIC_PROPERTIES/PHASE_N/` — the phase plan states the **public interfaces + their
responsibility**, **where** they're implemented, and **how** they're tested (both tiers) —
and coding starts only after explicit approval. Two human gates require explicit go, regardless
of Claude Code mode: (1) starting a phase's plan, (2) advancing to the next phase.

- **Slice 0 — foundation** (pulled in by Slice 1, no standalone feature): core bag types +
  `[DynamicProperties]`, `PropertySetValueCodec`, `PropertySetTableProvider`. Deep tests: bag
  semantics, change-tracking flags, codec encode/decode incl. bool/DateTime UTC roundtrip.
- **Slice 1 — create / drop table.** `PropertySetConnectionExtension` +
  `CreateEntityController` integration (create props after owner; drop before owner). Deep:
  table + its full index set present/absent (the 5 indexes enumerated in "Table shape"), FK
  order, idempotent Create/UpdateTables. Acceptance: create+drop across drivers.
- **Slice 2 — save / retrieve.** `PropertySetSaver` (change-tracking) + auto-hook in
  `ModifyEntityQuery`/`DeleteEntityQuery`; explicit `LoadDynamicProperties`. Deep: row contents
  per type, change-tracking (touch one on a partially-loaded bag preserves the rest), tombstone
  delete, cascade delete, transaction rollback. Acceptance: 5-type roundtrip (DateTime exact),
  update/remove, cascade.
- **Slice 2.1 — automatic / lazy retrieval.** `IncludeDynamicProperties()` +
  `SqlDbQuery.CloseReader()`. Deep: single batch query issued, >500 chunking, empty bags.
  Acceptance: include-on-select roundtrip, async.
- **Slice 3 — WHERE.** `DynamicProperty(name)` (operand-typed) + `HasDynamicProperty`/`HasNo`.
  Deep: generated subquery SQL, wrong-type→NULL→excluded, IsNull semantics. Acceptance: all
  operators × types, two dynamic props in one query, composition with regular `Property()`,
  count-query compat.
- **Slice 4 — ORDER BY.** `AddOrderByDynamicProperty(name, EntityPropertyType, SortDir)`. Deep:
  order-by-expr SQL, ticks/0-1 order == semantic order, NULL ordering. Acceptance: ordered
  results across drivers.
- **Slice 5 — aggregation.** aggregate a dynamic property in the resultset
  (`COUNT/SUM/AVG/MIN/MAX` over the value subquery) via a helper. Deep: aggregate SQL + decoded
  result. Acceptance: per-driver aggregate values.
- **Slice 6 — GROUP BY.** group by a dynamic property (explicit type). Deep: group-by-expr SQL.
  Acceptance: grouped counts/sums.
- **Slice 7 — HAVING.** predicate on an aggregate of a dynamic property. Deep: having SQL.
  Acceptance: filtered groups.
- **Slice 8 — docs** (after features land): docgen pages + XML doc comments (XML docs enabled
  → required on new public API).

## Constraints / conventions

- Both `Gehtsoft.EF.Entities.csproj` and `Gehtsoft.EF.Db.SqlDb.csproj` (and test csproj) use
  `EnableDefaultCompileItems=false` — **every new .cs needs an explicit `<Compile Include>`**;
  don't touch anything else in the build (user manages packaging).
- Tests assert INTENDED behavior; product bugs → `KNOWN_BUGS.md`, never adapt tests.
- `ArgumentNullException.ThrowIfNull(x, nameof(x))`; never `replace_all` for constant extraction.

## Out of scope / v2

- Optional self-learning type catalogue (would add cross-row type-consistency + typeless
  ORDER BY) — storage layout and query API are forward-compatible with adding it later.
- JSON read-optimization (hybrid C); LINQ `ExpressionCompiler` support; MongoDb; DB-level
  `(owner,name)` unique constraint; LEFT-JOIN ORDER BY/resultset optimization.
