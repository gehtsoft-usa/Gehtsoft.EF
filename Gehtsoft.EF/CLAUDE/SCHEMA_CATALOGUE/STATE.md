# Schema Catalogue — build state

*Snapshot 2026-07-19. Branch `geo`. Increments 1–4 committed (`a583bba`); **increments 5–6 + AdoptExistingScope
+ coverage-audit hardening + old-controller deprecation + all documentation (guide articles, docgen-compatible
XML docs) committed (`fba359a`)**; `version.proj` untouched. Phase 3 is now **feature-complete through the parity gate**; next up is
post-parity geo (geometry + spatial index) and the deferred follow-ups.
Authoritative state file for the catalogue initiative. Design: `DESIGN.md` (Gate-1 decisions).
Combined phase plan: `CONTROLLER_STACK_PLAN.md` (Phases 1–3, Q1–Q5 resolved). Serializer prereq:
`PREREQ_SERIALIZATION/PLAN.md`. Live geo-return-point + cross-links: `../GEO/PREREQUISITES_STATE.md`.
**Operational flows (all scenarios incl. old→new migration): `FLOWS.md`.** Increment-5 detail:
`INCREMENT_5_PATCH_REPLAY.md`.
Process: two human gates per phase (start plan / advance phase) — see [[feedback_phase_process]].*

## Where we are

```
Prereq A serializer   ✅ built + green (8 tests, pure)
Prereq B instance lock ✅ built + green (24 tests, 5 drivers; native locks = Phase 4)
Phase 1  store         ✅ built + green on ALL 5 LIVE DRIVERS (17 tests × 5 = 85 green, 2026-07-16;
                          incl. version-semantics addendum: Dropped/AdvanceVersion/WriteTombstone)
Phase 2  diff engine   ✅ built + green, 100% line coverage (38 tests, pure)
Phase 3  controller    ✅ increments 1–6 DONE (guard + lock + column add/drop, index reconcile,
                          OnEntity* hooks, ALTER=refuse, Recreate/CreateNew + FK guard, obsolete→
                          tombstone, prop-drop hook, views, dynamic-properties reconcile;
                          increment 5: EnsureCatalogInfrastructure + de-automation + IEfPatch replay;
                          increment 6: PARITY GATE — real-action physical-schema parity vs the old
                          controller, 9 tests × 5 = 45 green, 2026-07-19)
Phase 4  native locks   — later
Phase 5  compare-w/-actual + torn-write recovery — later; FIRST SLICE DONE: AdoptExistingScope
                          (TrustModel/ReconcileToModel) + IsOrphanScopeExists + old-controller
                          failIfUpdateNeeded verify flag (2026-07-18)
then GEO Phase 3 rides the catalogue
```

Full Catalogue + InstanceLock + Patch sweep: **391 green** (8 serialization + 24 lock + 85 store + 41 diff
+ 175 controller [135 increment-1..5 + 40 adoption/orphan] + 7 in-memory patch-replay/adopt E2E + **45
parity-gate [9 × 5]**; the 7 existing `SqlDb.Patch.PatchTest` also green — `EfPatchProcessor` untouched).
**Full product suite: 3625 green, 0 failed on all 5 live drivers (confirmed 2026-07-19, after the data-loss
safety pass; 3609 at `fba359a` → +6 JSON/dyn-props migration → +10 data-safety Fail-throws tests).**

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

**Increment-5 DONE (2026-07-18)** — explicit infrastructure + `IEfPatch` replay. Detailed plan +
as-built + rationale: `INCREMENT_5_PATCH_REPLAY.md`.
- **`EnsureCatalogInfrastructure(conn)` (+ async)** — the single explicit bootstrap: creates `ef_catalog`
  **and** the `ef_patch_history` ledger if absent (idempotent, race-tolerant). **De-automation:**
  `CreateTables`/`UpdateTables`/`DropTables` no longer self-bootstrap (removed the internal
  `EnsureBootstrapped`); a missing catalogue just lets the store SELECT fail — the heuristic/adopt path is
  Phase 5 (user: "just let select query fail. We will add heuristic later"; "too much automation here").
