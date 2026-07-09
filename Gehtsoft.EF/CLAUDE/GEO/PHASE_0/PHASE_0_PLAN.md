# Phase 0 — Foundation: geometry CLR type + WKT/WKB codec (plan, pre-approval)

*Phase plan for the geo feature (`../GEO_PLAN.md`, approved 2026-07-09). **Gate 2:** this document is
reviewed and approved before any code is written; advancing to Phase 1 is a separate gate. Phase 0 is
**pure .NET, no DB dependency** — it lands entirely in `Gehtsoft.EF.Entities` and is exhaustively
unit-tested with no database.*

## Goal

Deliver the in-house, DB-independent 2-D geometry value type (the seven OGC subtypes, carrying an
integer SRID) and a lossless WKT reader/writer + WKB reader/writer. This is the CLR representation
every later phase round-trips: WKB is the DB wire form (bound as `byte[]`, wrapped per driver in
`ST_GeomFromWKB`/`STGeomFromWKB`/`SDO_UTIL.FROM_WKBGEOMETRY`, read back via `ST_AsBinary`/…); WKT is
retained for debugging, `ToString()`, and human-readable test vectors.

Nothing in this phase references `Gehtsoft.EF.Db.*`, attributes, accessors, or any driver.

## Scope

**In:**
- The geometry type hierarchy (7 OGC subtypes + abstract base), a coordinate value, a subtype enum.
- WKT reader + writer (OGC Simple Features text, incl. `… EMPTY`).
- WKB reader + writer (standard OGC WKB; both byte orders on read, little-endian on write).
- A dedicated format-error exception.
- Exhaustive pure-.NET unit tests (xUnit v3 + AwesomeAssertions), no DB.

**Out (later phases / feature scope):**
- Any attribute, accessor, descriptor, driver, or SQL (Phases 1–7).
- EWKB (PostGIS SRID-prefixed) and ISO WKB Z/M variants — **not produced**; the DB read path uses
  plain `ST_AsBinary`-style OGC WKB. Reader tolerance to EWKB is an explicit sub-decision (below),
  defaulting to *reject with a clear error*.
- Geoprocessing, validity checking, geometry algorithms (area/length/predicates are computed by the
  DB, not the CLR type). The CLR type is a **carrier + codec only** — it does not compute area,
  test topology, or validate ring orientation/closure beyond what the codec structurally requires.
- 3-D / M coordinates; the `geography` type.

## Public API (interfaces + responsibility)

All in namespace **`Gehtsoft.EF.Entities.Geometry`**, folder `Gehtsoft.EF.Entities/Geometry/`.
Type names carry a `Geo` prefix (matching the already-chosen `Geo*` `SqlFunctionId` convention and
sidestepping the namespace/type name clash a bare `Geometry` base would create). *(Naming is
sub-decision D1.)*

### Core value types

| Type | Kind | Responsibility |
|---|---|---|
| `GeoGeometryType` | enum | The 7 OGC subtypes: `Point`, `LineString`, `Polygon`, `MultiPoint`, `MultiLineString`, `MultiPolygon`, `GeometryCollection`. Numeric values align with OGC WKB type codes 1–7 so the WKB codec maps directly. |
| `GeoCoordinate` | value (`readonly struct` — sub-decision D2) | Immutable `(double X, double Y)`. Value equality, `ToString()` → `"X Y"` (invariant, round-trip precision). The one deliberate deviation from the "no structs" house style; a coordinate pair is the textbook value type and this keeps large geometries allocation-light and equality correct. |
| `GeoGeometry` | abstract `class`, `IEquatable<GeoGeometry>` | Base of the hierarchy. Carries `int Srid` and `GeoGeometryType GeometryType` (abstract). Declares `bool IsEmpty` (abstract), structural `Equals`/`GetHashCode` (SRID + subtype + shape), `ToString()` → WKT. Exposes convenience `ToWkt()`, `ToWkb()`, and static `Parse(string wkt, int srid)` / `FromWkb(byte[] wkb, int srid)` that delegate to the codec classes. |

### Concrete subtypes (all `sealed`, immutable, under `GeoGeometry`)

| Type | Shape it holds |
|---|---|
| `GeoPoint` | a single `GeoCoordinate` (+ an empty state) |
| `GeoLineString` | ordered `IReadOnlyList<GeoCoordinate>` |
| `GeoPolygon` | exterior ring + ordered interior rings (each a coordinate list) |
| `GeoMultiPoint` | `IReadOnlyList<GeoPoint>` |
| `GeoMultiLineString` | `IReadOnlyList<GeoLineString>` |
| `GeoMultiPolygon` | `IReadOnlyList<GeoPolygon>` |
| `GeoGeometryCollection` | `IReadOnlyList<GeoGeometry>` (heterogeneous) |

Constructors validate arguments with the house guard-clause style
(`ArgumentNullException.ThrowIfNull(x, nameof(x))`, `ArgumentException` for structurally invalid
input) and defensively copy incoming collections so instances are truly immutable. All members carry
XML doc comments; `Equals`/`GetHashCode`/`==`/`!=` carry `[DocgenIgnore]` per house convention.

