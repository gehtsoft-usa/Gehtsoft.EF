# Prereq A — schema-tolerant catalogue (de)serialization (PLAN, for Gate)

*Standalone prerequisite for the Schema Catalogue (`../DESIGN.md`). Independently developed and tested,
no DB required. Drafted 2026-07-14; **no code until approved.** Process per [[feedback_phase_process]].*

## Goal

A **versioned, self-describing text format** that records the applied schema of one table, plus a mapper
from the runtime `TableDescriptor` (+ its composite indexes) into that format — such that:

- the on-disk form is **decoupled from the runtime classes** (`TableDescriptor`/`ColumnInfo`/metadata),
  so refactoring those classes never breaks reading an existing catalogue;
- a **newer** reader loads an **older** blob (backward: missing members → documented defaults, plus
  explicit **upgrade-on-read** for deliberate older→current transforms);
- the blob carries the **producing framework's schema-format capability version** so the *consumer*
  (the update process) can enforce the safety gate below.

This is the storage decision from Gate 1 realized as an isolated, unit-testable unit.

## ⚠ Forward-compatibility is NOT tolerance — it is a HARD REFUSAL (design pivot, user 2026-07-14)

An older updater that reads a catalogue written by a **more capable** framework version and then applies
DDL will **damage the live schema** — it reconciles the DB toward its older model and drops columns /
indexes / constructs the newer version legitimately created and recorded but that the old updater has no
concept of. Preserving unknown members does not help: the damage comes from *acting* on partial
understanding. Therefore:

> **The update process MUST refuse to touch the database when `catalogue.version > this.maxSupportedVersion`
> — hard stop, clear error, zero DDL.** No best-effort, no ignore-and-proceed.

Consequences (this pivot simplifies the format):
- **`SchemaFormatVersion`** = a **single monotonic integer**, bumped in code whenever the framework learns
  a new DDL-relevant construct (a new column kind, index kind, attribute). Not the product version.
- No `JsonExtensionData` / preserve-unknowns (it only ever mattered for read-modify-write of a newer
  catalogue, which is now forbidden — dead weight, dropped).
- No major/minor "tolerate additive" tier — for the updater there is no safe *partial* understanding.
- An unknown enum value / unrecognized structure can only originate from a newer writer → caught by the
  same refuse rule; no special enum handling needed.
- The **serializer** (this prereq) only: round-trips known shapes, upgrades older versions on read, and
  exposes `SchemaFormatVersion`. The **refuse gate lives in the update process** (the catalogue consumer),
  not here.

## Format & serializer

- **JSON via `System.Text.Json`** — already a `PackageReference` in `Gehtsoft.EF.Db.SqlDb` (v8.0.5), works
  on netstandard2.0. No new dependency.
- Top-level object carries `schemaFormatVersion` (monotonic int) + the table payload. Stored as plain
  text in the catalogue row (works on every driver; no native-JSON-column dependency).
- **Version rules:**
  - Expose `schemaFormatVersion` from the parsed blob so the consumer can apply the refuse gate (a
    newer blob than this build supports → the update process refuses; see the pivot above). The
    serializer itself may parse-and-expose even a newer version, but must **flag it as newer** so the
    consumer stops.
  - Backward: every DTO member is optional with a defined default; a reader fills defaults for absent
    members, plus an explicit `switch (schemaFormatVersion)` **upgrade-on-read** chain (older→current)
    for deliberate transforms. A dedicated test writes a minimal old-shape JSON and asserts a clean load.
  - No forward tolerance / ignore-unknown-and-proceed (see pivot). For determinism, unknown members are
    not silently dropped-and-rewritten; the consumer refuses before any rewrite happens.

## The DTO (catalogue format v1) — a hand-owned model, NOT the runtime classes

One `CatalogTableDto` per table with only DDL-relevant state (no `PropertyAccessor`, no `Table`
back-ref, no arbitrary `Metadata` object graph):

