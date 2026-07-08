# PHASE 2 — Dynamic-property CRUD

*Planned 2026-07-04 (restarted). Builds on Phase 1 (side table managed automatically). This phase
adds the value CRUD. **Planned one task at a time.** Tests: `Gehtsoft.EF.Test.DynamicProperties.DataManagement`.*

## Key decision — the bag is a driver-neutral bag of **objects**

The in-memory bag holds `name → object`. It has **no** type enum, **no** `Property` class, and
**no** value encoding. Rationale: how a value is typed and stored is a **DB-level** concern — the
SQL EAV design needs a `prop_type` discriminator + `v_str`/`v_int`/`v_real` columns (Boolean→0/1,
DateTime→ticks), but **MongoDB would store values natively** and want none of that. So each
back-end owns its own mapping; the bag stays a portable container. The value's own CLR type carries
its type in memory.

**But the bag DOES fix and enforce the supported-value-type contract** (driver-neutral): the only
accepted CLR types are the **six primitives** `bool`, `int`, `long`, `double`, `string`,
`DateTime`. `Set` (and `Initialize`) reject anything else with `ArgumentException` — **no
widening** (`short`/`byte`/`float`/`uint`/… are not accepted; the caller must use `int`/`long`/
`double`). `decimal` is deliberately **out** for now (adding a supported type later is
non-breaking; it would need its own storage decision). This is the contract; the *mapping* of each
supported type to storage stays DB-level.

## Task 1 — the property bag  ← *detailed & ready to implement*

`Gehtsoft.EF.Entities/PropertySet/DynamicPropertyBag.cs` (netstandard2.0 / C# 7.3; new file +
explicit `<Compile Include>` in `Gehtsoft.EF.Entities.csproj`). One class, no helpers:

- **Write**: `void Set(string name, object value)` — `null` value ⇒ remove (nulls never stored;
  absence == null). A non-null value's exact runtime type must be one of the six supported
  primitives (`bool`/`int`/`long`/`double`/`string`/`DateTime`), else `ArgumentException` — no
  widening. `bool Remove(string name)`.
- **Read**: `object Get(string name)` (null if absent); `T Get<T>(string name)` (generic CLR
  conversion via `Convert.ChangeType`/invariant; `default(T)` if absent); `bool Contains(string name)`;
  `int Count`.
- **Enumerate**: `IEnumerable<(string Name, object Value)>` over current values. The public unit is
  a **neutral named `ValueTuple`**, not `KeyValuePair` — the bag exposes name/value pairs without
  leaking its internal dictionary representation.
- **Initial (untracked) state**: `DynamicPropertyBag()`; `DynamicPropertyBag(IEnumerable<(string Name, object Value)> initial)`;
  `void Initialize(IEnumerable<(string Name, object Value)>)` replaces contents and takes them as
  the tracking baseline without marking anything modified (used by a future loader).
- **Change tracking** vs the baseline snapshot: `Added` / `Changed` (each
  `IEnumerable<(string Name, object Value)>`), `Removed` (`IEnumerable<string>`), `bool AnyModified`;
  `void AcceptChanges()` promotes current to baseline (used by a future saver). Derived by comparing
  current values to the baseline, so add-then-remove nets to nothing, etc.

**Tests (`DataManagement`, pure `[Fact]`, no DB):** set/get round-trip (string/int/long/double/bool/
DateTime/arbitrary object); `Get<T>` conversions + widening + absent-key default; `Set(name,null)`
removes; `Contains`/`Count`/`Remove`/enumeration; tracking (fresh→unmodified; add→Added;
change-existing→Changed; same-value→not Changed; remove-baseline→Removed; add-then-remove→nothing;
Initialize untracked; AcceptChanges clears).

## Task 1b — entity exposure + load model  ← *decided; interface implemented*

