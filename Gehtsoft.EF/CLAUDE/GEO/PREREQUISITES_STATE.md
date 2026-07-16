# GEO prerequisites — Schema Catalogue + DB-instance lock (state)

*Snapshot 2026-07-15. Branch `geo`. Geo Phase 3 is PARKED behind this work ("catalogue first, geo rides
it"). **Both prerequisites (A serializer, B instance-lock) are BUILT + green.** The catalogue controller
stack (Phases 1–3) is PLANNED with all decisions resolved — awaiting the go to start Phase 1 (no code
yet). Full docs: `../SCHEMA_CATALOGUE/DESIGN.md`, `../SCHEMA_CATALOGUE/CONTROLLER_STACK_PLAN.md`
(combined Phases 1–3), `../SCHEMA_CATALOGUE/PREREQ_SERIALIZATION/PLAN.md`, `../DB_INSTANCE_LOCK/PLAN.md`
+ `STATE.md`. See also `STATE.md` (geo itself). Uncommitted; commit only when asked; `version.proj`
untouched.*

## Break point (2026-07-15)

Stopped after resolving the controller-stack plan decisions, before coding Phase 1. Resume at
**"Immediate next action"** below.

## Why geo waits on this

Geo Phase 3 (add geo column + reconcile spatial indexes on a live table) can't ride the current
introspection-based reconciler: spatial indexes hide in per-driver catalogs (MSSQL `sys.spatial_indexes`;
SpatiaLite virtual R-tree invisible to `PRAGMA index_list`; Oracle `ITYP_NAME`; PostGIS/MySQL normal).
Rather than add five bespoke introspection paths, the framework is moving to a **declared-state Schema
Catalogue** that replaces introspection-based `UpdateTables` reconciliation everywhere. Once it lands,
geo column add/**drop** + spatial-index add/drop become plain catalogue-diff entries.

## The initiative (Gate 1 = decisions RESOLVED 2026-07-14; overall design approved pending final sign-off)

Catalogue = EF-owned tables recording schema **as declared/applied**, diffed against the entity model.
Locked decisions:
- **Storage** = serialized snapshot in a **standalone versioned format decoupled from runtime classes**
  (NOT a live-object dump); plain text; scope `ef_catalog`; self-bootstrapping.
- **Forward-compat = HARD REFUSAL, not tolerance.** The updater REFUSES to touch the DB when
  `catalogue.SchemaFormatVersion > this build's max` (an old updater acting on a newer catalogue would
  wreck the live schema). ⇒ single monotonic `SchemaFormatVersion`; no preserve-unknowns; no major/minor.
- **Migration model** — *refined 2026-07-15 (supersedes the earlier append-only/history sketch):* the
  store is **one `ef_catalog` table, one upserted row per `(scope, tableName)`** =
  `{scope, tableName, version, firstAppliedUtc, schemaFormatVersion, snapshot}`. `Create/UpdateTables`
  take a **DB version** string (e.g. `"1.4.0"`); the row keeps the version **and the datetime it was
  first applied**. **No `migrated` flag / version-history in v1** — after-failure (torn-write) recovery
  is DEFERRED and designed together with compare-to-real-DB (Phase 5).
- **Coded migrations (`IEfPatch`)** retained but a **separate ledger integrated into the controller**, not
  folded into the schema table (structural = state-convergence; coded = imperative replay). The DB
  version is the **shared ordering key for schema and patches** (same version line). `ef_patch_history`
  superseded/rethought.
- **Rollout** = new **`CatalogEntityController`** mirroring `CreateEntityController`'s surface (plus the
  `version` argument on Create/Update); old class `[Obsolete]` only **after Phase 3 parity**; no feature flag.
- **Compare-with-actual** = optional later phase (drift repair, Phase 5); default trust-model (adopt seeds
  from the model, no introspection cross-check in v1).
- **Concurrency** = instance-wide `IDbInstanceLock` (Prereq B), held across the whole read→diff→apply.
- **Refuse gate** = new `EfExceptionCode.CatalogFormatTooNew` when a stored snapshot is newer than this
  build supports (zero DDL).

## Prerequisite A — schema-tolerant (de)serialization

- **Status:** **BUILT + green 2026-07-15.** `../SCHEMA_CATALOGUE/PREREQ_SERIALIZATION/PLAN.md`.
  `Gehtsoft.EF.Db.SqlDb/Catalog/`: `CatalogTableDto` (+ column/geometry/spatial-index/json/json-index/
  composite-index/field DTOs + `CatalogColumnDefault`), `CatalogSnapshot` (envelope +
  `SchemaFormatVersion` + `IsNewerThanSupported`), `CatalogSerializer` (`Serialize`/`Deserialize` with
  upgrade-on-read scaffold, `FromDescriptor`, `CurrentSchemaFormatVersion`=1). JSON via
  `System.Text.Json` (camelCase, omit-nulls, deterministic order); enums stored **by name string** in
  the DTOs; `DefaultValue` as `{typeName, invariant-string}` (RS1). Newer-version blob is parsed but
  **flagged** (refuse gate lives in the consumer). 8/8 pure in-memory tests in
  `Gehtsoft.EF.Test/Catalog/Serialization` (round-trip lossless+deterministic, every field flavour,
  enum-by-name, backward defaults, unknown-member ignore, newer-version flag). Solution builds.
- **Note:** no DTO→`TableDescriptor` reverse map in v1 (the diff is DTO-vs-DTO); add one only if repair
  later needs it. Geometry `ClrType` is intentionally not stored (not DDL-relevant); JSON `ClrType`
  stored as `FullName`, opaque for diffing (RS2).

## Prerequisite B — DB-instance lock (`IDbInstanceLock`)

- **Status:** v1 **BUILT + green (SQLite lease path)** 2026-07-15. `../DB_INSTANCE_LOCK/PLAN.md` +
  `../DB_INSTANCE_LOCK/STATE.md` (build state). Portable lease fallback + `IDbInstanceLock` +
  `Acquire`/`TryAcquire` on `SqlDbConnection` + `LockTimeout` + `CurrentServerTimeEpochSeconds` (5
  dialects) done; 9/9 tests green; solution builds. Decided: non-reentrant, lease = per-acquire param
  (default 30 s). **Remaining:** native per-driver advisory locks (Phase 4, live DBs) + live lease
  validation on the other 4 drivers. Own top-level initiative (reusable, not catalogue-specific).
- **Scope:** `IDbInstanceLock : IDisposable` + `AcquireInstanceLock`/`TryAcquireInstanceLock` on
  `SqlDbConnection`; **session/advisory-scoped** (must span auto-committing DDL). Native per driver
  (`pg_advisory_lock` / MSSQL `sp_getapplock @LockOwner='Session'` / MySQL `GET_LOCK` / Oracle
  `DBMS_LOCK`) + portable **`ef_catalog_lock` lease-with-expiry** fallback (SQLite; Oracle w/o grant).
- **Decided:** lease duration **configurable** (default modest, not fixed minutes); new
  `EfExceptionCode.LockTimeout`; no advisory-lock primitive exists in the codebase today.
- **Tests:** namespace `Gehtsoft.EF.Test.InstanceLock` — SQLite lease path (acquire / contended timeout /
  release / expiry reclaim) verifiable **here**; native paths need live DBs (text/AST + live where
  configured), incl. session-drop auto-release.
- **Open:** RL1 session-vs-pooling pinning; RL2 Oracle DBMS_LOCK privilege→lease fallback; RL4 reentrancy
  (lean non-reentrant).

## Phasing (catalogue) & where geo re-enters

Prereq A ✅ + Prereq B ✅ (both built + green) → catalogue **Phase 1** store (`ef_catalog` one-row-per-
`(scope,table)`, adopt/seed, refuse gate; consumes A & B) → **Phase 2** diff engine (DTO-vs-DTO, incl.
alter) → **Phase 3** `CatalogEntityController` drives create/update via diff (+ `IEfPatch`), **then**
obsolete old class + geo reconcile, full-suite parity → **Phase 4** instance-mutex native impls →
**Phase 5** compare-with-actual/repair + torn-write recovery → **then GEO Phase 3** rides it (geo column
add/drop + spatial-index add/drop as plain diff entries).

The user chose to **plan Phases 1–3 as one combined plan** — `../SCHEMA_CATALOGUE/CONTROLLER_STACK_PLAN.md`
(all Q1–Q5 decisions resolved 2026-07-15).

## Phase 1 — Catalogue store (BUILT + green on SQLite, 2026-07-16)

**Storage shape REVISED 2026-07-16 (user):** append-only history + `migrated` flag now, torn-write
*recovery behaviour* still deferred to Phase 5. Rationale: the two are separable; the storage **shape**
is the expensive thing to change later (`ef_catalog` is hand-bootstrapped outside the catalogue), and
history has standalone audit/diff-any-two/repair value — so ship the shape now, defer only the
resume-in-progress behaviour. This **supersedes** the 2026-07-15 "one upserted row, no flag/history"
sketch in the plan/design docs.

`Gehtsoft.EF.Db.SqlDb/Catalog/Store/`:
- **`EfCatalogRecord`** — `[Entity(Table="ef_catalog", Scope="ef_catalog")]`, **one row per applied
  state** (append-only). Columns: `[AutoId] ID` (PK **and** monotonic latest-row key), `Scope` (128,
  non-null, `null`→`""`), `TableName` (128, non-null), `Version` (64, nullable), `AppliedUtc` (DateTime,
  per-row), `Migrated` (bool — **always `true` in v1**; Phase-5 two-phase `false→apply→true` reuses it,
  no table change), `SchemaFormatVersion` (int), `Snapshot` (Size=0 → unbounded text/`clob`/`TEXT`).
  Managed via ordinary entity machinery (like `EfPatchHistoryRecord`) — no recursion.
- **`CatalogStore`** (no raw SQL — all query builders): `EnsureBootstrapped` (idempotent, race-tolerant,
  `GetCreateEntityQuery`); `ReadApplied` (latest `Migrated=true` row + refuse gate); **`ReadAppliedForScope`
  (the controller's ONE-query batch read → `tableName→dto` map; avoids a SELECT-per-table in
  `UpdateTables`; added per user efficiency note 2026-07-16)**; `WriteApplied` (**append** on real change,
  no-op appends nothing; belt-and-braces refuse gate); `AdoptFromModel` (first-contact seed, no-op if
  present). Does **not** take the instance lock — controller (Phase 3) holds `IDbInstanceLock` across
  read→diff→apply.
- **Refuse gate:** new `EfExceptionCode.CatalogFormatTooNew` (in `EfSqlException.cs`).
- **Tests:** `Gehtsoft.EF.Test/Catalog/Store/CatalogStoreTest.cs` — **12 green** on SQLite in-memory
  (bootstrap idempotent; read-null-when-absent; round-trip; append-on-change keeps history + reads
  latest; no-op appends nothing; `AppliedUtc` stable-on-no-op / new-row-on-bump; `ReadApplied` &
  `ReadAppliedForScope` ignore `Migrated=false`; batch read latest-per-table + scope isolation + empty;
  adopt no-op baseline once; refuse newer-format). Query builders only. Structured to also run under the
  all-driver fixture (still to run on the 5 live drivers).
- Two `<Compile Include>` entries added to `Gehtsoft.EF.Db.SqlDb.csproj` (ns2.0, no glob).

**Note (RS3):** the `Snapshot` column uses `Size=0` → `text` on MySQL (64 KB cap). Fine for one
table's snapshot in v1; revisit to `longtext`/`mediumtext` only if a snapshot ever approaches that.
**Note (RS5):** `ReadAppliedForScope` reads the scope's confirmed history rows and reduces to latest-per-
table in memory (one round-trip, aligned with the "model is small, load in memory" design). If history
volume ever makes transferring superseded snapshots costly, switch to a group-wise `MAX(id)` two-step.

## Phase 2 — Diff engine (BUILT + green, pure, 2026-07-16)

`Gehtsoft.EF.Db.SqlDb/Catalog/Diff/` (pure, no DB):
- **`CatalogChange`** (+ `CatalogChangeKind`) — immutable, factory-constructed. Kinds: `CreateTable`/
  `DropTable`, `AddColumn`/`DropColumn`/`AlterColumn`, `AddGeometryColumn`/`DropGeometryColumn`,
  `AddIndex`/`DropIndex` (single-column from `Sorted`/`Unique`), `AddCompositeIndex`/`DropCompositeIndex`,
  `AddSpatialIndex`/`DropSpatialIndex`, `AddJsonIndex`/`DropJsonIndex`. (Refines the plan's grouped
  "AddIndex/DropIndex (plain+composite)" into explicit kinds — recorded in `CONTROLLER_STACK_PLAN.md`.)
- **`CatalogDiff.Compare(desired, stored)`** → ordered `IReadOnlyList<CatalogChange>`. RS4 rules pinned:
  identity by SQL name (rename = drop+add); `AlterColumn` = definition change only
  (DbType/Size/Precision/Nullable/PK/Autoincrement/ForeignTable/Default), carries previous+desired,
  Phase 3 decides ALTER-vs-replace; geometry-metadata change (SRID/subtype/Z/M) → Drop+Add geometry
  column; JSON `ClrType` opaque (RS2) → not a signal, only `Json.Indexes` diff; new columns carry their
  own indexes (separate index changes only for pre-existing columns); column-family change
  (plain↔geo↔json) → replace. **Ordering:** drop indexes → drop columns → alter → add columns → add
  indexes (dependents before columns, columns before their indexes). `stored==null`→CreateTable;
  `desired==null`→DropTable.
- **Tests:** `Gehtsoft.EF.Test/Catalog/Diff/CatalogDiffTest.cs` — 20 green (no-op identical;
  create/drop table; add/drop/alter column incl. size/type/nullable/default; single-column Sorted/Unique
  toggle; composite add/drop + shape-change→drop+add; spatial add/drop on unchanged geometry; geometry
  metadata change→replace; geo column add/drop; JSON index add/drop; JSON ClrType ignored; family
  change→replace; mixed-change ordering).
- Two `<Compile Include>` entries added to `Gehtsoft.EF.Db.SqlDb.csproj`.

## Immediate next action

Phases 1 (store) + 2 (diff) are built + green (SQLite/pure). Still open before/around Phase 3:
**(a) run the Phase 1 store tests on the 5 live drivers** (all-driver fixture, cross-driver parity);
**(b) Phase 2→3 gate**, then **Phase 3 — `CatalogEntityController`** (`Gehtsoft.EF.Db.SqlDb/
EntityQueries/Catalog/`): mirror `CreateEntityController`'s surface + the `version` arg, drive
create/update via `ReadAppliedForScope` → `CatalogDiff.Compare` → emit DDL through existing builders →
`WriteApplied`, under `IDbInstanceLock`; then parity gate on the full suite, obsolete old class, geo
reconcile. Phase-process gate: confirm before coding Phase 3.

Everything is **uncommitted** on branch `geo` (commit only when asked); `version.proj` untouched.
Test suites so far: `Gehtsoft.EF.Test/InstanceLock` (24 green, 5 drivers),
`Gehtsoft.EF.Test/Catalog/Serialization` (8 green, pure), `Gehtsoft.EF.Test/Catalog/Store` (12 green,
SQLite), `Gehtsoft.EF.Test/Catalog/Diff` (20 green, pure). Full suite was 3246 green before Phase 1.
