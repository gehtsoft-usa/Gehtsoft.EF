# Catalogue controller stack — combined plan (Phases 1–3), for Gate

*Drafted 2026-07-15. Branch `geo`. One combined plan (user chose "plan the whole stack up front"),
covering catalogue **Phase 1 (store) → Phase 2 (diff) → Phase 3 (`CatalogEntityController`)**, ending
with obsolete-old + geo-reconcile **after parity**. Prereqs A (serializer) and B (instance lock) are
DONE + green. Overall design: `DESIGN.md`. Process: [[feedback_phase_process]] — this is the plan gate;
advancing between the three phases stays a checkpoint. **No code until this plan is approved.**
`version.proj` untouched; commit only when asked.*

## Objective

Ship a new `CatalogEntityController` that **maps everything the old `CreateEntityController` does**
(`CreateTables` / `UpdateTables` / `DropTables` + async, `UpdateMode`, `OnAction`, the `OnEntity*`
hooks, obsolete/recreate/dynamic-properties/index handling, `IEfPatch`), but reconciles by **diffing
the declared model against an EF-owned catalogue** instead of introspecting the live DB. Keep the whole
existing test suite green on all 5 drivers throughout; only at the very end mark the old class
`[Obsolete]` and route geo (spatial-index) reconcile exclusively through the new controller.

## Surface to mirror (parity target)

`CreateEntityController` public surface the new class must match 1:1:
- ctors: `(Type findNearThisType, string scope=null)`, `(Assembly, string scope=null)`,
  `(IEnumerable<Assembly>, string scope=null)`
- `event EventHandler<CreateEntityControllerEventArgs> OnAction`
- `CreateTables(conn)` / `CreateTablesAsync`, `DropTables(conn)` / `DropTablesAsync`,
  `UpdateTables(conn, UpdateMode, IDictionary<Type,UpdateMode> individual=null)` / `UpdateTablesAsync`
- `enum UpdateMode { Recreate, Update, CreateNew }`
- Behaviours in today's `UpdateTables`: drop views that became tables; drop obsolete tables and
  `[ObsoleteEntityProperty]` columns; `Recreate` with the FK-dependency guard
  (`EfExceptionCode.CannotRecreateTable`); add missing columns; reconcile indexes (plain + FK +
  composite + JSON); `ReconcileDynamicPropertiesTable`; `OnEntityCreate/Drop/PropertyCreate/PropertyDrop`
  attribute hooks; `IEfPatch` application.

**Deliberate surface addition (Q1, decided):** the catalogue-writing entry points take a **DB version**
string (e.g. `"1.4.0"`):
- `UpdateTables(conn, string version, UpdateMode, IDictionary<Type,UpdateMode> individual=null)` (+ async)
- `CreateTables(conn, string version)` (+ async) — stamps the initial rows.
- `DropTables` needs no version.
The version is the **shared ordering key for structural schema and `IEfPatch`** (same version line for
both). It is stored with the datetime it was first applied.

## Phase 1 — Catalogue store + authority model

**New folder** `Gehtsoft.EF.Db.SqlDb/Catalog/Store/` (netstandard2.0, explicit `<Compile Include>`).

**Storage entity — ONE table, APPEND-ONLY HISTORY (Q3, REVISED 2026-07-16 — supersedes the
one-upserted-row sketch), reserved scope `ef_catalog`, self-bootstrapping like `ef_patch_history`**
(created directly via the create-table builders, NOT through the catalogue itself, to avoid recursion).
One **row per applied state** of a `(scope, tableName)` pair — a new row is appended whenever the
recorded state changes:

```
ef_catalog ( id AUTOID PK, scope, tableName, version, appliedUtc, migrated, schemaFormatVersion, snapshot )
        lookup by (scope, tableName); latest state = highest id where migrated = true
```
- `id` = surrogate PK **and** the monotonic ordering key that picks the latest row.
- `version` = the DB version supplied to Create/UpdateTables (e.g. `"1.4.0"`).
- `appliedUtc` = when **this row's** state was applied.
- `migrated` = whether the DB was actually brought to this state. **Always `true` in v1** (apply then
  record); reserved as the torn-write recovery signal for Phase 5's two-phase `false → apply → true`
  write. `ReadApplied` already ignores `migrated = false` rows, so Phase 5 is a *behaviour* addition
  with **no table change**.
- `snapshot` = the serialized `CatalogSnapshot` text (Prereq A) for this applied state.

