# Phase 1 — Declare (entity-level) (plan, REWRITTEN 2026-07-09)

*Rewritten for the `byte[]`/WKB core + pluggable `IGeometryCodec` architecture (see `../GEO_PLAN.md`
banner; Phase 0 delivered the abstraction + the NTS module). **Gate 2** applies: approved before coding;
advancing to Phase 2 is a separate gate. Phase 1 is declare-only — no SQL type emission, no index DDL,
no value binding at the query layer, no queries. Deep tests only, no DB.*

## Goal

Let a developer mark an entity member as a geometry column and declare spatial indexes on it, and have
entity discovery turn that into a `TableDescriptor.ColumnInfo` that carries a **geometry descriptor**
(subtype + SRID + Z/M + nullability + the declared spatial-index list). Two property shapes are
supported:

- **`byte[]` (WKB) property** — the zero-config core path. No codec, no accessor: the column is a
  `byte[]` value; the existing binder handles it.
- **Object property** (e.g. an NTS `Geometry`) — a decorating accessor presents it to the framework as
  `byte[]` (WKB), delegating object↔WKB conversion to the registered `IGeometryCodec`.

Everything geometry-object stays in the application + the codec; the framework stores WKB.

## Scope

**In:** the `[GeometryProperty]` / `[SpatialIndex]` attributes; the `GeometryColumnMetadata` /
`SpatialIndexDefinition` descriptor; the `ColumnInfo.Geometry` channel; the `ColumnDiscoverer`
interception (both property shapes + guardrail); the object-path `GeometryPropertyAccessor`; deep tests.

**Out (later phases):** per-driver `GeometryTypeName` + `SupportsGeometry` gate + column DDL (Phase 2);
`CompositeIndex.ForSpatial` + spatial-index DDL/reconcile + MySQL NOT-NULL-when-indexed (Phase 2/3);
insert/update WKB value-wrapping (`ST_GeomFromWKB`) and select `ST_AsBinary` read + spatial query
functions + the **per-connection codec override** (Phase 4+, where a connection is in scope). Until
Phase 2, a geometry column is a `DbType.Binary` placeholder.

## Public API (interfaces + responsibility)

### Entities project (`Gehtsoft.EF.Entities`) — library-agnostic, unchanged by the codec pivot