- **Replay window (catalogue-local), keyed on the SCOPE VERSION not the ledger:** new
  `ICatalogControllerAction.ReplayPatches(conn, assemblies, scope, firstContact, fromVersionKey,
  throughVersionKey)`. Runs every discovered patch with **`Vc < key ≤ Vi`** (`fromVersionKey =
  VersionKey(Vc)` = the scope's version *before* the run, 0 on first contact; `throughVersionKey =
  VersionKey(Vi)`), recording each into `ef_patch_history`. **A patch ≤ Vc is already baked into the
  structure (created/converged at Vc) → never runs.** This is what keeps `CreateTables` correct: it stamps
  `Vc=Vi` and runs no patches, so a later `UpdateTables` replays only `(Vi, …]` — never the already-baked-in
  `≤ Vi` patches *even though the ledger is empty* (the ledger is NOT the window driver — earlier as-built
  keyed on ledger-max, which wrongly re-ran pre-baked patches after a `CreateTables`; fixed 2026-07-18 per
  user). **REVISED 2026-07-19: first contact runs NO patches** (`ReplayPatches` called only when
  `currentVersion != null`) — a fresh DB is built directly at the target and has nothing to migrate, matching
  `EfPatchProcessor`'s stamp-and-runs-none on a fresh database; this also makes `CreateTables` and
  first-contact `UpdateTables` consistent (both patch-free) and retired the "CreateTables patch-seeding"
  question. Safe under the author-rule (a patch may only touch structure present at `Vi`). `EfPatchProcessor`
  is **not** delegated to
  (its present-but-empty-ledger branch runs-and-records nothing — an untested artifact of keying
  "greenfield" on table existence; confirmed via git blame + missing test); only `FindAllPatches` is reused.
  The ledger is used only for **orphan detection** and **recording**.
- **Orphan guard:** first contact (`Vc == null`) with a **non-empty** ledger for the scope →
  `EfSqlException(CatalogOrphanPatchHistory)` (new code + message). This is the "DB managed before the
  catalogue" case; the real import/decide is **Phase-5 `AdoptExistingScope`** (user chose *explicit* adoption
  of an old schema over silent import).
- **Fires:** end of `UpdateTables`, after structure converges, **on a real transition only**
  (`currentVersion != null` && `Vi > Vc`; **REVISED 2026-07-19 — first contact runs no patches**). Clean
  no-op (`Vi == Vc`, no diff) returns before replay (ledger already current; the only unrun-patch-at-`Vc`
  case is a mid-run crash = Phase-5 torn-write). `CreateTables`/`DropTables` are patch-free. **The
  "CreateTables patch-seeding" question is RESOLVED (2026-07-19): retired — it was an artifact of the
  increment-5 first-contact-runs-all bug, not a real Create-vs-Update distinction.**
- **Scope:** keyed to the controller's entity scope `mScope` (Edge-B: patches share the entity scope — the
  original intent; migrating a differently-scoped old ledger is a documented manual prerequisite).
- **Async:** `UpdateTablesAsync` stays `Task.Run(sync)`, so patches run via sync `Apply` even for
  `IEfPatchAsync` (which always has one). v1 limitation.
- **Tests:** controller 27×5 = **135 green** (+5 methods × 5: **REVISED 2026-07-19** first-contact
  `Update_FirstContact_DoesNotReplayPatches` (no replay); version-bump replays w/ window `(Vc,Vi]`;
  clean-no-op no replay; Create/Drop no replay). In-memory-SQLite `CatalogPatchReplayTest` (**6 green**):
  **REVISED 2026-07-19** `FirstContact_FreshDatabase_RunsNoPatches`; incremental open window;
  **CreateTables-then-update does NOT re-run pre-baked ≤Vc patches**; version-bump-without-new-patch runs
  nothing & no re-apply; first-contact-with-ledger → orphan throw; after-adopt window. Strict-mock
  unchanged/dynamic-unchanged tests (seeded → not first contact) allow+verify the one `ReplayPatches` call.
  **All operational flows documented in `FLOWS.md`** (incl. old→new migration).

**AdoptExistingScope DONE (2026-07-18)** — first slice of Phase 5, brought forward to close the migration
path. Plan + as-built: `ADOPT_EXISTING_SCOPE.md`; flows: `FLOWS.md` §6/§7.
- **`CreateEntityController.UpdateTables` gained `failIfUpdateNeeded` (optional, default false)** — verify
  mode: apply no structural DDL, throw **`SchemaUpdateRequired`** the moment a table/column/index/dyn-props
  change would be needed; views ignored (always drop+recreate, not drift). Implemented via a private
  **`VerifyingAction`** decorator swapped in for the injectable action (catches table/column create/drop,
  no-ops views) **plus flag-guards in `ReconcileIndexes`/`ReconcileDynamicPropertiesTable`** (complete
  verify, option (i)). Additive — existing callers unaffected.
- **`CatalogEntityController`:** `enum CatalogAdoptMode { TrustModel, ReconcileToModel }`;
  **`IsOrphanScopeExists(conn)`** (no catalogue for scope + a non-view table physically exists — greenfield
  is not orphan); **`AdoptExistingScope(conn, version, mode)` (+async)** — under the lock, refuses
  **`CatalogScopeAlreadyAdopted`** if already catalogued; runs the old controller with
  `failIfUpdateNeeded: mode==TrustModel` (TrustModel verifies, ReconcileToModel aligns), then records
  `FromDescriptor(model)` at `version` for each non-view table. **No patches** — sets the `Vc=version`
  baseline so later `UpdateTables` replays only `(version, Vi]`. Why old controller (both modes): reuses
  proven introspection reconcile; always sufficient because no adoptable (pre-catalogue) DB contains geo
  (geo is new-controller-only, post-parity) — the only thing the old controller can't reconcile (user's
  point).
- **Tests:** adoption in `CatalogEntityControllerTest` (same class → serialized, avoids `ef_catalog`
  cross-class race): `IsOrphanScopeExists` detects/clears; TrustModel matched seeds / drifted throws
  `SchemaUpdateRequired`; ReconcileToModel aligns+seeds; already-catalogued throws — **5×5 = 25 green**.
  Post-adopt patch window in `CatalogPatchReplayTest` (in-memory): adopt at 2.0.0 → `UpdateTables(3.0.0)`
  runs only `3.0.0` — **+1 (6 total in that class)**.

**Coverage-audit hardening DONE (2026-07-18)** — a fable-model subagent cross-checked `FLOWS.md` vs the
suite; two real items fixed + gaps closed:
- **Orphan guard moved to a PRE-CHECK (correctness fix).** It was inside `ReplayPatches` (end of
  `UpdateTables`), so on first contact it seeded the catalogue at `Vi` *then* threw → a re-run became a
  `Vi==Vc` no-op that masked the orphan and wedged `AdoptExistingScope`. Now a pre-check at the **top of
  `UpdateTables`** (after reading `Vc`, before any DDL): first contact + a discovered non-view table
  physically exists → new **`CatalogOrphanScope`**; else ledger has scope rows → `CatalogOrphanPatchHistory`.
  Zero DDL, catalogue unseeded. `ReplayPatches` lost its `firstContact` param + orphan logic (moved to
  controller `ReadLedgerTip`); its window logic is unchanged. `FLOWS.md` §6/§7 corrected.
- **`failIfUpdateNeeded` verify — the option-(i) guards now tested.** Added `Adopt_TrustModel_IndexDrift_Throws`
  (drop the sorted-column index → `ReconcileIndexes` guard) and `Adopt_TrustModel_DynamicPropertiesDrift_Throws`
  (drop the EAV side table → `ReconcileDynamicPropertiesTable` guard) — the paths beyond the `VerifyingAction`
  decorator that were untested. Plus `Update_OnPreCatalogueDatabase_RefusesAsOrphan_BeforeAnyDdl` and the
  Flow-6 `AdoptWithPreExistingLedger_ThenUpdate_RunsOnlyTheWindow` (real shape: tables + ledger rows, adopt,
  update runs only the window). Strengthened the orphan test to assert the catalogue stays unseeded.
- **M2 investigated, no code change:** "extra DB columns" (in DB, absent from model) are benign — additive
  reconcile never drops them; they linger invisibly, no data loss. **No** driver returns null from
  `GetTableIndexes` (all return `AssembleIndexes`, never null) — the `== null` short-circuit is defensive
  only, so index drift IS caught on all five. Documented as a `FLOWS.md` §7 caveat.

**Increment-6 DONE (2026-07-19) — the parity gate.** Detail: `INCREMENT_6_PARITY_GATE.md`.
- **`CatalogParityTest.cs`** (live all-driver) runs each supported scenario through **both** controllers
  with the **real DDL action** (not the mock the other controller tests use) on one connection, and asserts
  a **physically identical schema** via a model-derived fingerprint (`DoesObjectExist` over every
  table/column/declared-index/view/dyn-props side table) plus an insert/select round-trip. Create-from-
  scratch scenarios share the scope (both build from nothing); evolution scenarios build physical V1 via the
  old controller, seed the catalogue with the V1 snapshot under the target scope, then run the new
  controller's `UpdateTables("2.0.0")` and compare against the old controller's incremental result.
- **9 scenarios × 5 drivers = 45 green:** Create (rich model: FK-ordered tables + sorted index + composite
  index + view), CreateTables, Recreate, dynamic-properties create; Evolve add-column, drop-obsolete-property,
  drop-obsolete-entity, add-index, drop-index.
- **Two REAL product divergences found + fixed** (parity gate's whole point; both user-decided "match the
  old controller"):
  1. **`CatalogEntityController.CreateTables` skipped views** — old `CreateTables` materializes views. Fixed:
     a views loop now runs after the tables (views are not catalogued, matching `UpdateTables`).
  2. **`DropColumn` threw on drivers without `DropColumnSupported` (SQLite)** — old controller guards the
     drop and silently leaves the column. Fixed: `ApplyChanges` now guards `dropColumns` on
     `DropColumnSupported`; when unsupported the column lingers (safe fallback — unlike a column *alter*,
     which stays refused because its only fallback is destructive), the desired snapshot is still recorded,
     and the `OnEntityPropertyDrop` hook is skipped. The two mock drop-column tests in
     `CatalogEntityControllerTest` now branch on `DropColumnSupported`.
- **Intended, decided divergences EXCLUDED from parity** (documented in the test file; the old controller
  silently no-ops, the catalogue refuses loudly): a **column definition change**
  (`CatalogColumnAlterNotSupported`) and a **unique single-column index change** (`NotSupportedException`,
  and note this reconcile is genuinely NEW capability — the old `ReconcileIndexes` ignores unique/PK
  indexes entirely, so it never adds/drops them).

**Old-controller deprecation DONE (2026-07-19).** `CreateEntityController` is now discouraged, but its
implementation is retained (AdoptExistingScope + our tests still need it):
- The old introspection implementation was renamed to **`internal class CreateEntityControllerInternal`**
  (`EntityQueries/CreateEntity/CreateEntityControllerInternal.cs`). Our product (`CatalogEntityController`'s
  `AdoptExistingScope`) and all tests use this internal class directly.
- A new **`public [Obsolete] class CreateEntityController`** (same file name) is a thin **pass-through** to
  the internal class — same surface (3 ctors, `OnAction`, `Create/Drop/UpdateTables` + async,
  `failIfUpdateNeeded`), so existing external callers keep compiling (with a deprecation warning).
- The `UpdateMode` enum moved to a **public top-level `EntityUpdateMode`** (`EntityUpdateMode.cs`, chosen
  over nesting on `CatalogEntityController`), used by `CatalogEntityController` + the internal class. The
  obsolete shim keeps its own nested `CreateEntityController.UpdateMode` (identical members) for
  source-compat, mapping to `EntityUpdateMode` when it delegates.
- Tests: all functional usage switched to `CreateEntityControllerInternal` + `EntityUpdateMode`; new
  `CreateEntityControllerObsoleteShimTest` covers the shim itself (is-obsolete + delegates + forwards events).

**Public API surface tightened + docgen (2026-07-19).** The catalogue's **plumbing types are now `internal`**
(they were `public` only for cross-file/test reasons; all are referenced solely by `Gehtsoft.EF.Db.SqlDb` +
the test project, which has `InternalsVisibleTo`): `CatalogStore`, `CatalogChange`/`CatalogChangeKind`,
`CatalogDiff`, `EfCatalogRecord`, `CatalogTableDto` (+ the 8 nested DTO classes), `CatalogSnapshot`,
`CatalogSerializer`. Making them internal (rather than `[DocgenIgnore]`-ing public types — user's point:
"a DocgenIgnore candidate is a good candidate to become internal") keeps the **public API = exactly the
entry points**: `CatalogEntityController` (+ nested `CatalogAdoptMode`), `EntityUpdateMode`, and the obsolete
`CreateEntityController` shim. Full catalogue suite (361) + clean build confirmed the internal entity/serializer
work at runtime. **XML docs on those three public types were rewritten to be docgen-native** per
`CLAUDE/WRITING-DOC-COMMENTS.md`: `[clink=FQN]…[/clink]` instead of `<see cref>`, `[c]…[/c]` instead of
`<c>`, no `<b>`/`<i>`, no `<`/`>`/`≤`/arrows/`e.g.`/`i.e.` in prose, first `<para>` is the brief; internal
dev-narrative (phase/increment numbers, test-class names) dropped from the user-facing summary. Build is
warning-clean (no CS1570/CS1574/CS1587). NOTE: full docgen *render* verification needs the Windows `%docgen%`
tool (not run here); the markup follows the established repo conventions (`[clink]`/`[i]` already in use).

**Remaining increments** (see `CONTROLLER_STACK_PLAN.md`): parity gate is DONE; old-controller deprecation
DONE. Next: **post-parity geo** (geometry + spatial index) exclusively in the new controller, plus the
deferred unique-single-column index reconcile + dedicated composite/JSON index behavioural tests. Still
deferred to Phase 5: **full compare-with-actual drift repair** (introspect→DTO, diff catalogue vs live DB)
and torn-write recovery. (**`CreateTables` patch-seeding — RESOLVED/retired 2026-07-19:** first contact runs
no patches for both entry points, so there is nothing to seed.) The internal `CreateEntityControllerInternal`
is **not** removable (AdoptExistingScope's Practical mode depends on it); only the public name is obsolete.

**Documentation DONE (2026-07-19).**
- **Instance-lock public surface kept public + docgen-clean.** Decision: `IDbInstanceLock`,
  `AcquireInstanceLock`/`TryAcquireInstanceLock`, `DefaultInstanceLockLease` and the `protected virtual`
  `AcquireInstanceLockCore` stay public API (a cross-process DB mutex is a useful, self-contained, 24-test
  primitive, and it gets a user-facing article). Their XML docs were rewritten docgen-native (`LeaseInstanceLock`
  was already internal). Build warning-clean.
- **Guide articles CONSOLIDATED into the existing tutorial (2026-07-19).** First draft was a parallel
  `schema_management` group in `doc1/src/ns/schema_catalogue.ds` (under `sql`) — that duplicated the topic,
  since `tutorialsen` ("Entity Operations") already had `tutorialen_entities5` (Creating and Dropping Tables)
  and `tutorialen_autoupdate` (Automatic Schema Update), both written around the now-obsolete controller.
  Untangled per user: **deleted `schema_catalogue.ds`** and folded everything into `doc1/src/ns/sqltutorialsen.ds`:
  rewrote `tutorialen_entities5` and `tutorialen_autoupdate` **catalogue-first** (CatalogEntityController +
  EnsureCatalogInfrastructure + version + EntityUpdateMode; version guard; greenfield/patch replay;
  preserved the index-reconciliation / `ExcludeFor` / index-naming content, which still applies), and added
  three new articles **`tutorialen_patches`** (coded patches — "Handling Changes the Automatic Update
  Cannot": how/when to write an `IEfPatch`, the replay window, the author rule, a retype-a-column example;
  wired from autoupdate's Limitations), **`tutorialen_adopt`** (old→new migration incl.
  `IsOrphanScopeExists`/`AdoptExistingScope`) and **`tutorialen_instancelock`** (cross-process lock). Static checks pass (balanced `@end` 194/194, no raw
  `&` or angle brackets in prose, no stray bullets; all internal `[link]` targets resolve). **RENDER-VERIFIED
  2026-07-19:** after the user re-ran `rescan.bat` (regenerating `src/raw/` for the new types), the new type
  mentions were upgraded from `[c]` to real `[clink]` links (CatalogEntityController, EntityUpdateMode,
  CatalogAdoptMode, IDbInstanceLock) and the full docgen build (`doc.bat`) passes — "Check links integrity"
  clean, 0 warnings / 0 errors. (Windows `.bat` docgen tooling is runnable from WSL via `cmd.exe /c`.)
- **Brief-line fix (2026-07-19).** The rendered Brief was cut mid-sentence: docgen shows only the FIRST
  PHYSICAL LINE of `<summary>` as the Brief and the rest as Details, so a wrapped opening sentence truncates.
  Rewrote every catalogue + instance-lock public summary (class, methods, properties, enum members) so the
  first `///` line is a complete standalone sentence; detail paras may wrap. Verified in the regenerated
  `src/raw/*.ds` (`@brief=` holds the whole sentence, next line blank — CRLF-aware check) and re-rendered
  clean. The `CLAUDE/WRITING-DOC-COMMENTS.md` rule #1 was corrected accordingly (brief = first LINE, not
  first `<para>`; don't wrap it).

**JSON + dynamic-properties migration now tested through the catalogue (2026-07-19; committed `bd150a3`).** Closed the
noted JSON gap and moved the feature-specific migration suites onto `CatalogEntityController`:
- New shared helper **`Gehtsoft.EF.Test/Catalog/CatalogTestSupport.cs`** — `ResetCatalog(conn, asm)` (drop
  `ef_catalog` + `EnsureCatalogInfrastructure`; needed because `DropTables` only tombstones and the fixture
  reuses one live DB; safe because the assembly disables parallelization) and `Seed(conn, scope, table,
  modelType, version)` (write the "before"-shape DTO so a later `UpdateTables` diffs against it — the seed
  recipe that makes same-table V1→V2 migration testable under scope-keyed catalogue state).
- **`JsonTableUpdateTest`** switched to the catalogue: `UpdateTables_AddsJsonIndex`, `UpdateTables_DropsJsonIndex`
  (split from the old add-and-drop), `UpdateTables_AddsJsonColumn` (+idempotent). Real-DDL JSON index/column
  reconcile through the catalogue is now covered (SQLite/PG/Oracle-gated).
- **`DynamicPropertiesUpdateTablesTest`** switched to the catalogue (gain/lose/idempotent/false-positive) —
  real-DDL side-table gain/drop via `UpdateTables`. (The false-positive test now reflects the catalogue being
  authoritative: it only drops side tables it recorded, so a coincidentally-named table is never touched.)
- **`CatalogParityTest` +`Create_JsonModel_Parity`** — JSON column + value index create parity vs the old
  controller (probes the JSON index name via the fingerprint's `extra` param). Parity is now 10 scenarios.
- The **query-based create tests** (`JsonTableCreateTest`, `DynamicPropertiesCreateDrop*`) were left as-is
  (they test the `GetCreateEntityQuery` DDL layer, not a controller). The deferred "composite/JSON
  index-application behavioural test" gap is now closed for JSON; incremental composite add/drop remains
  mock/diff-only (minor).

**Data-loss safety pass (2026-07-19; committed) — no silent drop/add anywhere in the update process.** A
review (prompted by the geometry drop+add question) found several data-destroying paths; fixed to one
consistent principle: *the catalogue never silently destroys data; a change whose only automatic form is
destructive is refused, and any implicit drop requires opt-in.*
- **Destructive *modifies* → always refuse → patch.** `CatalogDiff` now emits `AlterColumn` (which the
  controller already refuses, `CatalogColumnAlterNotSupported`) for a **column-family change**
  (plain↔JSON↔geometry) and for a **geometry-metadata change** (SRID/subtype/Z/M) — instead of the previous
  `drop+add`, which silently destroyed the column's data. (The family/plain path was **live** data loss; the
  geometry path was inert only because the geo apply is still stubbed — fixed now so geo Phase 3 can't
  reintroduce it.)
- **Implicit data-losing *removals* → new `CatalogDataLossPolicy` flag** on the controller (`Fail` default /
  `Drop`). A **pre-check in `UpdateTables` before any DDL** (atomic refusal): a column that left the model
  **without** `[ObsoleteEntityProperty]` → `CatalogColumnDropWouldLoseData`; a dynamic-properties side table
  drop (owner stopped being an owner) → `CatalogDynamicPropertiesDropWouldLoseData`. **Explicit**
  `[ObsoleteEntityProperty]` / `[ObsoleteEntity]` drops and `Recreate` are exempt (deliberate). Two new
  `EfExceptionCode`s + messages.
- **Tests:** diff family/geometry-metadata tests now assert `AlterColumn`; new
  `Update_UnmarkedColumnDrop_FailsByDefault` + `Update_LoseDynamicProperties_FailsByDefault` (atomic refusal,
  nothing recorded); existing drop-asserting tests set `DataLossPolicy = Drop`. Docs: tutorial autoupdate
  "Limitations" + new "Data-Loss Safety" note.

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
`EntityQueries/Catalog/CatalogEntityController.cs` (increment 5: +`EnsureCatalogInfrastructure`/`…Async`,
+`ICatalogControllerAction.ReplayPatches`, removed internal `EnsureBootstrapped`; adopt: +`CatalogAdoptMode`,
+`IsOrphanScopeExists`, +`AdoptExistingScope`/`…Async`; **increment 6 parity fixes: `CreateTables` now
creates views; `ApplyChanges` guards `DropColumn` on `DropColumnSupported`**);
**`EntityQueries/CreateEntity/CreateEntityControllerInternal.cs`** (the old controller renamed to
`internal`; +`failIfUpdateNeeded` on `UpdateTables`/`…Async` + private `VerifyingAction` decorator +
flag-guards in `ReconcileIndexes`/`ReconcileDynamicPropertiesTable`; nested `UpdateMode` removed → uses
`EntityUpdateMode`); **new `EntityQueries/CreateEntity/CreateEntityController.cs`** (public `[Obsolete]`
pass-through shim to the internal class, keeps a nested `UpdateMode` for source-compat); **new
`EntityQueries/CreateEntity/EntityUpdateMode.cs`** (public top-level enum);
edit `EfSqlException.cs` (+`CatalogFormatTooNew`, `CatalogModelChangedWithoutVersionBump`,
`CatalogVersionRegressed`, `CatalogColumnAlterNotSupported`, `CatalogOrphanPatchHistory`,
`SchemaUpdateRequired`, `CatalogScopeAlreadyAdopted`, `CatalogOrphanScope` + messages);
(orphan guard moved to a pre-check: `ReplayPatches` lost `firstContact`, controller gained `ReadLedgerTip`);
5 `<Compile Include>` entries in `Gehtsoft.EF.Db.SqlDb.csproj`. (Prereq A/B files already present from
earlier.)

Tests (`Gehtsoft.EF.Test/`): `Catalog/Store/CatalogStoreTest.cs`, `Catalog/Diff/CatalogDiffTest.cs`,
`Catalog/Controller/CatalogEntityControllerTest.cs` (+infra bootstrap in `Open`, +5 replay decision tests,
+5 adoption tests [AdoptA/AdoptB entities, `DropAdoptTables`], strict tests allow+verify `ReplayPatches`),
`Catalog/Controller/CatalogPatchReplayTest.cs` (in-memory SQLite E2E replay + post-adopt window);
**`Catalog/Controller/CatalogParityTest.cs`** (increment 6: real-action physical-schema parity, 9 scenarios;
+two mock drop-column tests in `CatalogEntityControllerTest.cs` now branch on `DropColumnSupported`).

## Immediate next action

1. ✅ **DONE (2026-07-16)** — Phase 1 store green on all 5 live drivers (85/85), incl. the
   version-semantics addendum (`Dropped`, `AdvanceVersion`, `WriteTombstone`; `ReadApplied*` skip tips).
2. ✅ **DONE (2026-07-16)** — Phase 3 **increments 1–2**: `CatalogEntityController` skeleton + guard +
   lock + column add/drop, index reconcile (single-col/composite/JSON), `OnEntity*` hooks,
   `AlterColumn`=refuse; 65 green on 5 drivers (see Phase 3 section).
2b. ✅ **DONE (2026-07-16)** — Phase 3 **increments 3–4**: Recreate/CreateNew + FK guard, obsolete→
   tombstone, prop-drop hook, views, dynamic-properties reconcile; 110 green on 5 drivers.
3. ✅ **DONE (2026-07-18)** — Phase 3 **increment 5**: explicit `EnsureCatalogInfrastructure` + de-automation
   + `IEfPatch` replay (catalogue-local `(Vc,Vi]` window keyed on scope version, orphan-history refused). See
   the Increment-5 section and `INCREMENT_5_PATCH_REPLAY.md`.
3b. ✅ **DONE (2026-07-18)** — **`AdoptExistingScope`** (Phase-5 first slice): `IsOrphanScopeExists` +
   `AdoptExistingScope(TrustModel|ReconcileToModel)` + old-controller `failIfUpdateNeeded` verify flag; 25+1
   green. See the AdoptExistingScope section and `ADOPT_EXISTING_SCOPE.md`.
4. ✅ **DONE (2026-07-19)** — Phase 3 **increment 6, parity gate**: `CatalogParityTest` proves real-action
   physical-schema parity vs the old controller (9 scenarios × 5 = 45 green); two real product divergences
   found + fixed (CreateTables views; DropColumn guard on `DropColumnSupported`). See the Increment-6 section
   and `INCREMENT_6_PARITY_GATE.md`.
4b. ✅ **DONE (2026-07-19)** — **old-controller deprecation**: old impl → `internal CreateEntityControllerInternal`;
   new `public [Obsolete] CreateEntityController` pass-through; `UpdateMode` → public top-level
   `EntityUpdateMode`. See the deprecation section above. Full suite stayed green (3607).
5. **NEXT — post-parity geo:** add geometry (add/drop geo column) + spatial-index reconcile as `CatalogDiff`
   entries + `ApplyChanges` cases, **exclusively in `CatalogEntityController`** (confirm the old controller
   never gains it). Then the deferred follow-ups: unique-single-column index reconcile (NEW capability —
   old controller ignores unique indexes); dedicated composite/JSON index-application behavioural tests.
   Phase-5 deferred: full compare-with-actual **drift repair** (introspect→DTO), torn-write recovery.
   (`CreateTables` patch-seeding — RESOLVED/retired 2026-07-19.) The internal `CreateEntityControllerInternal`
   stays (AdoptExistingScope Practical depends on it); only the public name is obsolete.