**Why history + flag now, recovery later (user, 2026-07-16):** the two are separable. Torn-write
*recovery behaviour* is genuinely deferrable (Phase 5, with compare-to-real-DB). But the storage
**shape** (per-version rows + `migrated`) is the expensive thing to change later — `ef_catalog` is
hand-bootstrapped *outside* the catalogue, so reshaping it later is exactly the fragile manual migration
the catalogue exists to remove, and history has standalone audit / diff-any-two-versions / repair-seed
value now. So v1 ships the history-capable shape; only the resume-in-progress behaviour (+ its 5-driver
crash testing) waits. Growth is bounded: a row is appended **only when the state actually changes** (a
no-op re-run appends nothing).

**`CatalogStore`** (consumes Prereq A serializer; runs inside the controller's Prereq B `IDbInstanceLock`
— the store itself does not take the lock):
- `EnsureBootstrapped(conn)` — create the `ef_catalog` table if absent (idempotent, race-tolerant).
- `CatalogTableDto ReadApplied(conn, scope, tableName)` — the **latest `migrated = true`** row's
  snapshot deserialized; `null` if none. Applies the Prereq-A **refuse gate**: if `IsNewerThanSupported`,
  throw `EfSqlException` (new code `CatalogFormatTooNew`) — zero DDL. (Single-table path + adopt.)
- `IReadOnlyDictionary<string,CatalogTableDto> ReadAppliedForScope(conn, scope)` — **the controller's
  batch read: one query** loads every table's latest applied snapshot for the scope into a
  `tableName → dto` map (reduced to latest-`migrated=true`-per-table in memory), so `UpdateTables`
  does **not** issue a SELECT per table. Same refuse gate per entry. (If a scope's *history* volume
  ever makes transferring superseded snapshots costly, switch this to a group-wise `MAX(id)` two-step;
  not needed for v1 — history grows only on real changes.)
- `WriteApplied(conn, scope, tableName, version, CatalogTableDto)` — **append** a new row when the tip
  state changes; a re-apply of the identical version+snapshot is a no-op (appends nothing). Belt-and-
  braces refuse gate: an existing row from a newer build blocks an older build from appending.
- ~~`AdoptFromModel`~~ **DROPPED 2026-07-16 (user, supersedes Q2).** A trust-the-model "record current
  model as already-applied" seed fits only the ~0% case where the catalogue switch is a release's *sole*
  change and the DB already matches. In the real case (switch shipped *with* model changes) it would
  stamp the new model as applied and **silently skip that release's DDL**. So there is no adopt: first
  contact is `ReadApplied → null` → the controller diffs against `null` (full CreateTable). v1 is
  **greenfield** (catalogue born with the DB); adopting an existing mismatched DB needs actual-state
  introspection = **Phase 5 (compare-with-actual)**.

**Tests** (`Gehtsoft.EF.Test.Catalog.Store`, live all-driver via the fixture — 55 green, 11×5):
bootstrap idempotent; read null when absent; write→read round-trip; **append-on-change keeps history +
reads latest**; **no-op re-run appends nothing**; `appliedUtc` stable on no-op / new row on version
bump; **`ReadApplied`/`ReadAppliedForScope` ignore in-progress (`migrated=false`) rows**; batch
latest-per-table + scope isolation + empty; newer-format snapshot → store refuses.

## Phase 2 — Diff engine (pure, no DB)

