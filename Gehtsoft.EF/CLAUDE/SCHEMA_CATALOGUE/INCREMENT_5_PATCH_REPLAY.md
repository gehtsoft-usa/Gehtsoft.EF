# Phase 3 — Increment 5: `IEfPatch` replay (IMPLEMENTED 2026-07-18)

> **REVISED 2026-07-19 — first contact runs NO patches (supersedes every "greenfield seeds run / `(0, Vi]`
> on first contact" statement below).** The increment-5 as-built ran all patches ≤ Vi on first contact. That
> was a **divergence from the established `EfPatchProcessor`**, which stamps-and-runs-none on a fresh database
> (`EfPatchProcessor.cs:157-175`) — a fresh DB has nothing to migrate. It was corrected in the increment-6
> follow-up discussion: `UpdateTables` now calls `ReplayPatches` **only when `currentVersion != null`** (a
> real version transition of an already-catalogued DB). First contact — fresh **or** adopted, via
> `UpdateTables` **or** `CreateTables` — runs no patches; the structure is created directly at the target and
> everything ≤ that version is baked in. This makes `CreateTables` and first-contact `UpdateTables`
> consistent (both patch-free) and **retires the "CreateTables patch-seeding" open question** — there was no
> real Create-vs-Update distinction, only a bug. Patches run **only** on the `(Vc, Vi]` transition. See the
> "REVISED" notes inline and `STATE.md` → Increment-5 section.

*Branch `geo`. Realizes Q4 (PINNED 2026-07-16) of `CONTROLLER_STACK_PLAN.md`. **The design below evolved
during the gate discussion** — the sections marked "AS BUILT" are authoritative; earlier prose is kept
for rationale. Key deviations from the first draft: (1) an explicit `EnsureCatalogInfrastructure` bootstrap
replaces per-method auto-bootstrap; (2) the replay window is **`(Vc, Vi]` keyed on the scope version**
(NOT the ledger) — a patch ≤ Vc is already baked into the structure and never runs, which is what keeps
`CreateTables` correct; first contact (`Vc=0`) ⇒ `(0, Vi]` = all ≤ Vi so greenfield data seeds run;
(3) first contact with a pre-existing ledger is **refused** (`CatalogOrphanPatchHistory`) pending explicit
Phase-5 adoption; (4) replay drives its **own** window (does NOT delegate to `EfPatchProcessor.ApplyPatches`,
whose present-empty-ledger behaviour is an untested artifact). All operational flows (incl. old→new
migration) are in `FLOWS.md`. Test status: catalogue+lock+patch sweep **304 green** (controller 135 = 27×5,
+ 5 in-memory replay end-to-end).*

## AS BUILT — summary

- **`EnsureCatalogInfrastructure(conn)`** (+ async): the single explicit bootstrap. Creates `ef_catalog`
  **and** `ef_patch_history` if absent (idempotent, race-tolerant). `CreateTables`/`UpdateTables`/
  `DropTables` no longer self-bootstrap — a missing catalogue just lets the store's SELECT fail (a
  heuristic/adopt path comes in Phase 5). Call it once before the others.
- **Replay window (catalogue-local, in `ICatalogControllerAction.ReplayPatches`), keyed on the SCOPE
  VERSION:** run every discovered patch with `VersionKey(Vc) < key ≤ VersionKey(Vi)` (`Vc` = scope version
  before the run), recording each into `ef_patch_history`. A patch ≤ Vc is already reflected in the structure
  (created/converged at Vc) and never runs. **REVISED 2026-07-19: first contact runs NO patches** (see the
  banner) — `ReplayPatches` is called only when `currentVersion != null`. Safe under the author-rule.
  `EfPatchProcessor` is untouched (only `FindAllPatches` reused). The ledger is used only for orphan detection
  + recording — **not** as the window bound (an earlier as-built keyed on ledger-max, which re-ran pre-baked
  patches after `CreateTables`; fixed).
- **Orphan guard:** on **first contact** (`Vc == null`) with a **non-empty** ledger for the scope →
  `EfSqlException(CatalogOrphanPatchHistory)`. Real import/decide = Phase-5 `AdoptExistingScope`.
- **Where it fires:** end of `UpdateTables`, after structure converges, **on a real transition only**
  (`currentVersion != null` && `Vi > Vc`). **REVISED 2026-07-19:** first contact (`currentVersion == null`)
  runs no patches (see the banner). The clean no-op (`Vi == Vc`, no diff) returns before replay — the ledger
  is already current; the only unrun-patch-at-`Vc` case is a mid-run crash (Phase-5 torn-write).
