# Increment 6 — the parity gate (as-built)

*Branch `geo`, uncommitted, 2026-07-19. Authoritative state: `STATE.md`. This file is the detail record for
the parity gate, the last increment of Phase 3.*

## Goal

Prove that `CatalogEntityController`, running its **real** DDL action (not the mock the other controller
tests use), produces a **physically identical schema** to the proven introspection-based
`CreateEntityController` for every scenario both controllers support. This is the gate that lets geo ride the
new controller with confidence.

## Method

`Gehtsoft.EF.Test/Catalog/Controller/CatalogParityTest.cs` — live all-driver via `SqlConnectionFixtureBase`.
For each scenario, on one connection:

1. Run the scenario via the **old** controller; take a **schema fingerprint**.
2. Reset the physical tables.
3. Run the scenario via the **new** controller (real action); take the fingerprint again.
4. Assert the two fingerprints are equal, plus a behavioural insert/select round-trip on the result.

The **fingerprint** is a `SortedDictionary<string,bool>` built by enumerating the model
(`EntityFinder.FindEntities`, include-obsolete) and probing `DoesObjectExist` for every table, column,
declared index (single-column by column name + composite by metadata name), view, and dynamic-property side
table — so both controllers are probed identically. Equal fingerprints = physical parity; no name
normalization needed because both runs use the same table names on the same connection.

**Create-from-scratch** scenarios share one scope (both build from nothing). **Evolution** scenarios can't be
reached with a second model on the same connection (the catalogue reconciles against its own stored state,
not the live DB), so they: build physical V1 via the old controller, **seed the catalogue with the V1
snapshot under the target (V2) scope** (`CatalogStore.WriteApplied`, DTO built from the V1 descriptor with
its scope overridden), then run `new.UpdateTables("2.0.0")` and compare against the old controller's
incremental (old(V1) → old(V2)) result. V1/V2 share table names.

## Scenarios (9 × 5 drivers = 45 green)

- **Create_RichModel_Parity** — FK-ordered `par_dict` + `par_main` (sorted index + composite index) + view.
- **CreateTables_RichModel_Parity** — the unconditional-create entry point (tables only; views covered above).
- **Recreate_RichModel_Parity** — `Recreate` mode from scratch.
- **Create_DynamicProperties_Parity** — owner + EAV side table.
- **Evolve_AddColumn_Parity**, **Evolve_DropObsoleteProperty_Parity**, **Evolve_DropObsoleteEntity_Parity**,
  **Evolve_AddIndex_Parity**, **Evolve_DropIndex_Parity**.

## Two real product divergences found + fixed (user-decided: match the old controller)

1. **`CatalogEntityController.CreateTables` skipped views.** The old `CreateTables` materializes views; the
   catalogue one did `if (info.View) continue;`. **Fix:** a views loop now runs after the tables (views are
   not catalogued, mirroring `UpdateTables`), making `CreateTables` a true drop-in replacement.
2. **`DropColumn` threw on drivers without `DropColumnSupported` (SQLite).** The old controller guards column
   drops on `DropColumnSupported` and silently leaves the column; the catalogue called the ALTER builder
   unconditionally → `EfSqlException: Requested feature isn't supported`. **Fix:** `ApplyChanges` guards the
   `dropColumns` block on `DropColumnSupported`; when unsupported the column lingers (a *safe* fallback,
   unlike a column **alter** which stays refused because its only fallback is destructive), the desired
   snapshot is still recorded (no future retry), and the `OnEntityPropertyDrop` hook is skipped. The two mock
   drop-column tests in `CatalogEntityControllerTest` now branch on `DropColumnSupported`.

## Intended divergences EXCLUDED from parity (documented in the test file)

The old controller silently no-ops these; the catalogue refuses loudly (surfacing what the old path drops):

- **Column definition change** → `CatalogColumnAlterNotSupported` (no portable in-place modify; route to a
  patch). Old `ReconcileIndexes`/update has no in-place column modify.
- **Unique single-column index change** → `NotSupportedException`. This is genuinely **new capability**, not
  a regression: the old `ReconcileIndexes` ignores unique/PK indexes entirely, so it never adds/drops them.

## Follow-ups (unchanged, still deferred)

Post-parity geo (geometry + spatial index) exclusively in the new controller; unique-single-column index
reconcile; dedicated composite/JSON index-application behavioural tests; Phase-5 compare-with-actual drift
repair, torn-write recovery, `[Obsolete]` on the old controller, `CreateTables` patch-seeding.