- table: `name`, `scope`, `view` (bool), `obsolete` (bool)
- columns[]: `id`, `name`, `dbType` (**serialized as the enum NAME string**, not its int — immune to enum
  reordering), `size`, `precision`, `primaryKey`, `autoincrement`, `sorted`, `unique`, `nullable`,
  `foreignTable` (name or null), `ignoreRead`, `defaultValue` (see risk RS1)
  - `geometry` (nullable): `srid`, `subtype` (enum name), `hasZ`, `hasM`, `nullable`, `indexes[]`
    (name, xmin/ymin/xmax/ymax, tolerance) — from `GeometryColumnMetadata` / `SpatialIndexDefinition`
  - `json` (nullable): serialized CLR type name + `indexes[]` (name, path, dbType-name) — from
    `JsonColumnMetadata` / `JsonIndexDefinition`
- compositeIndexes[]: name, `excludeFor[]`, fields[] (`name`, `function` enum-name or null, `direction`,
  `jsonPath`/`jsonType` or null) — extracted from `ICompositeIndexMetadata` at map time

## API surface (this prereq owns)

New folder `Gehtsoft.EF.Db.SqlDb/Catalog/` (netstandard2.0 → explicit `<Compile Include>` each file):

- `CatalogTableDto` (+ nested column/index DTOs) — POCOs with `System.Text.Json` attributes.
- `CatalogSnapshot` — the versioned envelope (`SchemaFormatVersion` + `CatalogTableDto`).
- `CatalogSerializer`:
  - `string Serialize(CatalogSnapshot)` (always writes this build's current `SchemaFormatVersion`).
  - `CatalogSnapshot Deserialize(string)` — applies upgrade-on-read for older versions; for a **newer**
    version, returns a snapshot whose `SchemaFormatVersion` the consumer checks (or a dedicated
    `IsNewerThanSupported` flag) so the update process can refuse. Never partially applies a newer shape.
  - `CatalogTableDto FromDescriptor(TableDescriptor table, IReadOnlyList<CompositeIndex> compositeIndexes)`.
  - `int CurrentSchemaFormatVersion { get; }` — this build's max supported version (the refuse-gate input).
- The catalogue **diff engine (catalogue Phase 2) consumes `CatalogTableDto`** — both the desired side
  (mapped from the model via `FromDescriptor`) and the stored side (deserialized) are the *same* DTO, so
  the diff is DTO-vs-DTO. (No need for a DTO→`TableDescriptor` reverse map in v1; DDL is emitted from the
  live model, not reconstructed from the catalogue. Flag if a reverse map is later needed for repair.)

## Testing (pure, no DB — a SEPARATE test set)

New namespace `Gehtsoft.EF.Test.Catalog.Serialization` (default-globbed test csproj):

- Round-trip: build a `TableDescriptor` for every column flavour (plain of each DbType, PK, autoincrement,
  sorted, unique, nullable, FK, default value, geometry with/without Z/M + spatial indexes, JSON with
  indexes, composite indexes incl. functional/JSON/excludeFor) → `FromDescriptor` → `Serialize` →
  `Deserialize` → assert deep field equality.
- **Forward tolerance:** hand-written JSON with an extra unknown member deserializes cleanly.
- **Backward tolerance:** hand-written minimal old-shape JSON (missing newer members) → defaults applied.
- **Version stamp:** `formatVersion` present; an unknown/newer version handled per policy (reject vs
  best-effort — decide in plan review).
- Enum-by-name: a `dbType`/`subtype` written as name still loads (and would survive an enum reorder).

## Risks / to confirm

- **RS1 — `ColumnInfo.DefaultValue` is `object`. DECIDED:** store `{ typeName, invariantStringValue }`
  (type-tag + invariant-culture string); document the supported primitive set.
- **RS2 — JSON column CLR type name.** Storing an assembly-qualified type risks breaking on assembly
  moves; store the framework's own serialized form (as `JsonColumnMetadata` already keeps it) and treat
  it as opaque text for diffing.
- **RS3 — member ordering / determinism.** Serialize with stable member order so two equal descriptors
  produce byte-identical JSON (helps diffing and test assertions). Configure `System.Text.Json` for
  deterministic output.
- **RS4 — what counts as a change.** The diff (Phase 2, not here) defines equality; this prereq only must
  preserve every field faithfully. Keep the DTO lossless w.r.t. DDL-relevant state.

## Constraints

netstandard2.0 explicit `<Compile Include>`; no LINQ; `ArgumentNullException.ThrowIfNull(x, nameof(x))`;
never touch `version.proj`; tests assert intended behaviour (product bugs → `KNOWN_ISSUES.md`).
