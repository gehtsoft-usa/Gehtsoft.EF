# AdoptExistingScope — IMPLEMENTED 2026-07-18

*Branch `geo`, uncommitted. The first slice of **Phase 5** (compare-with-actual): onboarding a
**pre-catalogue database** onto the catalogue. Closes the migration path increment 5's orphan-patch guard
points at (`CatalogOrphanPatchHistory` → "adopt the scope first"). Built exactly as the unified plan below
(user's `failIfUpdateNeeded` refinement, complete verify option (i)). Flows: `FLOWS.md` §6/§7. `version.proj`
untouched. Tests: adoption 5×5 = 25 + post-adopt patch 1 (in-memory) green; `failIfUpdateNeeded` covered via
the adoption drift/reconcile cases. Full compare-with-actual **drift repair** (introspect→DTO) is still
later Phase 5.*

## Problem

A database managed **before** the catalogue existed (by the old `CreateEntityController`, ± manual
`connection.ApplyPatches`) has **tables that physically exist** but **no `ef_catalog`** entry for the
scope. Pointing `CatalogEntityController.UpdateTables` at it fails two ways (by design):
1. first contact (`Vc == null`) diffs the model against `null` → `CreateTable` on **already-existing
   tables** → error; and
2. the orphan-patch guard throws `CatalogOrphanPatchHistory` when the ledger already has rows.

Adoption seeds the catalogue so the scope is no longer "first contact", after which normal `UpdateTables`
works. **Adoption runs no patches** — it only establishes the `Vc = V` baseline; patches replay later via
`UpdateTables`, whose `(Vc, Vi]` window then correctly skips everything ≤ `V`.

## Unified implementation (user refinement, 2026-07-18)

Rather than two branches ("TrustModel = write-only" vs "Practical = reconcile-then-write"), **both modes go
through one path**: always call the **old `CreateEntityController.UpdateTables`** with a new
**`failIfUpdateNeeded`** flag, then run the **single** `FromDescriptor` catalogue-write. The mode is *just*
the flag value:

| Mode (public) | `failIfUpdateNeeded` | Old `UpdateTables` behaviour |
|---|---|---|
| **Recommended = `TrustModel`** | `true` | **verify**: apply nothing; **throw** if any structural change would be needed (DB ≠ model) |
| **Practical = `ReconcileToModel`** | `false` | **reconcile**: bring the DB to the model (today's behaviour) |

Then, identically for both, write `FromDescriptor(model)` at `V` for each non-view table. **Neither touches
the patch ledger.**

**Why this is better than the original two-branch plan:** `TrustModel` now **verifies** model == DB (fails
loudly on drift) instead of blindly writing a catalogue that could silently lie. One code path, one
`FromDescriptor` write; the "mode" is a single boolean.

**Why the old controller (both modes):** it reuses proven introspection-based brownfield reconcile, avoids
lossy "best guess" DTO recovery (SQLite column sizes, SRID, …). **It is always sufficient** because the
only thing it can't reconcile is **geo**, which appears only in the *new* controller (post-parity) — so no
DB old enough to need adoption can contain geo (user's point). **Known limitation (both modes):** the old
controller is *additive* — a pre-existing column whose size/type differs from the model is not altered, so
`ReconcileToModel` won't fix it and `TrustModel`'s verify won't necessarily catch it (same `ALTER = refuse
→ patch` gap the catalogue already has).

### The `failIfUpdateNeeded` flag on `CreateEntityController.UpdateTables`

New optional param (default `false` = today's behaviour, existing callers unaffected):
`UpdateTables(conn, mode, individual=null, bool failIfUpdateNeeded=false)` (+async). When `true`: perform
**no** structural DDL; **throw `SchemaUpdateRequired`** (new `EfExceptionCode` + message) the moment a
structural change would be needed. **Views are ignored** (they are always drop+recreate, not structural
drift — neither recreated nor counted in verify mode).

**Implementation note — where the mutations are.** The old controller routes **table/column** DDL through
the injectable `ICreateEntityControllerAction` (`Create`/`Drop`/`AddColumns`/`DropColumns`) — a *verifying
decorator* that throws on any non-view mutation (and no-ops view calls) catches those cleanly by swapping
`ActionController` when the flag is set. But **index** and **dynamic-properties** reconcile run through
separate direct helpers (`ReconcileIndexes`, `ReconcileDynamicPropertiesTable`), *not* the action surface.
Two options:
- **(i) complete verify** — also guard those helpers with the flag (throw before changing an index / side
  table). A few more touch-points in the legacy method; recommended for a truthful "model == DB" check.
- **(ii) tables+columns verify** — decorator only; index/dynprops drift not caught. Lighter, and in
  practice low-risk (a DB previously managed by the old controller already has the model's indexes/side
  tables, since the old controller kept them in sync), but not a complete guarantee.

Recommend **(i)**; decide at coding time.

## API (on `CatalogEntityController`)

```csharp
public enum CatalogAdoptMode { TrustModel, ReconcileToModel }   // Recommended / Practical

// True when the scope has NO catalogue yet but at least one discovered (non-view) table physically
// exists in the DB — i.e. a pre-catalogue database that must be adopted rather than created/updated.
public bool IsOrphanScopeExists(SqlDbConnection connection);

// Seeds the catalogue for a pre-catalogue database at `version` and returns. Under the instance lock.
// Refuses (CatalogScopeAlreadyAdopted) if the scope is already catalogued (Vc != null). Runs NO patches.
public void AdoptExistingScope(SqlDbConnection connection, string version, CatalogAdoptMode mode = CatalogAdoptMode.TrustModel);
public Task AdoptExistingScopeAsync(SqlDbConnection connection, string version, CatalogAdoptMode mode = CatalogAdoptMode.TrustModel);
```

- Infrastructure is assumed present (`EnsureCatalogInfrastructure` first — consistent with the increment-5
  de-automation; a missing catalogue just lets the store SELECT fail).
- `IsOrphanScopeExists`: `ReadCurrentVersion(scope) == null` **and** at least one discovered non-view table
  returns true from `DoesObjectExist`. (Greenfield — no catalogue *and* no tables — is **not** orphan; use
  `CreateTables`/`UpdateTables`.)
- `AdoptExistingScope`:
  1. acquire the instance lock;
  2. if `ReadCurrentVersion(scope) != null` → throw `CatalogScopeAlreadyAdopted` (new code + message);
  3. `new CreateEntityController(mAssemblies, mScope).UpdateTables(connection,
     CreateEntityController.UpdateMode.Update, failIfUpdateNeeded: mode == CatalogAdoptMode.TrustModel)` —
     `TrustModel` verifies (throws `SchemaUpdateRequired` on drift), `ReconcileToModel` aligns the DB;
  4. `types = LoadTypes(includeObsolete:false)`; for each non-view table:
     `mStore.WriteApplied(connection, mScope, info.Table, version, DesiredDto(info))`;
  5. no patch replay, no ledger writes.

## Interactions / correctness

- **Resolves the orphan-patch guard:** after adoption `Vc = V`, so a subsequent `UpdateTables` is no longer
  first contact → the `CatalogOrphanPatchHistory` guard does not fire, and existing ledger rows ≤ `V` never
  re-run (the window is `(V, Vi]`, keyed on the scope version, not the ledger). Adoption therefore needs no
  ledger writes — consistent with "no patches involved."
- **Version choice:** `V` should be the app's current version; ideally `V ≥` the last patch already applied
  in the ledger (so no already-applied patch falls into a future `(V, Vi]` window). Not hard-guarded in v1
  — noted.
- **Idempotency:** adoption is a one-shot; a second call is refused (already catalogued). Re-adopting
  requires clearing the scope's catalogue rows first (manual / a future reset).
- **Not** obsoleting the old `CreateEntityController`: Practical *depends* on it; it stays.

## Tests (`Gehtsoft.EF.Test/Catalog/Controller/`, 5 live drivers via the fixture)

- `IsOrphanScopeExists`: tables-exist + no catalogue → true; catalogued scope → false; nothing exists →
  false (greenfield, not orphan).
- **`failIfUpdateNeeded` in isolation** (old controller): matched DB → no throw, no DDL applied; drifted DB
  (missing table/column, and — option (i) — missing index/side table) → throws `SchemaUpdateRequired` and
  applies nothing.
- **Adopt Recommended (matched):** simulate an existing DB (create tables via the old controller), drop
  `ef_catalog`, `AdoptExistingScope(V, TrustModel)` → verify passes, catalogue seeded at `V` for every
  table, no DDL; then `UpdateTables(V)` is a clean no-op, `UpdateTables(V+1)` with a real change applies
  only that delta.
- **Adopt Recommended (drifted) → throws:** existing DB missing a column, `AdoptExistingScope(V, TrustModel)`
  → `SchemaUpdateRequired`; catalogue untouched.
- **Adopt Practical:** existing DB **missing a column** the model has, `AdoptExistingScope(V, ReconcileToModel)`
  → the old controller adds the column, the catalogue records the model, and a following `UpdateTables(V)`
  is a no-op (DB now matches).
- **Refuse:** `AdoptExistingScope` on an already-catalogued scope → `CatalogScopeAlreadyAdopted`.
- **Post-adopt patch behaviour** (in-memory SQLite, real patches): adopt at `V`, then `UpdateTables(V')`
  runs only patches in `(V, V']`; a patch ≤ `V` never runs; the orphan guard no longer fires.

## Files

Product:
- `EntityQueries/CreateEntity/CreateEntityController.cs` — `+failIfUpdateNeeded` optional param on
  `UpdateTables`/`UpdateTablesAsync` (verify mode: no structural DDL, throw on drift, ignore views); a
  private verifying `ICreateEntityControllerAction` decorator for the table/column sites, plus flag-guards
  on the index/dynprops reconcile helpers (option (i)).
- `EntityQueries/Catalog/CatalogEntityController.cs` — `+CatalogAdoptMode`, `IsOrphanScopeExists`,
  `AdoptExistingScope`/`…Async`.
- `EfSqlException.cs` — `+SchemaUpdateRequired` (flag throw) and `+CatalogScopeAlreadyAdopted` (re-adopt
  refusal) + messages.

Tests: extend `CatalogEntityControllerTest.cs` (fixture, adopt cases) and `CatalogPatchReplayTest.cs`
(post-adopt patch window); a small `PatchTest`/`CreateEntity` case for `failIfUpdateNeeded` in isolation
(verify passes when matched, throws `SchemaUpdateRequired` when drifted). Possibly a `CatalogAdoptTest.cs`.

## Out of scope (still later Phase 5)

Full compare-with-actual **drift repair** (option (a) introspection→DTO, diffing catalogue vs live DB to
detect/repair divergence), torn-write recovery, and marking the old `CreateEntityController` `[Obsolete]`.
`CreateTables` data-patch seeding remains a separate deferred discussion.