### Codec classes

| Type | Responsibility |
|---|---|
| `GeoWktReader` | `GeoGeometry Read(string wkt, int srid)`. Parses OGC SFA text (all 7 subtypes, nested collections, `EMPTY`), invariant-culture numbers, tolerant of extra whitespace and both `MULTIPOINT ((x y),(x y))` and legacy `MULTIPOINT (x y, x y)` forms. Throws `GeoFormatException` on malformed input. SRID is supplied by the caller (WKT carries none). |
| `GeoWktWriter` | `string Write(GeoGeometry geometry)`. Emits canonical OGC WKT (parenthesized MULTIPOINT, `EMPTY` for empties), invariant culture, round-trip double precision (`"R"`/G17). Deterministic output for stable golden tests. |
| `GeoWkbReader` | `GeoGeometry Read(byte[] wkb, int srid)`. Standard OGC WKB: per-geometry byte-order flag honored (little **and** big endian, incl. mixed within nested geometries), type code 1–7, correct nesting. Throws `GeoFormatException` on truncated/invalid input. SRID supplied by caller (plain WKB carries none). |
| `GeoWkbWriter` | `byte[] Write(GeoGeometry geometry)`. Standard OGC WKB, **little-endian (NDR)**, no SRID prefix, no Z/M. |
| `GeoFormatException` | `class : FormatException`. Thrown by both readers for malformed WKT/WKB, with a message pinpointing the failure. |

*(Codec entry points are also surfaced as the static helpers on `GeoGeometry` in the table above so
callers have a single obvious door; the reader/writer classes remain public for direct use and
testing. Whether the codec is exposed as instance readers/writers or purely static facades is
sub-decision D3 — default: the four public reader/writer classes above.)*

## Key design decisions (locked unless a sub-decision overrides)

1. **SRID lives on the CLR object only.** Neither OGC WKT nor plain OGC WKB carries SRID, so it is a
   constructor/read argument and a property; codec round-trip preserves it via the object, never via
   the wire bytes. Round-trip tests assert SRID preservation through the object, separately from the
   byte/text payload. This matches the plan's "bind WKB, pass SRID separately in the constructor SQL."
2. **WKB write = little-endian, plain OGC, 2-D, no SRID.** Read = tolerant of both byte orders. This
   is exactly what `ST_AsBinary`/`STAsBinary`/`SDO_UTIL.TO_WKBGEOMETRY` return, so the DB read path in
   Phase 4 decodes with `GeoWkbReader` unchanged.
3. **Immutable + value equality.** Structural `Equals` (SRID + subtype + ordered coordinates, exact
   `double` bit-equality — no tolerance at the CLR layer; tolerant comparison belongs to DB
   acceptance tests). `ToString()` → WKT.
4. **No geometry algorithms.** The type is a carrier/codec. It does not validate polygon ring
   closure/orientation, self-intersection, or compute measures — the engines do that. The codec only
   enforces structural well-formedness (right token/byte shape, counts match).
5. **`net standard2.0`, house style** — block-scoped namespaces, 4-space indent, UTF-8 BOM, XML docs
   on every public member, `[DocgenIgnore]` on equality/operators, no LINQ (explicit loops, eager),
   classic guard clauses.

## Edge cases the tests must pin

- **Empty geometries** every subtype: `POINT EMPTY`, `LINESTRING EMPTY`, `POLYGON EMPTY`,
  `MULTIPOINT EMPTY`, …, `GEOMETRYCOLLECTION EMPTY`. WKT `EMPTY` ⇄ `IsEmpty == true`. **Empty point in
  WKB** has no canonical OGC encoding — sub-decision D4 (default: encode as a point with NaN,NaN and
  read NaN,NaN back to an empty point, documented; reject silently-wrong alternatives).
- **Both WKB byte orders**, including a collection whose members use different byte orders.
- **Precision**: `double` values round-trip exactly through WKT (G17) and WKB (raw IEEE-754 bytes).
- **Nested collections**: `GEOMETRYCOLLECTION` containing a `MULTIPOLYGON` containing polygons with
  holes; deep nesting.
- **Malformed input** → `GeoFormatException` (not a raw parse/`IndexOutOfRange`): bad keyword,
  unbalanced parens, wrong coordinate count, truncated WKB, unknown WKB type code, wrong byte-order
  flag.
- **Culture independence**: parsing/formatting under a comma-decimal culture still uses `.`.
- **Legacy `MULTIPOINT (x y, x y)`** parses; writer emits the canonical parenthesized form.

## Where it is implemented

Project: **`Gehtsoft.EF.Entities`** (`netstandard2.0`, DB-independent — correct home).