`IDynamicPropertiesOwner { DynamicPropertyBag DynamicProperties { get; } }` (Entities). Decided:
- **Nullable, read-only contract (Option A, refined).** `null` = not loaded / not set. A `null` bag
  fails loudly on access rather than masquerading as an empty set (prefer a clear failure over a
  hidden missing-load). The contract exposes **only a getter** so client code can't assign a bag by
  mistake or clobber a loaded one; the entity backs it with a **private setter**
  (`{ get; private set; }`) that a **driver sets via reflection** when loading. (Reflection-set is
  wired in the save/load task.) New-entity population (attaching a bag before the first save) will
  also go through a framework path rather than a client assignment — detailed with save/load.
- **No lazy loading.** Loading is explicit and eager, always with a live caller connection: an
  opt-in on the select (default off) and/or a standalone `LoadDynamicProperties`. (Lazy rejected:
  capturing a connection → use-after-dispose; capturing a factory → wrong transaction + implicit
  connection use.)
- **Multi-entity load is batched:** one props query per result page (`WHERE owner IN (…)`, chunked
  ~500 for Oracle), distributed into each entity's bag by owner PK; a single `ReadOne` is +1.
- **Not loading is save-safe:** change-tracking + upsert-by-`(owner,name)` means saving a
  never-loaded (or partially set) bag touches only the names actually Set/Removed.

**New-entity path + new-bag guard** (implemented):
- `object.InitializeDynamicProperties()` extension (Entities) attaches a fresh empty bag to a new
  entity (reflectively sets the private setter) and returns it, so the app can populate props
  before inserting. Throws if the entity isn't an `IDynamicPropertiesOwner`.
- `DynamicPropertyBag.IsNew` flag marks a bag created for a not-yet-inserted entity. It is cleared
  when the bag becomes a persisted/loaded baseline (`AcceptChanges` / `Initialize`). The save layer
  (later) will **reject a new bag anywhere but an insert** (a new bag in an update/select means a
  skipped load).

## DB-level save/load — one operation at a time