- **Scope:** keyed to the controller's entity scope `mScope` (Edge-B decision — patches share the entity
  scope; migrating a differently-scoped old ledger is a documented manual prerequisite).
- **Async:** `UpdateTablesAsync` stays `Task.Run(sync)`, so patches run via sync `Apply` even for
  `IEfPatchAsync` (which always has one). v1 limitation.

## Original rationale (pre-discussion draft below)

## Objective

Integrate coded-patch (`IEfPatch`) replay into `CatalogEntityController` so a single
`UpdateTables(conn, version, …)` brings a deployment fully current: **state-based structure convergence
first, then coded patches**. Reuse `EfPatchProcessor` and the existing `ef_patch_history` ledger verbatim
— no new patch storage, no change to the patch numbering scheme (both already share the
`major.minor.patch` → `major*10^7 + minor*10^4 + patch` key the controller's `VersionKey` uses).

## What already exists (reused, unchanged)

- `EfPatchProcessor.FindAllPatches(assemblies, scope)` — discovers `[EfPatch]`+`IEfPatch` types, sorted by
  version. Scope filter: empty/null scope → **all** patches; non-empty → attribute-scope match.
- `EfPatchProcessor.ApplyPatches(conn, patches, scope)` / `ApplyPatchesAsync` — the ledger machinery:
  - **ledger absent** (`ef_patch_history` missing) → create it and **stamp the latest patch as applied,
    running none** (greenfield: patches are baked into the freshly-created schema).
  - **ledger present** → run every patch with version `> lastApplied`, saving each to the ledger.
- `IEfPatchAsync : IEfPatch` — an async patch always also carries a sync `Apply`.

**Gap vs the plan:** `ApplyPatches` has **no upper bound** — it runs through the newest patch found. The
plan's window is `(Vc, Vi]`. So the controller must pass `ApplyPatches` a list **pre-filtered to
version ≤ Vi**. The lower bound stays the ledger's `lastApplied` (which is the correct, idempotent
realization of `Vc`: the ledger is the source of truth for which patches actually ran).

## Design

### 1. A replay seam on `ICatalogControllerAction`

Consistent with the increment-1..4 philosophy (DDL *decisions* are unit-tested driver-agnostically via an
injectable action), add one method:

```csharp
void ReplayPatches(SqlDbConnection connection, IEnumerable<Assembly> assemblies, string scope, long throughVersionKey);
```

Default implementation (`CatalogControllerAction`):
```csharp
public void ReplayPatches(SqlDbConnection connection, IEnumerable<Assembly> assemblies, string scope, long throughVersionKey)
{
    IList<EfPatchProcessor.EfPatchInstance> all = EfPatchProcessor.FindAllPatches(assemblies, scope);
    var window = new List<EfPatchProcessor.EfPatchInstance>();
    foreach (var p in all)
        if (PatchKey(p.Version) <= throughVersionKey)   // (Vc, Vi] upper bound; ledger gives the lower bound
            window.Add(p);
    connection.ApplyPatches(window, scope);
}
```
`PatchKey` mirrors the existing `major*10^7 + minor*10^4 + patch`. Tests inject a mock action and assert
`ReplayPatches` is invoked once with the right `(scope, throughVersionKey)`; a handful of live end-to-end
tests use real `[EfPatch]`/`IEfPatch` classes on SQLite (as `PatchTest` does) to prove actual execution
and ledger state.

### 2. Where replay fires in `UpdateTables`

After the create/update/view phases converge structure to `Vi`, **still inside the instance lock**, and
**only on the apply path** (`Vi > Vc` or first contact):

- `Vi < Vc` → throw regression (unchanged) — no replay.
- `Vi == Vc` & any diff → throw (unchanged) — no replay.
- `Vi == Vc` & no diff → clean no-op **returns before replay**. Rationale: on an idempotent re-run the
  ledger already holds every patch ≤ Vi, so `ApplyPatches` would be a no-op anyway; keeping the no-op path
  free of a ledger round-trip preserves "a no-op re-run touches nothing." (The only way a patch ≤ Vc stays
  unrun is a crash between structure-write and patch-run — that is the torn-write case explicitly deferred
  to **Phase 5**.)
- `Vi > Vc` / first contact → apply structure, then `ActionController.ReplayPatches(conn, mAssemblies,
  PatchScope, VersionKey(version))`.

### 3. `CreateTables` / `DropTables` — patch-free (DECIDED 2026-07-18; RESOLVED 2026-07-19)

