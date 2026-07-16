# Schema Catalogue — build state

*Snapshot 2026-07-16. Branch `geo`, **uncommitted** (commit only when asked); `version.proj` untouched.
Authoritative state file for the catalogue initiative. Design: `DESIGN.md` (Gate-1 decisions).
Combined phase plan: `CONTROLLER_STACK_PLAN.md` (Phases 1–3, Q1–Q5 resolved). Serializer prereq:
`PREREQ_SERIALIZATION/PLAN.md`. Live geo-return-point + cross-links: `../GEO/PREREQUISITES_STATE.md`.
Process: two human gates per phase (start plan / advance phase) — see [[feedback_phase_process]].*

## Where we are

```
Prereq A serializer   ✅ built + green (8 tests, pure)
Prereq B instance lock ✅ built + green (24 tests, 5 drivers; native locks = Phase 4)
Phase 1  store         ✅ built + green on ALL 5 LIVE DRIVERS (17 tests × 5 = 85 green, 2026-07-16;
                          incl. version-semantics addendum: Dropped/AdvanceVersion/WriteTombstone)
Phase 2  diff engine   ✅ built + green, 100% line coverage (38 tests, pure)
Phase 3  controller    🔨 increments 1–4 DONE (guard + lock + column add/drop, index reconcile,
                          OnEntity* hooks, ALTER=refuse, Recreate/CreateNew + FK guard, obsolete→
                          tombstone, prop-drop hook, views, dynamic-properties reconcile;
                          22 tests × 5 = 110 green, 2026-07-16)
Phase 4  native locks   — later
Phase 5  compare-w/-actual + torn-write recovery — later
then GEO Phase 3 rides the catalogue
```

Full Catalogue + InstanceLock test sweep: **268 green** (8 serialization + 24 lock + 85 store + 41 diff
+ 110 controller).
Full product suite was 3246 green before this work began.

## Prerequisites (both DONE)

- **A — schema-tolerant (de)serialization.** `Gehtsoft.EF.Db.SqlDb/Catalog/`: `CatalogTableDto` (+ nested
  column/geometry/spatial-index/json/json-index/composite-index/field DTOs + `CatalogColumnDefault`),
  `CatalogSnapshot` (envelope + `SchemaFormatVersion` + `IsNewerThanSupported`), `CatalogSerializer`
  (`Serialize`/`Deserialize` + upgrade-on-read scaffold, `FromDescriptor`, `CurrentVersion`=1). JSON via
  System.Text.Json, enums by name, deterministic order, defaults as {typeName, invariant-string} (RS1).
  Newer-version blob parsed but flagged (refuse gate is the consumer's). Tests
  `Gehtsoft.EF.Test/Catalog/Serialization` (8, pure).
- **B — DB-instance lock.** `Gehtsoft.EF.Db.SqlDb/InstanceLock/`: `IDbInstanceLock` +
  `Acquire/TryAcquireInstanceLock` on `SqlDbConnection` + portable `ef_catalog_lock` lease fallback +
  `LockTimeout` + `CurrentServerTimeEpochSeconds` (5 dialects). Non-reentrant; lease per-acquire
  (default 30 s). Native per-driver advisory locks = Phase 4. Tests `Gehtsoft.EF.Test/InstanceLock`
  (24, 5 drivers). Full details: `../DB_INSTANCE_LOCK/STATE.md`.

## Phase 1 — Store (`Gehtsoft.EF.Db.SqlDb/Catalog/Store/`)

**Storage shape = APPEND-ONLY HISTORY + `migrated` flag** (REVISED 2026-07-16 by user — supersedes the
2026-07-15 "one upserted row, no flag/history" sketch in `DESIGN.md`/`CONTROLLER_STACK_PLAN.md`).

*Why (user):* history + flag are **separable** from torn-write *recovery behaviour*. Only the
resume-in-progress behaviour (+ 5-driver crash testing) is deferred (Phase 5). The storage **shape**
ships now because `ef_catalog` is hand-bootstrapped **outside** the catalogue, so reshaping it later is
exactly the fragile manual migration the catalogue exists to remove — and history has standalone
audit / diff-any-two-versions / repair-seed value. Growth is bounded: a row is appended **only when the
state actually changes** (a no-op re-run appends nothing).

- **`EfCatalogRecord`** — `[Entity(Table="ef_catalog", Scope="ef_catalog")]`, **one row per applied
  state**. Columns: `[AutoId] ID` (PK **and** monotonic latest-row key), `Scope` (128, non-null,
  `null`→`""`), `TableName` (128, non-null), `Version` (64, nullable), `AppliedUtc` (per-row),
  `Migrated` (bool — **always `true` in v1**; Phase-5 two-phase `false→apply→true` reuses it, no table
  change), `SchemaFormatVersion` (int), `Snapshot` (Size=0 → unbounded `text`/`clob`/`TEXT`). Managed
  via ordinary entity machinery (like `EfPatchHistoryRecord`) — never through the catalogue, no recursion.
- **`CatalogStore`** (no raw SQL — all query builders):
  - `EnsureBootstrapped` — create `ef_catalog` if absent (idempotent, race-tolerant, `GetCreateEntityQuery`).
  - `ReadApplied(conn, scope, table)` — latest `Migrated=true` row's snapshot + refuse gate.
  - `ReadAppliedForScope(conn, scope)` → `tableName→dto` map — **the controller's ONE-query batch read**
    (added per user efficiency note 2026-07-16; avoids a SELECT-per-table in `UpdateTables`).
  - `WriteApplied(...)` — **append** on real change; identical version+snapshot re-apply is a no-op
    (a tombstone tip never counts as no-op → recreate always appends); belt-and-braces refuse gate.
  - `AdvanceVersion(...)` — bump an unchanged table's **live row `Version` in place** (no new row,
    snapshot untouched); no-op when absent / tombstoned / already at that version. (version-semantics
    addendum, 2026-07-16.)
  - `WriteTombstone(...)` — append a `Dropped=true` row (keeps last-known snapshot for audit); no-op when
    absent or already tombstoned. `ReadApplied`/`ReadAppliedForScope` treat a `Dropped` tip as **absent**
    (refuse gate still applies first). (version-semantics addendum, 2026-07-16.)
  - **NO `AdoptFromModel`** — dropped 2026-07-16 (user). A trust-the-model "record current model as
    already-applied" seed only fits the ~0% case where switching to catalogues is the *sole* release
    change and the DB already matches; in the real case (catalogue switch shipped *with* model changes)
    it would stamp the new model as applied and **silently skip that release's DDL**. First contact is
    just `ReadApplied → null` → the controller diffs against `null` (full CreateTable). v1 is greenfield;
    migrating an existing mismatched DB needs actual-state introspection = Phase 5 (compare-with-actual).
  - Store does **not** take the instance lock — the controller (Phase 3) holds `IDbInstanceLock` across
    the whole read→diff→apply; the store runs inside it.
