# Schema Catalogue — overall design (DRAFT for Gate 1)

*Drafted 2026-07-14. Status: **overall plan, not yet approved; no code.** Process mirrors EAV/JSON/geo:
this overall plan is approved first (Gate 1), then each phase is planned in a `PHASE_N/` doc and approved
before coding, and advancing between phases is a second gate. Decision to build this **before** geo
Phase 3 ("catalogue first, geo rides it") was made by the user 2026-07-14; geo is parked at `533f8b6`.*

## Motivation

`CreateEntityController.UpdateTables` decides what to create/alter/drop by **introspecting the live
database** (`Schema()`, `GetTableIndexes()`). That is structurally lossy:

- **Declared intent is unreadable.** MSSQL `BOUNDING_BOX`, Oracle spatial tolerance, SRID, a JSON
  index's path/type, a column's original size/precision — introspection can't reliably read these back
  in a form comparable to the declaration, so "**did the declaration change?**" is largely
  unanswerable. Today only add/drop is detected, never a parameter change.
- **Every driver's system catalog is different.** The geo spatial-index case is the trigger: spatial
  indexes live in `sys.spatial_indexes` (MSSQL), a virtual R-tree invisible to `PRAGMA index_list`
  (SpatiaLite), `USER_INDEXES.ITYP_NAME` (Oracle), normal catalogs (PostGIS/MySQL). That is five
  bespoke introspection paths *per feature*.

A **catalogue** — EF-owned tables recording the schema *as EF declared and applied it* — replaces
"read the DB and guess" with "compare the model to what we recorded." One driver-agnostic model;
change-detection becomes possible; per-driver introspection quirks stop multiplying.

**Precedent:** EF already owns and manages a bookkeeping table via the normal entity machinery —
`EfPatchHistoryRecord` → `[Entity(Table="ef_patch_history", Scope="ef_patches")]`. The catalogue is the
same pattern, generalized.

## Goals

- A driver-agnostic record of the EF-managed schema (tables → columns → indexes, **with declared
  parameters**), maintained by `UpdateTables`.
- Diff the current entity model against the catalogue; apply the minimal DDL; update the catalogue.
- Detect **changes** introspection can't (parameter/type/index changes), not just add/drop.
- Uniformly solve geo (and JSON, and composite indexes) reconciliation with no per-driver catalog reads.
- Backward compatible for the installed base (existing databases with no catalogue must not break).

## Non-goals (v1)

- Full migration framework / down-migrations / data migrations (that is what `Patch` is for; the
  catalogue complements it, does not replace it).
- Detecting/repairing **out-of-band drift** (user or another tool dropped a table/index). Accepted loss
  vs the introspection approach; a later "verify/repair" tool re-seeds from introspection. *(User
  accepted this trade explicitly.)*
- Renames (treated as drop+add in v1 unless a phase decides otherwise).

## Authority model — HYBRID (catalogue-authoritative + introspection seed/repair)

> **REVISED 2026-07-16 (user) — v1 is greenfield, no adopt.** The three-way branch below was the
> original Gate-1 sketch. It is superseded: v1 does **not** seed from the model on first contact. A
> trust-the-model adopt is only correct when the catalogue switch is a release's *sole* change (≈never);
> when the switch ships alongside model changes it would stamp the new model as applied and **silently
> skip that release's DDL**. And a *correct* adopt of an existing DB needs the DB's actual state, i.e.
> introspection = **Phase 5 (compare-with-actual)**. So in v1: catalogue entry → diff vs catalogue;
> **no entry → `stored` is `null` → full CreateTable** (the catalogue is born with the DB). Introspection
> is not the v1 seed path; it returns only with Phase 5. The italicised *adopt* bullet below is retained
> for history but does not describe v1.

The catalogue is authoritative **once populated**. Because the installed base has no catalogue, first
contact with a table falls back to introspection **once** to seed the catalogue, then the catalogue is
used thereafter:

- **Table has a catalogue entry** → diff model vs catalogue; apply DDL; rewrite entry. (Introspection
  not consulted.)
- **No catalogue entry, table exists in DB** → *adopt*: seed the entry from the current model treated as
  already-applied (optionally cross-checked against a light introspection), record it, apply only what
  the model adds beyond it. This is the upgrade path for existing deployments. *(v1: dropped — see the
  revision note above; this is Phase 5.)*