| Type | Kind | Responsibility |
|---|---|---|
| `GeometryPropertyAttribute` (`[GeometryProperty]`) | `[AttributeUsage(Property)]` | Marks a property (of type `byte[]` **or** an object the codec handles) as a geometry column. Props: `string Field` (column name; null → naming policy), `int Srid = 4326`, `GeometrySubtype Subtype = GeometrySubtype.Geometry`, `bool HasZ`, `bool HasM`, `bool Nullable`. *(Naming = sub-decision D1; dims = D2.)* |
| `SpatialIndexAttribute` (`[SpatialIndex]`) | `[AttributeUsage(Property, AllowMultiple = true)]` | A declared spatial index (repeatable). Props: `string Name` (null → derived), bounding box `double MinX/MinY/MaxX/MaxY = NaN` (`HasBoundingBox` derived), `double Tolerance = 0.005`. Convenience ctor `(minX, minY, maxX, maxY)`. |
| `GeometrySubtype` | enum | `Geometry = 0` (generic/any) + `Point = 1 … GeometryCollection = 7`. (Nullable enum isn't a legal attribute argument, hence the `Geometry = 0` sentinel.) In `Geometry/GeometrySubtype.cs`. |

Files under `Attributes/` + `Geometry/`; namespace `Gehtsoft.EF.Entities` (attributes) / `…​.Geometry`
(enum). Each needs a `<Compile Include>` in `Gehtsoft.EF.Entities.csproj`.

### Db.SqlDb project (`Gehtsoft.EF.Db.SqlDb`)

| Type | Kind | Responsibility |
|---|---|---|
| `GeometryColumnMetadata` | class (`…​.Metadata`) | Descriptor attached to `ColumnInfo`. Carries `Type ClrType` (the property type), `int Srid`, `GeometrySubtype Subtype`, `bool HasZ`, `bool HasM`, `bool Nullable`, `IReadOnlyList<SpatialIndexDefinition> Indexes`. Mirrors `JsonColumnMetadata`. |
| `SpatialIndexDefinition` | class (`…​.Metadata`) | One resolved spatial index: derived `string Name`, `bool HasBoundingBox` + `double MinX/MinY/MaxX/MaxY`, `double Tolerance`. Mirrors `JsonIndexDefinition`. |
| `GeometryPropertyAccessor` | `IPropertyAccessor` (`…​.QueryBuilder`) | **Object-path only.** Decorates the inner accessor. `PropertyType` ⇒ `typeof(byte[])`. `GetValue` = inner object → `codec.ToWkb(obj, includeSrid: false)` (null ⇒ null). `SetValue` = `byte[]` → `codec.FromWkb(bytes, srid)` → inner (null ⇒ null). Ctor `(IPropertyAccessor inner, int srid)`; resolves the codec **lazily** via `GeometryCodecs.Resolve()` at conversion time (so registration order is forgiving and a future per-connection override can slot in). `Name`/attribute methods delegate. Mirrors `JsonPropertyAccessor`. |
| `ColumnInfo.Geometry` (new property) | edit `QueryBuilder/TableDescriptor.cs` | `public Metadata.GeometryColumnMetadata Geometry { get; internal set; }` — sibling to `Json`; null for non-geo columns; does not affect `FullName`-based equality. |
| geo branch + `CreateGeometryColumnDescriptor` | edit `ColumnDiscoverer.cs` | Sibling early-return after the JSON branch. Resolve column name; read repeatable `GetCustomAttributes<SpatialIndexAttribute>()` → `List<SpatialIndexDefinition>` (derived names via a `DeriveSpatialIndexName(column, ordinal)` helper). Then **branch on the property type** (see below). Add one `ColumnInfo { DbType = DbType.Binary, Nullable = geo.Nullable, PropertyAccessor = <chosen>, Geometry = new GeometryColumnMetadata(...) }`. |

**Property-shape branch (guardrail) in `CreateGeometryColumnDescriptor`:**
- `propertyType == typeof(byte[])` → **byte[] path:** keep the default `propertyAccessor` (no decoration).
- otherwise → **object path:** `IGeometryCodec codec = GeometryCodecs.Resolve();` (throws a clear error
  if none registered → "reference Gehtsoft.EF.Geo.NetTopologySuite / register a codec, or use a byte[]
  property"); if `!codec.CanHandle(propertyType)` throw a clear `EfSqlException`; else
  `PropertyAccessor = new GeometryPropertyAccessor(propertyAccessor, geo.Srid)`.

Each new Db.SqlDb `.cs` needs a `<Compile Include>` (confirm `EnableDefaultCompileItems=false` on
`Gehtsoft.EF.Db.SqlDb.csproj`).

## Codec resolution & the per-connection override

The decorating accessor is created once and cached in the shared `TableDescriptor`; its
`GetValue`/`SetValue` take no connection, so it uses the **global** `GeometryCodecs.Resolve()`. The
**per-connection override** the design calls for is therefore honored at the **query layer** (Phase 4+),
where a `SqlDbConnection` is in scope and can pass `connection.GeometryCodecFactory ?? GeometryCodecs.Factory`
for query-parameter/value conversion — not at the entity accessor. Phase 1 leaves the global path in
place. *(Sub-decision D5: accept this split, or make entity object-property conversion connection-aware
now — a broader accessor-signature change.)*

## Where it is implemented

New product files:
```
Gehtsoft.EF.Entities/Attributes/GeometryPropertyAttribute.cs
Gehtsoft.EF.Entities/Attributes/SpatialIndexAttribute.cs
Gehtsoft.EF.Entities/Geometry/GeometrySubtype.cs
Gehtsoft.EF.Db.SqlDb/Metadata/GeometryColumnMetadata.cs
Gehtsoft.EF.Db.SqlDb/Metadata/SpatialIndexDefinition.cs
Gehtsoft.EF.Db.SqlDb/QueryBuilder/GeometryPropertyAccessor.cs
```
Edited: `Gehtsoft.EF.Entities.csproj` (+3 Compile), `Gehtsoft.EF.Db.SqlDb.csproj` (+3 Compile),
`QueryBuilder/TableDescriptor.cs` (`ColumnInfo.Geometry`),
`EntityQueries/EntityDiscovery/ColumnDiscoverer.cs` (geo branch + method + name helper).

## How it is tested (deep tier only — no DB)

Test folder `Gehtsoft.EF.Test/Geo/Entities/`, namespace `Gehtsoft.EF.Test.Geo.Entities` (xUnit v3 +
AwesomeAssertions, default-globbed). Discovery through the same entry point the JSON declare tests use
(confirm from `Gehtsoft.EF.Test/JsonProperties/`). The NTS codec is registered (`NtsGeometry.Register()`)
for the object-path tests.

- **`GeometryDeclareTest`** — discover entities with `[GeometryProperty]` on a **`byte[]`** property
  (varying SRID incl. the 4326 default, `Subtype`, `HasZ`/`HasM`, `Nullable`, `Field` vs naming policy):
  assert the `ColumnInfo` is `DbType.Binary`, has the expected `Geometry` metadata, and keeps the default
  accessor (no decoration).
- **`GeometryObjectPropertyTest`** — discover an entity with `[GeometryProperty]` on an **NTS `Geometry`**
  property: a `GeometryPropertyAccessor` is installed with `PropertyType == typeof(byte[])`; `GetValue`
  returns `codec.ToWkb(pt, false)`; `SetValue(bytes)` sets an equal NTS geometry (round-trip, incl. a Z/M
  geometry); null ⇒ null both ways; the declared SRID is applied on read.
- **`SpatialIndexDeclareTest`** — multiple `[SpatialIndex]` on one property → expected
  `SpatialIndexDefinition` list (names distinct/stable; bbox present/absent via `HasBoundingBox`;
  tolerance incl. default).
- **`GeometryDiscoveryGuardrailTest`** — `[GeometryProperty]` on an object type with no registered codec
  (or a codec that can't handle it) throws a clear error; on a `byte[]` property it succeeds with no codec
  registered.

No DB / DDL / query.

## Sub-decisions to confirm at Gate 2

- **D1 — attribute name**: `[GeometryProperty]` (recommended) vs `[GeometryEntityProperty]`.
- **D2 — dimensionality**: `HasZ`/`HasM` bools (recommended) vs a `GeometryOrdinates` enum.
- **D3 — subtype default**: `GeometrySubtype.Geometry` (any) default (recommended) vs required subtype.
- **D4 — bbox/tolerance on `[SpatialIndex]`**: NaN-defaulted doubles + `Tolerance = 0.005` (recommended).
- **D5 — per-connection codec override**: defer to the query layer, entity accessor uses the global codec
  (recommended) vs make entity object-property conversion connection-aware now.

## Acceptance criteria

- New files compile in both projects; `<Compile Include>` added; solution builds clean (no new warnings).
- byte[]-path and object-path discovery both yield the geometry descriptor; object path installs the
  codec-backed accessor; guardrail fires as specified.
- All Phase-1 deep tests green; no DB/DDL/query code (Phase 2+); `version.proj` untouched; no commit
  unless asked.