**New** `Gehtsoft.EF.Db.SqlDb/Catalog/Diff/`.
- `CatalogDiff.Compare(CatalogTableDto desired, CatalogTableDto stored)` → ordered `CatalogChange[]`.
- `CatalogChange` kinds: `CreateTable`, `DropTable`, `AddColumn`, `DropColumn`, `AlterColumn`
  (parameter/type change — the thing introspection can't see), `AddIndex`/`DropIndex` (plain+composite),
  `AddJsonIndex`/`DropJsonIndex`, `AddSpatialIndex`/`DropSpatialIndex`, `AddGeometryColumn`/`DropGeometryColumn`.
- **`AlterColumn` handling DECIDED (Phase 3 increment 2, 2026-07-16): REFUSE.** The controller throws
  `CatalogColumnAlterNotSupported` and routes a column-definition change to an `IEfPatch`. No portable
  in-place column modify exists (`AlterTableQueryBuilder` is deliberately add/drop-only — column-change
  semantics diverge too much per driver, user-confirmed) and drop+add is destructive. Detection still
  belongs to the diff; only auto-*application* is refused. (Supersedes the earlier "Phase 3 decides
  ALTER-vs-replace" open item.)
- Equality is defined here (RS4): column identity by name; a field-by-field compare flags an alter;
  indexes by logical name + shape. Deterministic ordering (drops before adds within a table; columns
  before their indexes).
- **Pure unit tests** (`Gehtsoft.EF.Test.Catalog.Diff`): add/drop/alter column; size/precision/nullable
  change; add/drop each index kind; geometry subtype/SRID/dim change; no-op on identical DTOs.

## Phase 3 — `CatalogEntityController` + rollout

### Version semantics — DECIDED 2026-07-16 (user)

**`Version` on a live row = the last version at which that table still has this descriptor** (NOT
"last changed at"). So after any successful `UpdateTables(Vi)` **every live table carries `Vi`** — the
version is uniform across the scope and equals the DB's single **current version `Vc`**. `version ↔
whole-schema-state` is a bijection; a version is a complete, immutable snapshot of the entire schema.

Kept cheap by *how* rows are written per run:
- **changed** table (snapshot differs) → **INSERT** a new row `(Vi, newSnapshot)`; the prior latest row
  is untouched and becomes history (its `Version` now reads as the last version that snapshot was valid).
- **unchanged** table → **UPDATE the latest row's `Version` to `Vi` in place** (no new row). The
  append-only guarantee holds for *superseded* rows; only the single **live** row per table is a mutable
  "current pointer", and its `Version` only ever moves forward.
- **new** table → INSERT `(Vi, snapshot)`.
- **dropped** table (in catalogue, gone from model) → **tombstone**: append a row
  `(Vi, Dropped=true, Snapshot=last-known)`. `ReadApplied`/`ReadAppliedForScope` treat a latest
  `Dropped=true` row as **absent** (omitted from the map) so a dropped table neither re-creates nor
  re-drops; reintroducing it in the model diffs `null→CreateTable` (new live row after the tombstone).
  Tombstones are terminal — the stamp-all-to-`Vi` rule skips them; their version ≤ live versions so they
  never trip the regression check. (New `Dropped` bool column on `EfCatalogRecord` — added now, storage
  uncommitted.)

**The guard (scope-level, airtight — model change without a bump is impossible to miss):**
`Vc` = `max(Version)` over latest-per-table live rows (uniform by the stamp rule; `null` = first contact).
- `Vi < Vc` → **throw** regression (older app on a newer DB).
- `Vi == Vc` & any table diff non-empty → **throw** `CatalogModelChangedWithoutVersionBump`.
- `Vi == Vc` & no diffs → clean no-op (idempotent re-run).
- `Vi > Vc` (or first contact) → apply. *Consequence (intended): every schema change requires a version
  bump — no accumulating two changes under one version.*

Version parsing/ordering **reuses the `EfPatch` `major.minor.patch` scheme** (`"1.2"`→`(1,2,0)`,
ordered by `major*10^7 + minor*10^4 + patch`) so structure and patches share one numbering line.

### Apply algorithm

**New** `Gehtsoft.EF.Db.SqlDb/EntityQueries/Catalog/CatalogEntityController.cs`, mirroring the surface
above. Under an `IDbInstanceLock` held across the whole read→guard→diff→apply (lease fallback works now;
native locks are the separate Phase 4):
1. Resolve model → `TableDescriptor` (existing discovery); parse `Vi`.
2. `store.ReadAppliedForScope` (batch, `Dropped` rows excluded) → `stored` map + `Vc`. First contact =
   empty → every table's `stored` is `null`. **No adopt** (greenfield v1; brownfield migration = Phase 5).
3. Per table `CatalogDiff.Compare(FromDescriptor(model), stored)`; **run the scope-level guard above**
   over all tables before emitting any DDL.
4. Emit DDL through the **existing builders** (`CreateTableBuilder`, `AlterTableQueryBuilder`,
   `CreateIndexBuilder` + spatial builders, `DropIndexBuilder`) — reusing the same DDL layer the old
   controller uses, so create-table/geo/JSON DDL is unchanged.
5. Record: **INSERT** changed/new (`WriteApplied`), **UPDATE-version-in-place** unchanged (new store
   method), **tombstone** dropped. Then replay `IEfPatch` in `(Vc, Vi]` via `EfPatchProcessor` (its own
   ledger, unchanged). *Interleaving: structure converges to `Vi` first, then patches replay — patches
   may only touch structure that still exists at the target (intermediate structure can't be reproduced
   on a machine that skipped versions; see Q4).*
- `UpdateMode` mapping: `CreateNew` = create-if-absent only; `Update` = full diff-apply; `Recreate` =
  drop+create (same FK-dependency guard). Views, obsolete drop, dynamic-properties reconcile, `OnEntity*`
  hooks, `IEfPatch` (as a separate ledger integrated in the controller) — all preserved.
- **Store additions needed (small Phase-1 addendum):** `Dropped` column on `EfCatalogRecord`; an
  advance-version-in-place method (`UPDATE … SET Version WHERE ID=@latestId`, via query builders);
  `ReadApplied*` exclude `Dropped=true` latest rows; new store tests (version-advances-without-new-row;
  `Vc` uniform after a mixed changed/unchanged run; tombstone read-as-absent + recreate) on the 5-driver
  suite.
- **Parity gate — DONE 2026-07-19** (increment 6; detail `INCREMENT_6_PARITY_GATE.md`): `CatalogParityTest`
  runs the supported scenarios through both controllers with the real DDL action and asserts an identical
  physical schema (model-derived `DoesObjectExist` fingerprint + behavioural round-trip); 9 × 5 = 45 green.
  Found + fixed two real product divergences (CreateTables now creates views; DropColumn guarded on
  `DropColumnSupported`); column-alter + unique-single-column-index changes stay intended (refused) divergences.

**Only after parity is green on all drivers:**
- Add **geo spatial-index reconcile** (add/drop geo column + spatial index on a live table) as diff
  entries — the parked geo Phase 3 — exclusively in the new controller. Confirm the old controller
  never gains it.
- ~~**Do NOT obsolete `CreateEntityController` in v1**~~ **SUPERSEDED 2026-07-19 (user):** the public
  `CreateEntityController` is now `[Obsolete]` — a pass-through shim to the retained implementation, renamed
  to `internal CreateEntityControllerInternal`. The introspection/brownfield path still exists (the internal
  class + `AdoptExistingScope` use it), but public callers are steered to `CatalogEntityController`. The
  `UpdateMode` enum moved to public top-level `EntityUpdateMode`. See STATE.md → old-controller deprecation.

## Testing strategy

Phase 1 store + Phase 3 controller: live all-driver via `SqlConnectionFixtureBase` (as the geo/dynamic
tests do) + SQLite always. Phase 2 diff: pure in-memory. Test intended behaviour; product bugs →
`KNOWN_ISSUES.md`, never adapt (per [[feedback_test_intended_behavior]]). Assert on AST/behaviour, not
string-match (per [[feedback_test_sql_via_ast]]).

## Decisions — RESOLVED (user, 2026-07-15)

- **Q1 — version identity.** `Create/UpdateTables` take a **DB version** string (e.g. `"1.4.0"`); the
  version is the **shared ordering key for structural schema and `IEfPatch`** (same version line),
  parsed/ordered via the existing `EfPatch` `major.minor.patch` scheme (REVISED 2026-07-16). Semantic of
  the stored `Version` = **"last version at which the table has this descriptor"**, uniform across live
  tables = the DB current version `Vc` (see Phase 3 → Version semantics). `AppliedUtc` per row.
- **Q2 — adopt. REVISED 2026-07-16 → NO ADOPT.** The original trust-model seed is dropped: it is only
  correct when the catalogue switch is a release's sole change (≈never), and is actively unsafe when the
  switch ships with model changes (stamps new model as applied, skips the release's DDL). v1 is
  greenfield — first contact diffs the model against `null` (full CreateTable). Migrating an existing
  mismatched DB onto the catalogue requires actual-state introspection and moves entirely to **Phase 5
  (compare-with-actual)**.
- **Q3 — storage. REVISED 2026-07-16 → append-only history + live "current pointer".** One `ef_catalog`
  table, append-only history of snapshot rows (INSERT on snapshot change; the single latest/live row per
  table has its `Version` UPDATED in place on every run to stay uniform; drops = `Dropped=true`
  tombstone rows). `Migrated` flag present from v1 (always `true`; reserved for Phase-5 torn-write).
  Supersedes the earlier one-upserted-row-no-history sketch.
- **Q4 — `IEfPatch` interleaving. PINNED 2026-07-16.** Structure converges to the target `Vi` first
  (state-based, one jump), then coded patches replay `(Vc, Vi]` in `major.minor.patch` order via
  `EfPatchProcessor` (its own `EfPatchHistoryRecord` ledger, unchanged). **Author rule:** a patch may
  only touch structure that still exists at `Vi` — intermediate structure cannot be reproduced on a
  machine (prod) that jumped versions, so structure can only ever converge to the deployed model.
- **Q5 — new `EfExceptionCode.CatalogFormatTooNew`** for the refuse gate. Confirmed.

## Deferred (noted by user)

**After-failure / torn-write recovery** is designed **together with compare-to-real-DB (Phase 5)** — v1
does not implement resume-from-crash; the DDL-then-record window is a documented limitation until then.

## Constraints

netstandard2.0 explicit `<Compile Include>`; no LINQ in product code; classic `throw new
ArgumentNullException(nameof(x))` (netstandard2.0 has no `ThrowIfNull`); never touch `version.proj`;
tests assert intended behaviour; keep the full suite green at every phase boundary.
