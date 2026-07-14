# Phase 0 — Foundation: geometry codec abstraction + NetTopologySuite module (plan, REWRITTEN 2026-07-09)

*Rewritten after the 2026-07-09 architecture revision (see `../GEO_PLAN.md` banner). The framework no
longer owns a geometry type or a WKT/WKB parser. Phase 0 now delivers a small **codec abstraction** in
the core plus the **NetTopologySuite-backed implementation** in a separate module, and **retires** the
first-cut in-house `GeoGeometry` + codec. Pure .NET, no DB. Gate 2 applies (approve before coding).*

## Goal

Two things, so later phases have an object↔WKB conversion path without the framework owning any geometry
type or parser:

1. **Core codec abstraction** (`Gehtsoft.EF.Entities`, netstandard2.0, no external deps) — an
   `IGeometryCodec` + `IGeometryCodecFactory` + a static `GeometryCodecs` registration. This is the
   *only* geometry-related thing in the core; the `byte[]`/WKB path needs no codec at all.
2. **Default implementation module** (`Gehtsoft.EF.Geo.NetTopologySuite`, new project) — the sole shipped
   `IGeometryCodec`, mapping NetTopologySuite geometry objects ↔ WKB/WKT.

And **retire** the in-house `GeoGeometry` hierarchy + WKT/WKB codec (delete), keeping the `test.wkt` /
`test.wkb` fixtures to validate the NTS codec instead.

## Public API (interfaces + responsibility)

### Core — `Gehtsoft.EF.Entities`, namespace `Gehtsoft.EF.Entities.Geometry`

| Type | Kind | Responsibility |
|---|---|---|
| `IGeometryCodec` | interface | Converts the application's geometry objects ↔ portable wire forms. Members: `bool CanHandle(Type geometryType)`; `byte[] ToWkb(object geometry, bool includeSrid)`; `object FromWkb(byte[] wkb, int srid)`; `string ToWkt(object geometry, bool includeSrid)`; `object FromWkt(string wkt, int srid)`. Object-based (the framework works via `object`). The framework only *needs* the WKB pair for the DB path; WKT is for app/interop use and the conformance tests. |
| `IGeometryCodecFactory` | interface | `IGeometryCodec Create();` — the app-supplied factory that produces a (reusable) codec. |
| `GeometryCodecs` | static class | Registration/resolution: `static IGeometryCodecFactory Factory { get; set; }` (global default) and `static IGeometryCodec Resolve()` (uses `Factory`; throws a clear error if none registered — "reference Gehtsoft.EF.Geo.NetTopologySuite or set GeometryCodecs.Factory"). *(A per-`SqlDbConnection` override is added in Phase 1, where connections live.)* |

Files: `Geometry/IGeometryCodec.cs`, `Geometry/IGeometryCodecFactory.cs`, `Geometry/GeometryCodecs.cs`
(+ `<Compile Include>` in `Gehtsoft.EF.Entities.csproj`).

### Default module — `Gehtsoft.EF.Geo.NetTopologySuite` (NEW project)

| Type | Kind | Responsibility |
|---|---|---|
| `NtsGeometryCodec` | `IGeometryCodec` | Uses NTS `WKBReader`/`WKBWriter` (`HandleSRID`, `HandleOrdinates` for Z/M) and `WKTReader`/`WKTWriter`. `CanHandle` = `typeof(NetTopologySuite.Geometries.Geometry).IsAssignableFrom(t)`. `ToWkb(includeSrid)` → EWKB flag on/off; `FromWkb(bytes, srid)` sets SRID from the argument when the bytes carry none. |
| `NtsGeometryCodecFactory` | `IGeometryCodecFactory` | Produces a shared `NtsGeometryCodec`. |
| `NtsGeometry` (static helper) | — | `Register()` convenience → `GeometryCodecs.Factory = new NtsGeometryCodecFactory();` for app startup. |

Project: references `Gehtsoft.EF.Entities` + the `NetTopologySuite` NuGet package; `netstandard2.0`.
**This new project (csproj + `.sln` entry) is a build/structure change** — per the "don't touch build"
convention it needs the user's explicit go / coordination (sub-decision B1).

### Retired (deleted)

`Gehtsoft.EF.Entities/Geometry/`: `GeoGeometry`, `GeoPoint`, `GeoLineString`, `GeoPolygon`,
`GeoMultiPoint`, `GeoMultiLineString`, `GeoMultiPolygon`, `GeoGeometryCollection`, `GeoCoordinate`,
`GeoGeometryType`, `GeoFormatException`, `GeoWktReader/Writer`, `GeoWkbReader/Writer` — and their
`<Compile Include>` entries. Their unit tests (`GeoWktCodecTest`, `GeoWkbCodecTest`,
`GeoGeometryValueTest`, `GeoZmCodecTest`) are deleted; `GeoThirdPartyFileTest` is rewritten against the
NTS codec. `test.wkt` / `test.wkb` embedded resources are **kept**.

## How it is tested (deep tier only — no DB)

Test folder `Gehtsoft.EF.Test/Geometry/`, referencing the NTS module.
- **`NtsGeometryCodecTest`** — round-trip conformance through the codec: for representative NTS
  geometries (Point/LineString/Polygon+hole/Multi*/GeometryCollection, plus Z, M, ZM, and empties),
  `ToWkb`→`FromWkb` and `ToWkt`→`FromWkt` reproduce an equal geometry (NTS `EqualsExact`); `includeSrid`
  true/false honored; SRID applied on read when absent from the bytes.
- **`GeoThirdPartyFileTest`** (rewritten) — load the embedded `test.wkt` (EWKT) and `test.wkb` (EWKB),
  parse both via the codec, assert both are a MULTIPOLYGON with SRID 4326 and 6 polygons, and that the
  two decode to equal geometries (NTS `EqualsExact` / normalized). Proves 3rd-party interop via the
  shipped codec.
- **`GeometryCodecsRegistrationTest`** — `Resolve()` throws when no factory is set; after
  `NtsGeometry.Register()` it returns the NTS codec; `CanHandle` true for NTS types, false otherwise.

No DB, no attributes, no accessor, no queries (those are Phase 1+).

## Sub-decisions to confirm at Gate 2

- **B1 — new module project**: create `Gehtsoft.EF.Geo.NetTopologySuite` (csproj + add to the solution)
  as part of Phase 0 — a build/structure change. Do it now (recommended, the module is the whole point)
  vs you scaffold the project and I fill in content.
- **B2 — NTS package version**: pin `NetTopologySuite` to the current stable (I'll confirm the exact
  version at build time) vs a version you specify.
- **B3 — retire now vs keep in-house temporarily**: delete the in-house `GeoGeometry`+codec in this phase
  (recommended — one code path) vs leave it dormant until the NTS path is proven.

## Acceptance criteria (definition of done)

- Core abstraction compiles in `Gehtsoft.EF.Entities` (0 new warnings); in-house geometry files deleted +
  `<Compile Include>` entries removed.
- `Gehtsoft.EF.Geo.NetTopologySuite` builds and implements the abstraction over NTS.
- NTS codec conformance + 3rd-party-file + registration tests green.
- No DB / attribute / accessor / query code (Phase 1+); `version.proj` untouched; no commit unless asked.