- **No catalogue entry, table absent** → create table; record entry.

This keeps introspection alive as the **seed + (future) repair** path exactly as the user framed it —
it is not deleted, it is demoted. *(v1 revision: the seed use moves to Phase 5; v1 keeps only the
create-from-null path.)*

## What the catalogue stores — DECIDED (Gate 1): serialized snapshot, standalone versioned format

A stable serialization of the applied schema (columns + their DbType/size/nullability/FK, geometry
metadata, spatial/JSON/composite indexes with all parameters), stored as **an append-only history of
schema versions** (see Migration model below) — each version stamped with app revision, schema-format
version and a `migrated` flag. Diff happens in C#. Driver-agnostic, we own the diff; the model is small
enough to load/parse/diff entirely in memory. Stored as **plain text** (works on every driver; no
native-JSON dependency), in the reserved scope `ef_catalog`, self-bootstrapping (created on first use
like `ef_patch_history`).

**HARD RULE (the trap to avoid):** the snapshot is a **dedicated, versioned wire format decoupled from
the runtime classes** — NOT a dump of the live `TableDescriptor`/`ColumnInfo` object graph. Serializing
the live objects would weld the on-disk format to internal class shapes, so any later refactor of those
classes (geo/JSON descriptor channels *will* change) breaks deserialization of old catalogues — the
opposite of the resilience we want. So: an explicit "catalogue format v1" DTO + an in-memory mapper
to/from `TableDescriptor`, an explicit format-version stamp, and readers retained for older versions.

**Known, accepted losses vs normalized rows (N):** (a) the catalogue is not SQL-queryable/inspectable
by non-EF tools (a blob needs EF's deserializer); (b) cross-table reconciliation (FK targets in other
tables' entries) must load multiple snapshots — fine, model is small; (c) the snapshot records EF's
**declared intent**, not the DB's **realized** state (DBs silently normalize precision/collation/
auto-indexes) — harmless on the trust-model path (stable model ⇒ matches ⇒ no-op) but the optional
compare-with-actual route (below) must normalize for per-driver behaviour or it reports phantom diffs.
(N) — normalized `ef_catalog_table/column/index` rows — is revisitable if a SQL-queryable catalogue is
later needed; not v1.

## Migration model — schema-version history + `migrated` flag + coded migrations (user, 2026-07-14)

The catalogue is an **append-only history of schema versions**, not just a current-state row. Each version
carries a **`migrated` flag** ("has the DB been brought to this schema version"). This unifies bookkeeping
and gives crash recovery for free:

