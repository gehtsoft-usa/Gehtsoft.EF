# Schema Catalogue — operational flows

*Created 2026-07-18 (during increment 5). The behaviour of `CatalogEntityController` across every
situation we have identified, including the **migration from the old `CreateEntityController`**. Terms:
`Vc` = the scope's current version (from the catalogue, `ReadCurrentVersion`, `null` on first contact);
`Vi` = the version passed to the call. Version key = `major*10^7 + minor*10^4 + patch`; `VersionKey(null)=0`.*

## The three controller methods — one-line contracts

| Method | Structure | Patches | Version |
|---|---|---|---|
| **`EnsureCatalogInfrastructure(conn)`** | creates `ef_catalog` + `ef_patch_history` if absent | — | — |
| **`CreateTables(conn, Vi)`** | creates every table **unconditionally** in its **current** model state, then the views (increment-6 parity fix) | **none — CreateTables never runs a patch** | stamps every table's scope version = `Vi` |
| **`UpdateTables(conn, Vi, mode)`** | reconciles model↔catalogue; a table **absent** from the catalogue is created in its current state (diff-vs-null), an existing one is diffed and altered | replays the window **`(Vc, Vi]`** after structure converges — **only on a real transition; first contact runs none** (REVISED 2026-07-19) | stamps every live table = `Vi` |
| **`DropTables(conn)`** | drops all discovered tables, tombstones them | — | — |