Per-operation, each its own step, SQLite-only tests at this stage (verify persisted rows with
**raw table-level SQL**, not the load path — load isn't built yet):
**Task 2 Insert → Task 3 Delete → Task 4 MultiDelete → Task 5 Update → Task 6 MultiUpdate →
Task 7 InsertSelect →** then load / include-on-select. This is where the SQL type model lives.

### Shared piece — value mapper (built with Task 2, reused after)
`Gehtsoft.EF.Db.SqlDb/EntityQueries/EntityDiscovery/DynamicPropertiesValueMapper.cs` (internal;
co-located with `DynamicPropertiesTableBuilder`). The SQL EAV type model — the `prop_type`
discriminator + value-column encoding. **Codes are fixed** (load depends on them):
```
enum DynamicPropertyValueType { String=0, Integer=1, Long=2, Real=3, Boolean=4, DateTime=5 }
```
`Encode(object value) → (DynamicPropertyValueType type, string column, object encoded)`:
- `string`  → (String,  `v_str`,  value)
- `int`     → (Integer, `v_int`,  (long)value)
- `long`    → (Long,    `v_int`,  value)
- `double`  → (Real,    `v_real`, value)
- `bool`    → (Boolean, `v_int`,  value ? 1L : 0L)
- `DateTime`→ (DateTime,`v_int`,  value.ToUniversalTime().Ticks)
- anything else → throw (the bag's contract should prevent it).

Column names come from `DynamicPropertiesTableBuilder` consts. `Decode(...)` is added with the
load task. (Integer vs Long is kept distinct precisely so load restores `int` vs `long`.)

### Task 2 — Insert  ← *detailed & ready to implement*
Persist the bag's properties when an entity is inserted.

**Hook:** `InsertEntityQuery` overrides `Execute(object)` / `ExecuteAsync(object)` — call `base`
first (inserts the owner row and populates the autoincrement PK back into the entity), *then* save
the props. Access the `EntityDescriptor` via the internal `EntityQueryBuilder.Descriptor`.

**Saver:** `DynamicPropertiesSaver.SaveOnInsert(SqlDbConnection, EntityDescriptor, object entity)`
(+Async), internal, co-located with the mapper:
- if `!descriptor.HasDynamicProperties` → return.
- `bag = (entity as IDynamicPropertiesOwner)?.DynamicProperties`; if `null` → return (no props).
- owner PK value = `descriptor.PrimaryKey.PropertyAccessor.GetValue(entity)`.
- one `GetInsertQueryBuilder(descriptor.DynamicPropertiesTable)` prepared once; for **every current
  property** in the bag (a fresh insert writes them all): `Encode` the value, then per row bind
  `owner`, `name`, `prop_type`, bind the one value column to the encoded value and **`BindNull` the
  other two** value columns (String→`v_str`, Int64→`v_int`, Double→`v_real`), `ExecuteNoData`.
  (`id` is autoincrement → skipped by the builder.)
- `bag.AcceptChanges()` — resets tracking and clears `IsNew` (the entity is now persisted).
- No `IsNew` guard here — a new bag is valid in an insert (that's the one place it is).

**Transaction note:** owner row + props rows are atomic only inside a caller-opened transaction
(the framework never auto-opens one); documented, not enforced.

**Tests (SQLite only; verify with raw table SQL):** namespace `DynamicProperties.DataManagement`.
- Create owner+props tables (`GetCreateEntityQuery`). New entity, `InitializeDynamicProperties()`,
  `Set` one value of each supported type (string/int/long/double/bool/DateTime), `Execute`.
- `SELECT name, prop_type, v_str, v_int, v_real FROM <table>_props WHERE owner=<pk>` (raw) →
  one row per property; correct `prop_type` code; value in the right column and correctly encoded
  (bool→0/1, DateTime→UTC ticks, int/long→`v_int`, double→`v_real`, string→`v_str`); the other two
  value columns `NULL`; `owner` = entity PK.
- After insert: `bag.IsNew` is false and `AnyModified` is false.
- Entity with no props set (empty/null bag) → no props rows. Non-opted-in entity → unaffected.

### Task 3 — Delete  ✅ *implemented 2026-07-04 (SQLite; 5 tests)*
When an entity is deleted, delete its property rows too.

**Hook:** `DeleteEntityQuery` overrides `Execute(object)` / `ExecuteAsync(object)`: if
`descriptor.HasDynamicProperties`, delete the property rows **first** (child before parent — the
side table has an FK to the owner; on engines that enforce FKs deleting the owner first would fail,
and on SQLite it would orphan rows), **then** `base.Execute` (delete the owner row). Cheap
`HasDynamicProperties` guard at the call site (skip the call / async frame otherwise).

**Deleter:** `DynamicPropertiesSaver.DeleteOwned(SqlDbConnection, EntityDescriptor, object entity)`
(+Async): `DELETE FROM <table>_props WHERE owner = @owner`, binding the entity's PK
(`descriptor.PrimaryKey.PropertyAccessor.GetValue(entity)`) — built via
`GetDeleteQueryBuilder(descriptor.DynamicPropertiesTable)` + `Where.Property(owner).Is(Eq).Parameter`.
**No bag involved** — delete is by owner PK, so it works whether or not properties were ever loaded
(and there's no `IsNew` guard: deleting by PK is safe regardless of bag state; a never-inserted
entity just deletes 0 rows).
(Needs the owner column's `ID` — add `internal const OwnerColumnId = "Owner"` to
`DynamicPropertiesTableBuilder` and use it for the descriptor's owner-column and the lookup.)

**Tests (SQLite; raw table SQL):** namespace `DynamicProperties.DataManagement`.
- Insert an owner + props, delete it → no `_props` rows for that owner, owner row gone.
- Delete when the bag was never loaded (fresh entity object carrying just the PK) → props still gone.
- Delete an opted-in entity that has no property rows → no error.
- Non-opted-in entity delete → unaffected. Async path exercised.

### Task 4 — MultiDelete — *design decided (NG); ready to plan concretely*

**Guiding design intent (NG):** dynamic properties are for **storage + occasional filtering**, *not*
extensive/hot-path search. Main search fields stay hardcoded real columns. So the filter path is
deliberately kept simple — we do **not** optimize for many-property predicates.

`MultiDeleteEntityQuery` deletes every entity matching a condition. Two concerns:

1. **Cascade the property rows of the deleted objects.** Delete the side-table rows for *all* owners
   the delete will remove, **before** the owner rows (FK order): `DELETE FROM <t>_props WHERE owner
   IN (SELECT <pk> FROM <t> WHERE <same condition>)` — the same condition the main delete uses.

2. **Allow a dynamic property as a delete condition** (`DynamicPropertyOf<T>`).

**Lowering — decided: flat N × `IN`, no aggregation.** Each dynamic-property predicate becomes a
**self-contained** subquery and composes through the *existing* outer And/Or/Not boolean tree:
- `DynamicPropertyOf<T>(name).{op}(value)` → `id IN (SELECT owner FROM <t>_props WHERE name=@n AND
  <valcol> {op} @v)`; the value's CLR type picks `<valcol>` (`v_str`/`v_int`/`v_real`) via the mapper.
- Negation → `id NOT IN (…)` — correctly includes owners with **zero** prop rows.
- Rejected `GROUP BY owner HAVING MAX(CASE…)`: for the small N we target it's a wash on data touched
  (both do ~N index seeks on the `name_*` indexes) and it needs new HAVING/CASE builder support and
  mishandles absence. Kept in back pocket only for large-N or "K-of-N" threshold semantics (not our case).
- Consequence: **no mandatory nested builder** — the flat `DynamicPropertyOf<T>` surface suffices;
  an OR-group→single-subquery merge is optional future sugar. **No lower-layer (`QueryBuilder`)
  change at all** — just subquery + `IN`/`NOT IN`, which already exist.

**Layering:** `DynamicPropertyOf<T>` is **entity-level only** (it knows the descriptor → side table +
owner PK + mapper) and hands `QueryBuilder` a plain `id IN (SELECT …)`; `QueryBuilder` never sees a
"dynamic property." v1 scope: positive predicates (`=,<,>,<=,>=`, range, `LIKE`) + `NOT`, combined by
the existing And/Or.

**Part 2 WHERE — ✅ implemented 2026-07-04 (SQLite; 8 filter tests).**
- `DynamicPropertyConditionBuilder` (new, `EntityQueries/EntityQuery/`) + two `DynamicPropertyOf<T>`
  extensions (on `EntityQueryConditionBuilder` for the first And-condition, and on
  `SingleEntityQueryConditionBuilder` for continuation after `And/Or/AndNot/OrNot`). Ops:
  `Eq/Neq/Gt/Ge/Ls/Le/Like`, each → `pk IN (SELECT owner FROM <t>_props WHERE name=@n AND
  <valcol> {op} @v)` built from a table-level `SelectQueryBuilder`; params bound on the outer query
  via `NextParam`; value's CLR type → value column + bind `DbType` via the mapper.
- Reused existing machinery entirely (`PropertyOf(pk).Is(In).Query(sub)`); the outer `Not` bit yields
  `NOT IN` → absence semantics. `Neq` (inner `<>`, requires the property set) vs `AndNot(...).Eq`
  (`NOT IN`, includes unset) verified as distinct.
- Support: promoted the side-table column IDs to consts on `DynamicPropertiesTableBuilder`
  (`NameColumnId`, value-column IDs, etc.). New file added to `Gehtsoft.EF.Db.SqlDb.csproj`
  (explicit `<Compile Include>`). **No `QueryBuilder`-layer change.**
- Tests `DynamicProperties.DataManagement.DynamicPropertiesFilterTest` (filtered select verifies the
  WHERE in isolation): Eq, AND, OR, Ge range, Neq, AndNot(+unset), composes-with-regular-property,
  all six value types.

**Still to do for Task 4:** MultiDelete part 1 (cascade props for matched owners, before the owner
rows) + wiring `MultiDeleteEntityQuery` + MultiDelete tests using `DynamicPropertyOf` as the condition.

#### Architectural problem surfaced by part 1 (cascade) — and why we use a workaround
Cascading needs the **same** predicate `P` in **two** statements: the direct root `DELETE … WHERE P`
(MySQL forbids `DELETE FROM t WHERE id IN (SELECT … FROM t …)`, so the root delete can't self-reference
→ must carry `P` directly) **and** the child `DELETE FROM <t>_props WHERE owner IN (SELECT root FROM t
WHERE P)`. But the two render `P` differently — modify statements qualify columns by **table name**
(`owner.col`; SQLite forbids an alias on a DELETE/UPDATE target), selects by **synthetic alias**
(`entity7.col`; needed for joins). A predicate that's been rendered to a *string* is welded to one
statement's aliasing and can't be reused in the other. This violates two ground assumptions of the
entity layer: **A1** WHERE is rendered eagerly at author-time, and **A2** one entity query = one SQL
statement (A1 depends on A2).

The correct fix — **detach the abstract predicate (EntityWhere) from query-specific SQL (SQLWhere),
make predicates copyable, and compile to SQL at the last moment** — is a refactor of the *universal*
condition builder. **Decision (2026-07-04): defer it, use a contained workaround**, because the blast
radius is large (every entity query) and the only consumer today is EAV MultiDelete/MultiUpdate
(dynamic properties are scoped as occasional filtering, not a hot feature). Full problem + proposed
refactor + backward-compat contract + staging are recorded in **`CLAUDE/ENTITY_WHERE_PROBLEM.md`**;
revisit when a second consumer appears.

**Workaround (for MultiDelete + MultiUpdate):** after `PrepareQuery`, reuse the modify statement's
already-rendered, table-name-qualified WHERE text verbatim inside `SELECT root FROM t WHERE <text>` for
the child `owner IN (…)`, and combine `{child delete ; root delete}` in one `MultiSqlQueryBuilder`
(params bound once on the shared query). Works precisely because modify-WHERE is table-name-qualified.

#### Status — end of day 2026-07-04
`MultiDeleteEntityQuery` implemented as **3 tiers** (detected by whether the rendered WHERE contains the
`_props` table name — the qualifier only lands there via a `DynamicPropertyOf` subquery, values are
parameterized so no literal can spoof it):
1. **no WHERE** → one command: delete all props; delete all owners.
2. **WHERE, regular columns** → one command: `DELETE props WHERE owner IN (SELECT id FROM owner WHERE
   cond); DELETE owner WHERE cond` (props-first, FK-safe, MySQL-safe). Reuses the rendered WHERE with a
   qualifier-prefix realign (builder aliases differ: delete `table.col`, select `entityN.col`). **Works.**
3. **WHERE filters on a dynamic property** (reads `_props`) → **materialize the matched owner ids to
   the client, then delete by that fixed id-set** (implemented 2026-07-05). In a (nested) transaction,
   batches ≤50: pre-read `SELECT owner.id WHERE <cond>`; delete props (subquery form where the engine
   allows a table in its own delete's sub-query, else by `IN [ids]`); delete owners by `IN [ids]`.
   NG chose client-side materialization over a temp table (dynamic properties are for storage +
   occasional filtering, not mass ops). New capability flag `SqlDbLanguageSpecifics
   .SelfReferenceInDeleteAllowed` (true; MySQL false) drives the props-delete branch. Works on all 6
   connections (`DynamicPropertiesMultiDeleteTest` 30/30, incl. Eq / composed-AND / async, validated
   by *which* owners survive, not just counts).

**Multi-driver INSERT defect — ✅ FIXED 2026-07-05** (was: dynamic-property INSERT broken on
MySQL/Oracle via the batched multi-statement command). Two root causes fixed: (1) the owner insert left
its auto-id readback reader open → added `SqlDbQuery.CloseReader()`, called in the binder after reading
the id (fixed PG/MSSQL/MySQL connection conflict); (2) each batched props insert emitted a per-row
autoincrement readback (`; SELECT LAST_INSERT_ID();` → `;;` on MySQL; `RETURNING id INTO :id` → ORA-50028
on Oracle) → added `InsertQueryBuilder.ReturnAutoincrement` (default true), set false for the batch; each
driver's `BuildQuery` guards its readback on it. See `../KNOWN_ISSUES.md #1`. Now green on all drivers:
`DynamicPropertiesMultiDeleteTest` 18/18, debug 6/6, entity insert 42/42, dynamic-properties suite 132/132.

**Tier-3 dynamic-property MultiDelete — ✅ DONE 2026-07-05** via client-side id materialization (see the
3-tier list above). The same "materialize ids → batched IN operations in a (nested) transaction" logic
will be reused for **MultiUpdate**. Nothing committed yet; NG to run full regression.

### Task 5 — Update  ✅ *implemented 2026-07-05 (all 6 drivers; 6 tests)*
`UpdateEntityQuery` applies the bag's **net changes** after the owner update (Option A, precise):
`Added` → INSERT, `Changed` → UPDATE, `Removed` → DELETE, combined in one command (owner a shared
param, each change row-suffixed; a running row index keeps params unique across the mixed statements).
Guards: null bag → skip; **new bag → throw** (`DynamicPropertiesBagIsNew` — a new bag belongs to an
insert / signals a never-loaded bag); `!AnyModified` → skip. No reader-close needed (the owner update
is `ExecuteNoData`, no read-back).

The per-property statement/row builders were **promoted to `DynamicPropertiesSaver`** (shared:
`AddInsert`/`AddUpdate`/`AddDelete`, `BindValueRow`/`BindNameRow`/`BindOwner`, `Suffixed`,
`RequireExistingBag`); `InsertEntityQuery` was refactored onto them. Tests
(`DynamicPropertiesUpdateTest`, 6×6): add / change / remove / **mixed** (verifies changed changes,
unchanged stays, added added, removed removed) / other-owner-untouched / async / new-bag-rejected.

### Task 6 — MultiUpdate  ✅ *implemented 2026-07-05 (all 6 drivers; 7 tests)*
`MultiUpdateEntityQuery` now updates owner **columns** and/or **dynamic properties** in bulk. Three cases:
- **owner columns only** (no prop changes) → the existing single `UPDATE owner SET … WHERE <cond>`
  (works with any condition incl. a dynamic-property filter — `owner` target, `_props` sub-query, no
  self-reference). Unchanged base path.
- **any `SetDynamicProperty`/`RemoveDynamicProperty`** → **materialize the matched owner ids** (uniform,
  avoids self-reference on MySQL and mutual dependency), then in a **nested transaction, batches ≤50**:
  owner-column `UPDATE` (one statement by the original condition, run *before* prop changes so a set
  can't disturb the filter) + per-name clear `DELETE props WHERE owner IN [ids] AND name=@n` + per-owner
  bulk `INSERT` for each set-property (option (b): owner varies per row — new `DynamicPropertiesSaver
  .AddBulkInsert`/`BindBulkInsert`).

New API: `SetDynamicProperty(name, value)` / `RemoveDynamicProperty(name)`. The owner update runs through
a query created **inside** the transaction (`GetQuery(mBuilder.QueryBuilder)` + `CopyParametersFrom`) so
it enlists — `mQuery` predates the transaction and would not be. The ids-select + alias-realign were
promoted to `DynamicPropertiesSaver` (`BuildMatchedIdsSelect`, `ConditionReferencesProps`) and MultiDelete
refactored onto them. Tests (`DynamicPropertiesMultiUpdateTest`, 7×6): owner-only / props-only set /
replace / remove / mixed (both matched rows) / **60-owner batch-boundary** / async. All 6 drivers green.

### Task 7 — InsertSelect  ✅ *2026-07-05 — rejected by design*
`INSERT … SELECT` (`GetInsertSelectEntityQuery`) for an entity that owns dynamic properties **throws
`NotSupportedException`** (guard in the factory, before the query is allocated): a select produces
column values only and cannot populate the side table. Non-dynamic entities are unaffected (guard
condition false; 43 existing InsertSelect tests green). Test:
`DynamicProperties.DataManagement.DynamicPropertiesInsertSelectTest`.

### Read-side filtering by dynamic property ✅
`SelectEntitiesQuery` / `SelectEntitiesCountQuery` filtered by `DynamicPropertyOf` need no special code
(a read never touches `_props`, so it's just `owner.id IN (SELECT owner FROM <t>_props WHERE …)`).
Tests: `DataSelecting.DynamicPropertiesSelectByPropertyTest` (Eq/And/Or/range/composed/no-match/count),
7×6 all drivers.

### Task 8 — the **load path** ✅ *2026-07-05 (all 6 drivers)*
Populate the bag on select. Two entry points:
- **`SelectEntitiesQuery.PreloadProperties`** (default off): `ReadAll` (and `GetAllAsEnumerable`) read all
  rows first, then **close the main select's reader** (`SqlDbQuery.CloseReader`) and **batch-load** the
  properties in one `WHERE owner IN (…)` select per chunk (`OwnerBatchSize = 500`), attaching a loaded
  (baseline-accepted, not-new) bag to each entity — empty bag for owners with no rows.
- **`SqlDbConnection.LoadPropertiesFor<T>(entity)`** (+`…Async`): on-demand load/reload for one
  already-read entity (no open reader → always safe). No-op for a non-dynamic type.

**Why not per-`ReadOne`:** loading during iteration needs a *second* command while the main reader is
still open — the exact conflict fixed for insert (PG "command in progress", MSSQL "open DataReader",
MySQL "connection in use"), and here the main reader can't be closed mid-stream. So a **direct**
`ReadOne`/`ReadOneAsync` with `PreloadProperties` set **throws `NotSupportedException`**; an internal
`mReadingAll` flag distinguishes `ReadAll`'s own `ReadOne` calls (allowed) from direct ones. Batching
also matches the Task-1b "one props query per page" decision (never N+1).

New shared helper `DynamicPropertiesLoader` (query-agnostic, sibling of the saver): reads `owner, name,
prop_type, v_str, v_int, v_real`, `Decode`s by the type code, groups rows by owner (string-keyed to
avoid int-vs-long boxing mismatches), and calls `entity.LoadDynamicProperties(...)` — a new
`DynamicPropertiesExtension` method that `Initialize`s a bag (IsNew=false) and reflection-sets it (shares
`AttachBag` with `InitializeDynamicProperties`). Test:
`DataSelecting.DynamicPropertiesLoadTest` (preload all types / empty bag / on-demand / direct-ReadOne
throws / update round-trip), 5×6 all drivers. **Phase 2 complete.**

## Conventions
- Explicit `<Compile Include>` for the new file; test csproj auto-includes.
- XML doc comments; classic `throw new ArgumentNullException(nameof(x))` (netstandard2.0, no `ThrowIfNull`).
- Tests assert intended behaviour; product bugs → `KNOWN_BUGS.md`.

## Gate
Bag implemented first; save/load each get their own decision + small plan before coding.