- **Refuse gate:** new `EfExceptionCode.CatalogFormatTooNew` (in `EfSqlException.cs`).
- **Tests:** `Gehtsoft.EF.Test/Catalog/Store/CatalogStoreTest.cs` — now a `[Theory]` over the
  all-driver fixture (`IClassFixture<SqlConnectionFixtureBase>` + `MemberData(ConnectionNames)`):
  **17 × 5 = 85 green on all live drivers** (SQLite, MSSQL, Oracle, PostgreSQL, MySQL) 2026-07-16.
  Each test drops `ef_catalog` first for a clean start (shared live table reused across runs; xUnit
  runs a class's theories serially so the reset is race-free, and no other class touches `ef_catalog`).
  Cases: bootstrap idempotent; read-null-when-absent; round-trip; append-on-change keeps history +
  reads latest; no-op appends nothing; `AppliedUtc` stable-on-no-op / new-row-on-bump; `ReadApplied` &
  `ReadAppliedForScope` ignore `Migrated=false`; batch latest-per-table + scope isolation + empty;
  refuse newer-format; **advance-version-in-place (no new row/snapshot change) + no-op guards; uniform
  live version after a mixed changed/unchanged run; tombstone reads-as-absent but keeps history + no-op
  when re-dropped; recreate-after-tombstone appends a new live row.** (The adopt-baseline test was
  removed with `AdoptFromModel`.)

**Notes:** RS3 — `Snapshot` `Size=0` → `text` on MySQL (64 KB cap); fine for one table's snapshot in v1,
revisit to `longtext` only if one approaches that. RS5 — `ReadAppliedForScope` reads the scope's
confirmed rows and reduces to latest-per-table in memory (one round-trip, matches the "model is small,
load in memory" design); switch to a group-wise `MAX(id)` two-step only if history volume ever bites.

## Phase 2 — Diff engine (`Gehtsoft.EF.Db.SqlDb/Catalog/Diff/`, pure, no DB)

- **`CatalogChange`** (+ `CatalogChangeKind`) — immutable, factory-constructed. Explicit kinds:
  `CreateTable`/`DropTable`, `AddColumn`/`DropColumn`/`AlterColumn`, `AddGeometryColumn`/
  `DropGeometryColumn`, `AddIndex`/`DropIndex` (single-column from `Sorted`/`Unique`), `AddCompositeIndex`/
  `DropCompositeIndex`, `AddSpatialIndex`/`DropSpatialIndex`, `AddJsonIndex`/`DropJsonIndex`. (Refines the
  plan's grouped "AddIndex/DropIndex (plain+composite)" into explicit kinds.)
- **`CatalogDiff.Compare(desired, stored)`** → ordered `IReadOnlyList<CatalogChange>`. **RS4 rules pinned:**
  - Identity by SQL name (column, index); a rename reads as drop+add (no rename detection in v1).
  - `AlterColumn` = definition change only (`DbType`/`Size`/`Precision`/`Nullable`/`PrimaryKey`/
    `Autoincrement`/`ForeignTable`/`Default`); carries previous+desired; **Phase 3 decides ALTER-vs-replace**.
  - Geometry-metadata change (SRID/subtype/Z/M/nullability) → **Drop+Add geometry column** (not portably
    alterable in place). Spatial-index list on an unchanged geometry column diffs separately.
  - JSON `ClrType` is opaque (RS2) → **not a diff signal**; only `Json.Indexes` diff.
  - New columns carry their own indexes (separate index changes only for **pre-existing** columns);
    `CreateTable` carries the whole table.
  - Column-family change (plain↔geometry↔json) → replace (Drop+Add).
  - **Ordering:** drop indexes → drop columns → alter columns → add columns → add indexes (dependents
    before their columns; columns before their indexes).
  - `stored==null` → CreateTable; `desired==null` → DropTable; both null → no changes.
- **Tests:** `Gehtsoft.EF.Test/Catalog/Diff/CatalogDiffTest.cs` — 38 green, **100% line coverage** of
  both `CatalogDiff` and `CatalogChange` (verified via dotnet-coverage). Coverage-driven pass added the
  "same name, changed shape" equality cases (spatial bbox/tolerance, JSON path/type/unique, composite
  field & `ExcludeFor` add/remove/count/change, default add/remove/type) that the first 20 tests missed
  — the exact parameter-change detection the catalogue exists for. Three provably-dead defensive guards
  (JSON/geometry non-null, composite name-guard) were removed rather than left uncovered.

## Phase 3 — `CatalogEntityController` (`Gehtsoft.EF.Db.SqlDb/EntityQueries/Catalog/`)

**Increment 1 DONE (2026-07-16).** `CatalogEntityController` mirrors `CreateEntityController`'s surface
(3 ctors + scope, `OnAction`, `CreateTables(conn, version)`/`DropTables(conn)`/`UpdateTables(conn,
version, UpdateMode, individual)` + async) and reuses `CreateEntityController.UpdateMode`. Under an
`IDbInstanceLock` (name `ef_catalog_update:<scope>`, `LockTimeout` default 30 s) it drives
`EnsureBootstrapped` → `ReadAppliedForScope` + `ReadCurrentVersion` → per-table `CatalogDiff.Compare`
→ **scope-level version guard** → apply DDL via an **injectable `ICatalogControllerAction`** (so
decisions are unit-tested driver-agnostically, like the old `EntityCreateControllerUnit`) → record
(`WriteApplied` changed / `AdvanceVersion` unchanged). Version parsed via the `EfPatch`
`major.minor.patch` scheme (`VersionKey = major*10^7 + minor*10^4 + patch`).
- **Guard (before any DDL):** `Vi<Vc`→`CatalogVersionRegressed`; `Vi==Vc` & any change→
  `CatalogModelChangedWithoutVersionBump`; `Vi==Vc` & none→clean no-op; `Vi>Vc`/first-contact→apply.
  Two new `EfExceptionCode`s added (+messages).
**Increment-2 DONE (2026-07-16)** — the `CatalogChange→DDL` applier (under `Update`), via the injectable
action (now `Create`/`Drop`/`AddColumns`/`DropColumns`/`CreateIndex`/`DropIndex`):
- **Columns:** `AddColumn` (resolve `ColumnInfo` from the model), `DropColumn` (reconstruct name/sorted/FK
  from the catalogued DTO). **`AlterColumn` = REFUSE** (`CatalogColumnAlterNotSupported`, +message) →
  route to a patch. Decided because there is no portable in-place column modify — `AlterTableQueryBuilder`
  is deliberately add/drop-only since column-change semantics diverge sharply per driver (user confirmed).
- **Indexes** for pre-existing columns (diff order drop-idx→drop-col→add-col→add-idx): single-column
  **Sorted** add/drop (built as `CompositeIndex(col)`), composite add/drop (resolve real `CompositeIndex`
  from `ICompositeIndexMetadata` by name / drop by name), JSON add/drop (resolve via
  `CompositeIndex.ForJson` from the model / drop by name). **Unique single-column** index changes refuse
  (`NotSupportedException`) — `CompositeIndex` carries no uniqueness; later increment.
- **`OnEntity*` hooks:** `OnEntityCreate` (create table), `OnEntityPropertyCreate` (add column),
  `OnEntityDrop` (`DropTables`). `OnEntityPropertyDrop` pairs with obsolete handling → increment 3.
- **Geometry** (`Add/DropGeometryColumn`, spatial index) still `NotSupported` — the post-parity geo
  increment.
- **Store additions consumed:** `ReadCurrentVersion` (max-id live row's version).
- **Tests:** `Gehtsoft.EF.Test/Catalog/Controller/CatalogEntityControllerTest.cs` — 13 × 5 = **65 green**
  (increment 1's 7 + drop-column, alter-refuse, create-hook, single-col index create/drop, unique-refuse).
  Mock action + real store on every driver. **Gap:** composite/JSON index *application* is implemented
  and compiles against the real types but has no dedicated behavioural test yet (symmetric to single-col).

**Increment-3 DONE (2026-07-16)** — table lifecycle + modes:
- **Modes:** `Recreate` = drop (if catalogued, reverse dep order) + recreate + record; `CreateNew` ≡
  `Update` (parity — `CreateEntityController` only ever special-cases `Recreate`). Removed the
  increment-1 mode-not-supported throw.
- **FK recreate guard:** an obsolete/`Recreate` table that a surviving, non-`Recreate` table still holds
  an active FK to → `CannotRecreateTable` (mirrors `HasActiveForeignKey`), before any DDL.
- **Obsolete entities:** `[ObsoleteEntity]` tables (found via `includeObsolete:true`) drop + `WriteTombstone`
  + `OnEntityDrop`. (A fully-deleted entity's row lingers — same as the old controller; only marked-obsolete
  is dropped.)
- **`OnEntityPropertyDrop`:** on a `DropColumn`, the matching `[ObsoleteEntityProperty]` property is found
  (`FindObsoleteProperty`, naming-policy aware) and its hook fired.
- **Views:** always drop + recreate (obsolete view → drop only); not diffed / not catalogued / excluded
  from the version-guard `anyChange` (a view-def change needs a version bump to re-apply).
- **Tests:** 19 × 5 = **95 green** (added Recreate drop+recreate, Recreate first-contact create-only,
  CreateNew≡Update, FK-guard throw, obsolete-entity drop+tombstone, obsolete-property drop+hook, view
  drop+recreate).

**Increment-4 DONE (2026-07-16)** — dynamic-properties reconcile:
- `CatalogTableDto` gained **`HasDynamicProperties`** (additive; format version kept at 1 — pre-release,
  so still in flux; `false` default is correct for non-dyn tables). `DesiredDto` sets it from the
  `EntityDescriptor`. `CatalogDiff` emits **`AddDynamicPropertiesTable`** (drop-first/add-last ordering,
  side table FKs the owner PK) / **`DropDynamicPropertiesTable`** when the flag flips on an *existing*
  table. First-contact create / recreate / drop already carry the side table via
  `GetCreateEntityQuery`/`GetDropEntityQuery`, so the diff signal only fires incrementally.
- Controller applies via new action methods `CreateDynamicPropertiesTable` (from
  `EntityDescriptor.DynamicPropertiesTable`) / `DropDynamicPropertiesTable` (rebuilds the fixed side-table
  descriptor via `DynamicPropertiesTableBuilder.Build(ownerTable, null)` — the entity no longer has dyn
  props). No introspection safety-check needed (the catalogue is authoritative on whether the side table
  exists).
- **Tests:** controller 22×5 = **110 green** (+gain creates side table, lose drops it, unchanged touches
  nothing); diff 41 (+gain/lose/same). 

**Remaining increments** (see `CONTROLLER_STACK_PLAN.md`): (5) `IEfPatch` replay `(Vc,Vi]`; (6) parity
gate; then post-parity geo (geometry + spatial index) and the deferred unique-single-column index +
composite/JSON index behavioural tests. **Old `CreateEntityController` is NOT obsoleted until Phase 5.**

## Cross-cutting decisions in force

- **No raw SQL anywhere** — everything through the query builders ([[feedback_use_query_builders]]).
- **Forward-compat = HARD REFUSAL** — `CatalogFormatTooNew` when a stored snapshot's format is newer
  than this build supports; zero DDL.
- **Q1 surface addition** — `Create/UpdateTables` take a **DB version string** (shared ordering key for
  structural schema and `IEfPatch`); stored with the datetime first applied (now per-row `AppliedUtc`).
- **Version semantics — DECIDED 2026-07-16.** Stored `Version` = *"last version at which the table has
  this descriptor"* → uniform across live tables = the DB current version `Vc`; `version ↔ whole-schema`
  is a bijection. Written cheaply: changed table INSERTs a new row, **unchanged table's live row has its
  `Version` UPDATED in place** (no new row), dropped table → `Dropped=true` **tombstone** (read as
  absent). Scope-level guard before any DDL: `Vi<Vc`→throw regression; `Vi==Vc` & any diff→throw
  `CatalogModelChangedWithoutVersionBump`; `Vi==Vc` & no diff→no-op; `Vi>Vc`→apply. **Every schema
  change requires a version bump** (no accumulating two changes under one version). Version parsed/ordered
  via the existing `EfPatch` `major.minor.patch` scheme. Patches replay `(Vc,Vi]` after structure
  converges (own ledger); patches may only touch structure still present at `Vi`. Full detail:
  `CONTROLLER_STACK_PLAN.md` → Phase 3.
- **Authority = catalogue-from-the-start (greenfield), v1** (user 2026-07-16). First contact →
  `ReadApplied` null → diff against `null` → full CreateTable. **No trust-model adopt** (it would skip a
  release's real DDL when the catalogue switch ships alongside model changes). Adopting/reconciling an
  existing mismatched DB needs actual-state introspection and is **Phase 5 (compare-with-actual)**.
- Constraints: netstandard2.0 explicit `<Compile Include>`; **no LINQ**; classic
  `throw new ArgumentNullException(nameof(x))`; **never** touch `version.proj`; tests assert intended
  behaviour on AST/behaviour, not string-match.

## Files added (all uncommitted, branch `geo`)

Product (`Gehtsoft.EF.Db.SqlDb/`):
`Catalog/Store/EfCatalogRecord.cs` (+`Dropped`), `Catalog/Store/CatalogStore.cs` (+`AdvanceVersion`,
`WriteTombstone`, `ReadCurrentVersion`), `Catalog/Diff/CatalogChange.cs`, `Catalog/Diff/CatalogDiff.cs`,
`EntityQueries/Catalog/CatalogEntityController.cs`; edit `EfSqlException.cs` (+`CatalogFormatTooNew`,
`CatalogModelChangedWithoutVersionBump`, `CatalogVersionRegressed`, `CatalogColumnAlterNotSupported` +
messages); 5 `<Compile Include>` entries in `Gehtsoft.EF.Db.SqlDb.csproj`. (Prereq A/B files already
present from earlier.)

Tests (`Gehtsoft.EF.Test/`): `Catalog/Store/CatalogStoreTest.cs`, `Catalog/Diff/CatalogDiffTest.cs`,
`Catalog/Controller/CatalogEntityControllerTest.cs`.

## Immediate next action

1. ✅ **DONE (2026-07-16)** — Phase 1 store green on all 5 live drivers (85/85), incl. the
   version-semantics addendum (`Dropped`, `AdvanceVersion`, `WriteTombstone`; `ReadApplied*` skip tips).
2. ✅ **DONE (2026-07-16)** — Phase 3 **increments 1–2**: `CatalogEntityController` skeleton + guard +
   lock + column add/drop, index reconcile (single-col/composite/JSON), `OnEntity*` hooks,
   `AlterColumn`=refuse; 65 green on 5 drivers (see Phase 3 section).
2b. ✅ **DONE (2026-07-16)** — Phase 3 **increments 3–4**: Recreate/CreateNew + FK guard, obsolete→
   tombstone, prop-drop hook, views, dynamic-properties reconcile; 110 green on 5 drivers.
3. **Phase 3 increment 5** — `IEfPatch` replay: after structural convergence, run coded patches in
   `(Vc, Vi]` in `major.minor.patch` order via `EfPatchProcessor` (its own `EfPatchHistoryRecord` ledger,
   unchanged); author rule — a patch may only touch structure still present at `Vi`. Then (6) parity gate;
   then post-parity geo. **Old `CreateEntityController` stays until Phase 5.** Follow-ups: dedicated
   composite/JSON index-application tests + unique single-column index reconcile (both currently deferred).