- **`migrated` flag = torn-write recovery.** Write the target version row `migrated=false` → apply DDL →
  set `migrated=true`. Because DDL auto-commits on Oracle/MySQL (can't wrap DDL+record in one txn), a crash
  leaves the latest row unmigrated; the next run *resumes*, idempotent via re-diff against the last
  `migrated=true` snapshot. The flag is the recovery signal we needed.
- **Version identity = app-supplied revision** (like today's `[EfPatch]` major.minor.patch), the shared
  ordering key.
- **Forward-refuse gate still applies:** latest stored version produced by a framework newer than the one
  running → refuse (see Prereq A pivot).
- Minimum for correctness = last-migrated snapshot + in-progress target; full history is additive value
  (audit, diff-any-two-versions, repair).

**Coded migrations (`IEfPatch`) stay a SEPARATE ledger, integrated at the CONTROLLER, not the schema
table** — they have different application semantics:
- **Structural schema = state convergence** (diff current-DB vs target snapshot; order-independent; jump
  straight to latest). The catalogue.
- **Coded `IEfPatch` = imperative replay** (path-dependent data transforms, run in order, once each). An
  evolved `ef_patch_history`-style ledger, NOT dissolved into the snapshot.
- The new `CatalogEntityController` orchestrates both, ordered by app revision; the exact interleaving
  (coded steps before/after structural convergence at a revision) is pinned in the catalogue phase plan.
- **`ef_patch_history` is superseded/rethought**, but the `IEfPatch.Apply(connection)` capability is
  retained and wired into the new controller.

## Apply algorithm (per table, inside UpdateTables)

1. Resolve current model → `TableDescriptor` (as today).
2. Load catalogue entry (or adopt/seed per the authority model).
3. Diff → an ordered change list: add/drop/alter column, add/drop/alter index (incl. spatial & JSON),
   table create/drop.
4. Emit DDL through the existing builders (`CreateTableBuilder`, `AlterTableQueryBuilder`,
   `CreateIndexBuilder`/spatial builders, `DropIndexBuilder`).
5. Record the new applied state in the catalogue.

## Transactionality (must design, not hand-wave)

DDL **auto-commits on Oracle and MySQL**, so DDL + catalogue-write cannot be one atomic transaction.
Rule: **apply the DDL step, then record it**; keep every step **idempotent**; on a torn write (crash
between DDL and record) the next run re-adopts from introspection and converges. Where the driver *does*
support transactional DDL (Postgres, MSSQL), wrap for cleanliness but do not depend on it.

## Locking — DB-instance mutex (DECIDED direction, Gate 1)

Something the current controller lacks entirely. Two apps running an update against the same DB must
serialize. **Driving constraint:** because DDL auto-commits on Oracle/MySQL, the lock must be
**session/advisory-scoped, held across the whole read→diff→apply**, NOT transaction-scoped (a txn lock
cannot span auto-committing DDL). "Read-and-lock as one" = acquire, then read the catalogue, but the
*hold* outlives any single transaction.

Abstraction **`IDbInstanceLock`** with:
- **Native advisory lock per driver** (preferred; most auto-release on disconnect → free crash-recovery):
  PostgreSQL `pg_advisory_lock`/`pg_try_advisory_lock` (session); MSSQL `sp_getapplock`/`sp_releaseapplock`
  (`@LockOwner='Session'`); MySQL `GET_LOCK`/`RELEASE_LOCK`; Oracle `DBMS_LOCK.REQUEST` (needs EXECUTE
  grant).
- **Portable lock-table lease fallback** for engines with no native advisory lock (SQLite; Oracle when
  `DBMS_LOCK` is not granted): a single-row `ef_catalog_lock` acquired by a conditional `UPDATE`, **with
  an expiry/lease** so a crashed holder does not wedge everyone permanently (native locks don't need this;
  the table lease does).
- `try`-acquire with a timeout; a clear `EfSqlException` on contention.

Decisions: **instance-wide** mutex for v1 (fully serializes any two updaters; per-scope later if needed).
Define `IDbInstanceLock` + `ef_catalog_lock` in Phase 1 (the catalogue needs the table anyway);
implement the native per-driver locks in a **dedicated phase**, documenting single-writer until then.

## Backward compatibility / rollout — DECIDED (Gate 1): a new class, obsolete the old one

No feature flag. Ship a **new controller `CatalogEntityController`** mirroring the public surface of the
existing `CreateEntityController` (constructors from assemblies/type + scope; `OnAction` event;
`CreateTables`/`DropTables`/`UpdateTables` + async; `UpdateMode`). It creates the catalogue the moment
it is first used. `CreateEntityController` (the current introspection-based class — note: there is no
class literally named "UpdateManager") is marked **`[Obsolete("use CatalogEntityController; to be
removed")]`**. Both coexist through the transition; the choice is which class you instantiate.
`Recreate` / obsolete-drop semantics preserved.

## Compare-with-actual — DECIDED (Gate 1): optional later-phase route, default is trust-model

Default apply path trusts the catalogue (trust-model). A separate, opt-in route introspects the live DB
and reconciles **catalogue vs actual** — the drift-repair tool. It is a distinct later phase of the new
controller, and must normalize for per-driver realized-vs-declared differences (see storage loss (c)).

## Integration points to migrate

`CreateEntityController.UpdateTables`, `ReconcileIndexes` / `ComputeDesiredIndexes` (plain + FK +
composite + JSON today), `ReconcileDynamicPropertiesTable`, `AddColumns`/`DropColumns`. The
just-landed index-reconciliation fix (introspection-based) is **subsumed** by the catalogue diff —
plan the cutover so behaviour is preserved and tests stay green.

## Proposed phasing (each phase = its own plan + gate)

Two pieces are **standalone prerequisites** — independently developed and tested, ahead of / in parallel
with the catalogue core (as the index-reconciliation fix was pulled out as shared work):

- **Prereq A — schema-tolerant (de)serialization** *(this folder, `PREREQ_SERIALIZATION/`)*. The
  standalone versioned wire format + a mapper to/from `TableDescriptor`, decoupled from runtime classes.
  **Pure in-memory tests, no DB:** round-trip every descriptor shape; a newer reader loads an older
  hand-written blob; adding a model field does not break old blobs (forward/backward tolerance).
- **Prereq B — DB-instance lock** *(own top-level initiative `CLAUDE/DB_INSTANCE_LOCK/`)*. `IDbInstanceLock`:
  native advisory lock per driver + `ef_catalog_lock` lease fallback. **Tested independently:** acquire /
  contended try-acquire timeout / release / session-drop auto-release / lease expiry. Reusable framework
  capability, not catalogue-specific.

- **Phase 1 — Catalogue store + authority model.** `ef_catalog` entities/tables, self-bootstrap; consumes
  Prereq A for the stored form and Prereq B for the update mutex (both already built + tested);
  load/adopt/seed. Deep tests: adopt an existing table; catalogue written on first use.
- **Phase 2 — Diff engine.** Model-vs-catalogue diff producing the change list, unit-tested in
  isolation (add/drop/alter column; add/drop index; parameter change; no-op). No DDL wiring yet.
- **Phase 3 — `CatalogEntityController`.** The new class (mirrors `CreateEntityController`) driving
  create/update via the diff for tables/columns/plain+composite+JSON indexes; obsolete the old class.
  Full existing suite green on all drivers (parity with the introspection path).
- **Phase 4 — Instance mutex.** Native per-driver advisory locks + the lock-table lease fallback behind
  `IDbInstanceLock`; acquire around the whole update. (Single-writer documented until this lands.)
- **Phase 5 — Compare-with-actual / repair.** The optional introspect-and-reconcile-catalogue-vs-actual
  route (drift repair), normalizing for per-driver realized-vs-declared differences.
- **(Then) geo Phase 3** rides the catalogue: geo column add/**drop** (user wants drop) + spatial-index
  add/drop become plain diff entries — no `GetTableIndexes` spatial surfacing, no per-driver spatial
  catalog reads.

## Risks

- **RC1 — adopt correctness.** Seeding an existing table from the *model* assumes the DB matches the
  model. If it doesn't (historical drift), the first diff may be wrong. Mitigate with an optional
  introspection cross-check during adopt, and document.
- **RC2 — torn DDL/record writes** on auto-commit drivers (see Transactionality). Idempotency + re-adopt.
- **RC3 — serialization format evolution.** Must version the blob and tolerate reading older formats.
- **RC4 — scope/naming collisions.** Reserve the `ef_catalog` scope; never touch user tables.
- **RC5 — parity regression.** Cutover must keep every current UpdateTables test green; the flag lets us
  A/B the two engines during transition.
- **RC6 — multi-process / concurrent updates.** Two apps updating the same DB. Addressed by the
  instance mutex (Phase 4); single-writer documented until then.

## Gate 1 decisions — RESOLVED (user, 2026-07-14)

1. **Storage** = **(S) serialized snapshot**, as a **standalone versioned format decoupled from runtime
   classes** (not a live-object dump). Accepted losses documented above. (N) revisitable later.
2. **Rollout** = **new class `CatalogEntityController`**, old `CreateEntityController` marked `[Obsolete]`
   "to be removed"; catalogue self-created on first use. No feature flag.
3. **Adopt / compare** = default **trust-model**; **compare-with-actual is an optional later phase**
   (Phase 5, drift repair).
4. **Concurrency** = **instance-wide `IDbInstanceLock`**, native advisory lock per driver + lock-table
   lease fallback; its own phase (Phase 4). Session/advisory-scoped (must span auto-committing DDL).
5. **Naming** = confirmed: docs `CLAUDE/SCHEMA_CATALOGUE/`, scope `ef_catalog`.

**Gate 1 = APPROVED pending user sign-off on this resolved version. Next: plan Phase 1 in `PHASE_1/`.**