New files (folder `Gehtsoft.EF.Entities/Geometry/`):
```
Geometry/GeoGeometryType.cs
Geometry/GeoCoordinate.cs
Geometry/GeoGeometry.cs
Geometry/GeoPoint.cs
Geometry/GeoLineString.cs
Geometry/GeoPolygon.cs
Geometry/GeoMultiPoint.cs
Geometry/GeoMultiLineString.cs
Geometry/GeoMultiPolygon.cs
Geometry/GeoGeometryCollection.cs
Geometry/GeoFormatException.cs
Geometry/GeoWktReader.cs
Geometry/GeoWktWriter.cs
Geometry/GeoWkbReader.cs
Geometry/GeoWkbWriter.cs
```
Because the project sets `EnableDefaultCompileItems=false`, **each file above needs an explicit
`<Compile Include="Geometry\…\.cs" />`** (backslash paths) added to `Gehtsoft.EF.Entities.csproj`.
This is a required product-source build change (distinct from content-only files) and the *only*
build/csproj edit in Phase 0 — no dependency, target-framework, or `version.proj` change. I will
confirm before touching the csproj.

## How it is tested (deep tier only — no DB in Phase 0)

Test project **`Gehtsoft.EF.Test`** (xUnit v3, AwesomeAssertions, default-globbed → **no Compile
Include needed**). New folder `Gehtsoft.EF.Test/Geometry/`, namespace
`Gehtsoft.EF.Test.Geometry`. Style per `Entity/Tools/HashCreator.cs` (`[Theory]`/`[InlineData]`
table-driven, `.Should().Be(...)`).

Planned test files / coverage:
- `GeoWktRoundTripTest` — per subtype (+ empties, nested, holes, legacy multipoint): `WKT → geom →
  WKT` equals canonical WKT (`[Theory]` vectors); malformed WKT → `GeoFormatException`.
- `GeoWkbRoundTripTest` — per subtype: `geom → WKB → geom` structural equality; a few **golden
  byte-array** vectors (hand-verified against the OGC spec / a known engine output) asserted exactly;
  big-endian read vectors; mixed-endian collection; truncated/invalid → `GeoFormatException`.
- `GeoCrossCodecTest` — `WKT → geom → WKB → geom → WKT` stable across the loop; `double` precision
  exactness; SRID preserved through the object across both codecs.
- `GeoValueTest` — equality/inequality (subtype, SRID, coordinate differences), `GetHashCode`
  consistency, `ToString()` == WKT, immutability (constructor defensive-copy: mutating a passed list
  after construction does not change the geometry), guard-clause throws.
- `GeoEmptyAndCultureTest` — every-subtype empty handling; parse/format under a comma-decimal culture.

Target: exhaustive branch coverage of the codec (all subtypes × both codecs × empty/non-empty ×
both byte orders × malformed). No DB, so it runs everywhere including CI.

## Sub-decisions to confirm at Gate 2

- **D1 — Naming.** `Geo`-prefixed types in `Gehtsoft.EF.Entities.Geometry` (recommended: greppable,
  matches `Geo*` `SqlFunctionId`, avoids the `Geometry` namespace/type clash) vs unprefixed types in
  a differently-named namespace (e.g. `…​.Spatial`).
- **D2 — `GeoCoordinate` as `readonly struct`** (recommended: value semantics, low allocation) vs a
  sealed class to strictly match the "no structs" house style.
- **D3 — Codec exposure**: four public reader/writer classes (recommended) vs static `GeoWkt`/`GeoWkb`
  facades only, with the reader/writer internal.
- **D4 — Empty-point WKB encoding**: NaN,NaN convention (recommended, documented) vs throw on
  writing an empty point to WKB.
- **D5 — extended-form + dimensionality support** *(REVISED 2026-07-09 across three user messages:
  "read 3rd-party files", "keep SRID on output as well", "don't reject Z and M — routing data")*:
  - **EWKT/EWKB on read** — both codecs accept the PostGIS extended forms: EWKT's `SRID=<n>;` prefix
    and EWKB's `0x20000000` SRID flag (embedded SRID overrides the argument). Verified against a real
    TIGER/Line county boundary exported as both `test.wkt` (EWKT) and `test.wkb` (EWKB), embedded as
    test resources; the two decode to bit-identical geometries.
  - **SRID on output by default** — `ToWkt()`/`ToWkb()` emit EWKT/EWKB carrying the SRID; the plain
    OGC form (the DB wire form) is the `includeSrid: false` opt-out.
  - **Z (3-D) and M (measure) ordinates supported** — `GeoCoordinate` carries optional Z/M;
    WKT ISO `Z`/`M`/`ZM` tags (+ untagged auto-detect: 3rd ordinate → Z, 4th → M) and WKB EWKB Z/M
    flags + ISO type offsets (1000/2000/3000) are all read and written. Reverses the "2-D only" scope
    (GEO_PLAN decisions 14 & 15).

## Acceptance criteria for Phase 0 (definition of done)

- All 15 product files compile in `Gehtsoft.EF.Entities`; `<Compile Include>` entries added; solution
  builds clean (no new warnings).
- All Phase-0 tests green; codec branch coverage exhaustive per the list above.
- No DB, no driver, no attribute/accessor code introduced (kept for Phase 1+).
- `version.proj` untouched.
- KNOWN_ISSUES.md created under `CLAUDE/GEO/` only if a product bug is discovered (per house rule:
  tests assert intended behaviour, never adapt to a bug).
