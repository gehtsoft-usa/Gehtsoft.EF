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

### Tasks 3–7 + load — *not planned yet* (planned each when reached).

## Conventions
- Explicit `<Compile Include>` for the new file; test csproj auto-includes.
- XML doc comments; classic `throw new ArgumentNullException(nameof(x))` (netstandard2.0, no `ThrowIfNull`).
- Tests assert intended behaviour; product bugs → `KNOWN_BUGS.md`.

## Gate
Bag implemented first; save/load each get their own decision + small plan before coding.