`EnsureCatalogInfrastructure` is the **single explicit bootstrap**; the other three no longer self-create
the tables (a missing catalogue simply lets the store's SELECT fail — the heuristic/adopt path is Phase 5).

## The patch model (why `CreateTables` runs nothing and `UpdateTables` runs `(Vc, Vi]`)

- **Structure is state-based**: `CreateTables`/`UpdateTables` bring the schema to the *current* model in
  one jump. Intermediate structural states are neither reproduced nor needed.
- **Patches are imperative** `IEfPatch.Apply`, replayed in `major.minor.patch` order, once each.
- **The window is keyed on the scope version, not the ledger.** A patch whose version ≤ `Vc` is already
  reflected in the structure (the tables were *created or converged at* `Vc`), so it must not run. Only
  patches in **`(Vc, Vi]`** run. This is what makes `CreateTables` safe: it creates at head `Vi` and stamps
  `Vc = Vi`, so a later `UpdateTables` replays only `(Vi, …]` — never the already-baked-in `≤ Vi` patches,
  **even though the ledger is empty**. (The empty ledger is *not* read as "run everything"; the scope
  version is the bound. The ledger is used only for orphan detection and for recording what ran.)
- **First contact runs NO patches (REVISED 2026-07-19).** Replay fires only on a real version transition of
  an already-catalogued DB (`currentVersion != null`). A fresh DB — whether created by `CreateTables` or by a
  first-contact `UpdateTables` — is built directly at the target version, so everything ≤ that version is
  baked in and there is nothing to migrate; this matches the old `EfPatchProcessor`, which stamps-and-runs-
  none on a fresh database. Patches exist to migrate an **existing** DB across versions.
- **Author rule:** a patch may only touch structure still present at `Vi`. This is what makes replaying the
  `(Vc, Vi]` subset safe against the head schema.
- **Ledger = the same `ef_patch_history` table (`EfPatchHistoryRecord`) the old `EfPatchProcessor` uses.**
  We reuse the physical table and rows; we borrow only `FindAllPatches` for discovery and drive the window
  ourselves (we do **not** call `EfPatchProcessor.ApplyPatches`). The old
  `connection.ApplyPatches(patches, scope)` extension still exists and writes the same table, so the two
  mechanisms **coexist on one ledger** — good for migration continuity (see Flow 6).
- **Scope:** patch replay is keyed to the controller's entity scope (`mScope`); patches must share the
  entity scope (the original intent).

## Flows

### 1. Greenfield via `UpdateTables` (the common path)
Fresh DB. `EnsureCatalogInfrastructure` → `UpdateTables(Vi)`.
- `Vc = null` (first contact) → **every table created** in its current state (diff-vs-null).
- **REVISED 2026-07-19: no patches run on first contact.** The schema is born at head `Vi`; everything ≤ Vi
  is baked in. (Was: "window `(0, Vi]`, all patches run" — a divergence from `EfPatchProcessor`, corrected.)
- Result: `Vc = Vi`, ledger empty.

### 2. Greenfield via `CreateTables` (create-at-head, no migrations)
Fresh DB. `EnsureCatalogInfrastructure` → `CreateTables(Vi)`.
- Every table created at head; **no patches run**; `Vc = Vi`; ledger empty.
- This is the *EnsureCreated* semantic: the schema is born at `Vi`, historical migrations are irrelevant.
- **Identical to Flow 1 for the patch dimension** (both first-contact paths run no patches). The only
  difference is structural: `CreateTables` creates unconditionally; first-contact `UpdateTables` diffs against
  null (same result on a fresh DB). *(The earlier "CreateTables should maybe opt-in-seed data patches"
  discussion is retired 2026-07-19 — there is no Create-vs-Update patch asymmetry to reconcile.)*

### 3. `CreateTables(Vx)` then a later `UpdateTables(Vy>Vx)`
The scenario that pinned the window rule.
- After `CreateTables(Vx)`: `Vc = Vx`, ledger empty, tables already at head-of-`Vx`.
- `UpdateTables(Vy)`: window `(Vx, Vy]` ⇒ runs only patches newer than `Vx`. The `≤ Vx` patches are
  **not** re-run despite the empty ledger. (Test: `CreateTables_ThenUpdate_DoesNotRerunPreBakedPatches`.)

### 4. Normal incremental release
`UpdateTables(V1)` … later `UpdateTables(V2>V1)`.
- Structure diffs model↔catalogue and applies the delta (add/drop column, index reconcile, dyn-props, …);
  any brand-new table is created in its current state.
- Patches window `(V1, V2]` run in order, each recorded.
- Every live table stamped `V2`; `Vc = V2`.

### 5. Idempotent re-run (no changes)
`UpdateTables(Vi)` with `Vi == Vc` and no model diff → **clean no-op**: no DDL, **no replay** (the ledger
is already current; the only way a `≤ Vc` patch is unrun is a mid-run crash = Flow 8/torn-write, Phase 5).

### 6. Migration from the old `CreateEntityController` to the catalogue
A DB previously managed by `CreateEntityController` (+ possibly `connection.ApplyPatches`). Two facts hold:
the **tables already exist**, and there is **no `ef_catalog`** for the scope (and maybe existing
`ef_patch_history` rows).

**What happens if you just point `CatalogEntityController` at it (v1, no adoption):**
- `EnsureCatalogInfrastructure` creates `ef_catalog` (+ `ef_patch_history` if absent).
- `UpdateTables(Vi)`: `Vc = null` (catalogue empty = first contact) → the **orphan pre-check runs BEFORE any
  DDL** and refuses cleanly:
  - a discovered non-view table **physically exists** → `EfSqlException(CatalogOrphanScope)`;
  - else `ef_patch_history` has rows for the scope → `EfSqlException(CatalogOrphanPatchHistory)`.
  Either way **zero DDL, catalogue not seeded** — a clean "adopt first" signal. (The pre-check is why the
  guard is at the top of `UpdateTables`, not inside replay: replay runs *after* structure is written, so a
  late guard would leave the catalogue seeded at `Vi` and permanently mask the orphan. Fixed 2026-07-18.)

**Migration procedure (implemented — `AdoptExistingScope`, 2026-07-18):**
1. `EnsureCatalogInfrastructure(conn)`.
2. If the old `connection.ApplyPatches` used a **different scope string** than the entity scope, update the
   `Scope` column of the relevant `ef_patch_history` rows to the entity scope first (the new replay keys on
   `mScope`; otherwise those rows are invisible and the patches could re-run). *Edge-B prerequisite.*
3. `AdoptExistingScope(conn, currentVersion, mode)` — seeds `ef_catalog` from the model at
   `currentVersion` (so the scope is no longer "first contact"), **runs no patches**:
   - **`TrustModel`** (Recommended): verifies the DB already matches the model — throws
     `SchemaUpdateRequired` if not — then records. Zero DDL.
   - **`ReconcileToModel`** (Practical): brings the DB to the model via the old controller's introspection
     reconcile (`UpdateTables(failIfUpdateNeeded:false)`), then records. Always sufficient because no
     adoptable (pre-catalogue) DB can contain geo, and geo is the only thing the old controller can't do.
4. Deploy normally thereafter (Flow 4): `UpdateTables(Vi>currentVersion)` diffs against the seeded state and
   replays `(currentVersion, Vi]` — the pre-existing ledger rows ≤ `currentVersion` never re-run.

`IsOrphanScopeExists(conn)` detects the condition (no catalogue for the scope + a table physically exists).
`AdoptExistingScope` refuses (`CatalogScopeAlreadyAdopted`) if the scope is already catalogued. The old
`CreateEntityController` stays available (Practical *depends* on it) — not obsoleted in v1. *Full
compare-with-actual drift repair (introspect→DTO) is still later Phase 5.*

### 7. Orphan pre-check (Flow 6, guarded before any DDL)
At **first contact** (`Vc == null`), `UpdateTables` refuses a pre-catalogue DB before touching anything:
a discovered non-view table exists → `CatalogOrphanScope`; else a scope ledger row exists →
`CatalogOrphanPatchHistory`. Zero DDL, catalogue not seeded. Resolve with `AdoptExistingScope` (Flow 6).
(Tests: `Update_OnPreCatalogueDatabase_RefusesAsOrphan_BeforeAnyDdl`,
`FirstContact_WithExistingLedger_IsRefusedAsOrphan` — the latter also asserts the catalogue stays unseeded.)

**Note on `TrustModel` verify scope:** verify (via the old controller's `failIfUpdateNeeded`) throws on
**missing or changed** structure the reconcile would act on — a missing table/column, a missing/changed
framework-owned index, a missing/orphan EAV side table. It does **not** flag an **extra column that exists
in the DB but is absent from the model** (the additive reconcile never drops it); such a column simply
lingers, invisible to the catalogue, harmless. (Our five drivers all enumerate indexes, so index drift *is*
seen; the `GetTableIndexes == null` short-circuit is a defensive no-op for hypothetical drivers only.)

### 8. Torn write (deferred to Phase 5)
`UpdateTables(Vi)` applies structure (catalogue now `Vi`) then **crashes mid-replay**. Re-run
`UpdateTables(Vi)`: `Vi == Vc` → clean no-op → replay is skipped → the unrun `(old-Vc, Vi]` patches stay
unrun. v1 does not recover from this; Phase 5 (compare-with-actual + `migrated` two-phase flag) detects and
resumes. Documented limitation.

### 9. Version guards (before any DDL)
- `Vi < Vc` → `CatalogVersionRegressed` (older app on a newer DB).
- `Vi == Vc` **and** any model diff → `CatalogModelChangedWithoutVersionBump` (bump the version when the
  model changes — every schema change requires a bump).
- `Vi == Vc` and no diff → Flow 5. `Vi > Vc` → apply + replay `(Vc, Vi]`; **first contact → apply structure,
  no replay** (Flows 1–2).
- All guards throw **before** any structure DDL and before replay.

## Async
`*Async` methods are `Task.Run(sync)`; patches therefore run via sync `IEfPatch.Apply` even for
`IEfPatchAsync` (which always also has a sync `Apply`). True async patch dispatch is a v1 limitation.