**Decision (user):** seeding the patch ledger is a *separate discussion* — "too much automation here is not
good." So `CreateTables` and `DropTables` are patch-free; only `UpdateTables` replays.

**RESOLVED 2026-07-19 — the "seeding discussion" collapsed to nothing.** The follow-up analysis showed the
apparent Create-vs-Update asymmetry was an artifact of the increment-5 bug (first-contact `UpdateTables`
running `(0, Vi]`), not a real design question. Once first contact runs **no** patches for **both** entry
points (matching `EfPatchProcessor`'s stamp-and-runs-none on a fresh DB), the two are consistent and there is
nothing left to seed: a fresh install builds structure directly at the target version and everything ≤ that
version is baked in; patches exist to migrate an **existing** DB across versions, and a fresh DB has nothing
to migrate. `CreateTables(Vc)` then `UpdateTables(Vi)` runs exactly `(Vc, Vi]` — same as a fresh
`UpdateTables(Vc)` then `UpdateTables(Vi)`. No `CreateTables` ledger-stamping is needed or wanted.

*(The earlier "sharp edge" text here described a skip under the old `EfPatchProcessor.ApplyPatches`
delegation, which this increment never used — the scope-version window already avoided it. That concern is
obsolete.)*

### 4. Scope keying — **DECIDED 2026-07-18: key to `mScope`**

Replay is keyed to the controller's entity scope `mScope` (passed straight to `FindAllPatches`/
`ApplyPatches`). A null/empty-scope controller replays **all** patch scopes (existing `FindAllPatches`
semantics — empty scope = no filter); a non-empty scope replays only patches whose
`EfPatchAttribute.Scope` matches. No separate `PatchScope` property in v1. (Note: entity default scope is
`""` while patch default is `"default"`, so a default-scope controller does not isolate to only `"default"`
patches — accepted.)

### 5. Async

`UpdateTablesAsync`/`CreateTablesAsync` remain `Task.Run(sync)` (the controller's existing pattern), so
patches run through the **sync** `ApplyPatches` → sync `IEfPatch.Apply` even for `IEfPatchAsync` (which
always has one). True async patch dispatch is not wired in v1 — consistent with the controller's uniform
`Task.Run` async and noted as a limitation. (Revisit if/when the controller gains real async internals.)

## Tests (`Gehtsoft.EF.Test/Catalog/Controller/`)

Mock-action decision tests (all 5 drivers, symmetric with existing increments):
- apply path (`Vi > Vc`) → `ReplayPatches` called once with `throughVersionKey == VersionKey(Vi)` and the
  controller's scope; called **after** structural apply.
- **REVISED 2026-07-19:** first contact → `ReplayPatches` **not** called (`Update_FirstContact_DoesNotReplayPatches`).
- `Vi == Vc` no-op → `ReplayPatches` **not** called.
- regression / model-changed-without-bump throws → `ReplayPatches` **not** called.
- `CreateTables` / `DropTables` → `ReplayPatches` **not** called (patch-free).

Live end-to-end (SQLite, real patch classes in a dedicated scope):
- **REVISED 2026-07-19:** fresh DB first contact → runs **no** patches (`FirstContact_FreshDatabase_RunsNoPatches`).
- catalogued DB, `UpdateTables(Vi)` → runs only patches in `(Vc, Vi]`, in order, each saved to the ledger.
- `CreateTables(Vc)` then `UpdateTables(Vi)` → runs only `(Vc, Vi]`, never the pre-baked ≤Vc patches.
- adopt at `Vc` then `UpdateTables(Vi)` → runs only `(Vc, Vi]`.
- upper bound honored: a patch authored at a version `> Vi` is **not** run until a run reaches its version.
- idempotent re-run at the same `Vi` (no diff) runs no patch.

## Constraints (unchanged)

netstandard2.0 explicit `<Compile Include>`; **no LINQ** in product code (`FindAllPatches` uses LINQ
internally but that is pre-existing and untouched — the new filter loop is explicit); classic
`throw new ArgumentNullException`; never touch `version.proj`; assert intended behaviour on
behaviour/ledger state, not string-match; keep the full suite green.

## Open decisions for the gate — BOTH RESOLVED

1. **Patch scope binding** (§4) — RESOLVED 2026-07-18: keyed to `mScope`; no separate `PatchScope` in v1.
2. **`CreateTables` seed** (§3) — RESOLVED 2026-07-19: `CreateTables` stays patch-free, and first-contact
   `UpdateTables` was corrected to also run no patches, so the two are consistent and there is nothing to
   seed. The question was an artifact of the increment-5 first-contact bug, not a real design choice.
