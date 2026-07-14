# Geo test datasets — CSV catalogue + WKT/WKB format

This directory holds real-world geospatial datasets converted from their original
ESRI shapefiles into a database-friendly form: a **CSV catalogue** of attributes
plus two **per-layer geometry blobs** (WKT text and binary WKB) addressed by byte
offset/length from the catalogue. It is intended as test data for a database geo
type that ingests **WKT / WKB**.

The source shapefiles were taken from the public-domain **WKB4J** test suite
(`wkb4j.sourceforge.net`); see `README.txt`. The subfolders are three independent,
publicly documented datasets described in [Datasets](#datasets) below.

---

## File format

Each source layer `<base>` (e.g. `usa`, `tgr08059lkA`, `i_2294_g_dd`) is represented
by **three files** in the same folder:

| File | Contents |
|------|----------|
| `<base>.csv` | `;`-delimited catalogue, one row per feature (see columns below). |
| `<base>.wkb` | Every feature's geometry as OGC **WKB**, concatenated back-to-back (no separators). |
| `<base>.wkt` | Every feature's geometry as OGC **WKT**, one geometry per line (`\n`-terminated). |

### Catalogue columns

```
object-name ; basefilename ; wkb_offset ; wkb_length ; wkt_offset ; wkt_length ; <attribute fields…>
```

| Column | Meaning |
|--------|---------|
| `object-name` | Stable per-feature id, `"<basefilename>_<n>"` (`n` = 1-based feature index). Empty if the feature has no geometry. |
| `basefilename` | Layer stem — identifies which `.wkb` / `.wkt` file the geometry lives in. |
| `wkb_offset` | 0-based **byte** offset of this feature's WKB within `<base>.wkb`. |
| `wkb_length` | Length in **bytes** of this feature's WKB. |
| `wkt_offset` | 0-based **byte** offset of this feature's WKT line within `<base>.wkt`. |
| `wkt_length` | Length in **bytes** of the WKT text (the trailing `\n` is **not** counted). |
| `<attribute fields…>` | The original DBF attribute columns, in DBF field order. |

Records tile each blob exactly — for feature `n`, `wkb_offset[n] == wkb_offset[n-1] + wkb_length[n-1]`,
and the last record ends at end-of-file. Rows with empty geometry leave all four
offset/length cells blank.

### Reading a feature's geometry

- **WKB (binary):** seek to `wkb_offset`, read `wkb_length` bytes → a complete OGC WKB value.
- **WKT (text):** seek to `wkt_offset`, read `wkt_length` bytes → the WKT string; or simply read
  line `n` of `<base>.wkt` (one WKT per line).

### Conventions

- **WKB** is standard OGC WKB, **little-endian (NDR)**, byte-order flag `01`. No SRID / no
  PostGIS EWKB extension — the source shapefiles carry no `.prj`, so no coordinate system is asserted.
- **WKT** coordinates are rounded to **6 decimal places**. The **WKB retains the exact
  IEEE-754 doubles** from the source; treat WKB as the authoritative geometry and WKT as the
  human-readable/rounded view.
- **Geometry types** are the faithful OGC multi-types: shapefile *PolyLine* → `MULTILINESTRING`,
  shapefile *Polygon* → `MULTIPOLYGON` (with correct outer-ring / hole nesting), *Point* → `POINT`.
- **Encoding:** UTF-8 without BOM; line terminator `\n` throughout (so byte offsets are stable
  across platforms).
- **CSV escaping:** RFC 4180 style against the `;` delimiter (a field containing `;`, `"`, or a
  newline is double-quoted with `""` escaping). Numeric attributes use invariant culture.

### Regenerating

The converter is a small .NET tool in `converter/` built on
**NetTopologySuite.IO.Esri** (shapefile reader) and **NetTopologySuite**
(`WKTWriter`/`WKBWriter`). To (re)generate one layer:

```
dotnet converter/bin/Release/net8.0/Shp2Csv.dll <path/to/layer.shp> [--outdir <dir>]
```

Polygons are read with `GeometryBuilderMode.FixInvalidShapes` so real-world rings that
violate strict orientation still build into valid OGC geometries.

> **Note — `tgr08059lpt`:** the original point shapefile has corrupt SHP record
> content-length headers (a 2001-era writer bug); its geometry bytes are intact. It is
> converted from a header-repaired copy (kept in `work/`). Point at `work/tgr08059lpt.shp`
> if you regenerate that layer.

---

## Datasets

### `tiger-line/` — U.S. Census TIGER/Line 2000

Topologically Integrated Geographic Encoding and Referencing (**TIGER/Line**) data from
the **U.S. Census Bureau**, 2000 vintage, distributed via ESRI's Geography Network
(see `tiger-line/readme.html`). Public domain (U.S. Government work). This extract covers
a single county: **Jefferson County, Colorado** (state+county FIPS **08059**).

File names follow `tgr<FIPS><layer>`. Feature classes are distinguished by the Census
**CFCC** (Census Feature Class Code) attribute. Line layers are split by CFCC category:

| Layer | Geometry | Features | Description |
|-------|----------|---------:|-------------|
| `tgr08059cty00` | MultiPolygon | 1 | County boundary (2000). |
| `tgr08059kgl` | MultiPolygon | 6 | Key Geographic Location landmark polygons. |
| `tgr08059wat` | MultiPolygon | 60 | Water / area-hydrography polygons. |
| `tgr08059lpt` | Point | 102 | Landmark points. |
| `tgr08059lkA` | MultiLineString | 33862 | Line features — **roads**. |
| `tgr08059lkB` | MultiLineString | 215 | Line features — **rails**. |
| `tgr08059lkC` | MultiLineString | 21 | Line features — **miscellaneous transportation**. |
| `tgr08059lkE` | MultiLineString | 273 | Line features — **physical** (fence/property lines etc.). |
| `tgr08059lkF` | MultiLineString | 6452 | Line features — **non-visible** boundaries. |
| `tgr08059lkH` | MultiLineString | 3550 | Line features — **hydrography** (streams/shorelines). |

Key attribute fields:

- **Line layers (`lk*`):** `TLID`, `FNODE`, `TNODE`, `LENGTH`, `FEDIRP`, `FENAME`, `FETYPE`,
  `FEDIRS`, `CFCC`, `FRADDL`, `TOADDL`, `FRADDR`, `TOADDR`, `ZIPL`, `ZIPR`, `CENSUS1`,
  `CENSUS2`, `CFCC1`, `CFCC2`, `SOURCE` (address ranges and left/right ZIP for street segments).
- **`cty00`:** `ID`, `FIPSSTCO`, `STATE`, `COUNTY`.
- **`kgl`:** `ID`, `POLYID`, `COUNTY`, `CFCC`, `KGLNAME`.
- **`wat`:** `ID`, `COUNTY`, `CFCC`, `LANDNAME`, `LANDPOLY`.
- **`lpt`:** `ID`, `CFCC`, `NAME`.

Coordinates are geographic (longitude/latitude, decimal degrees, NAD83).

### `mars-i-2294/` — USGS Geologic Map of Mars (I-2294)

**U.S. Geological Survey Miscellaneous Investigations Series Map I‑2294**,
*"Geologic Map of Science Study Area 1B, West Mangala Valles Region of Mars
(MTM −08157 Quadrangle)"*, by Mary G. Chapman, Harold Masursky, and Arthur L. Dial Jr.,
published 1991 (see `mars-i-2294/i_2294_g_dd.htm` / `2294readme_dd.txt`). Public domain.

Compiled from Viking 1:500,000-scale photomosaics. The original map is in a projected
coordinate system on the **Clarke 1866** spheroid; these copies have been **unprojected to
decimal degrees**. Approximate extent: **149.9°–155.1° W, 2.4°–12.6° S**. Note that
Mars longitudes are natively **positive-west** — the shipped files store them as positive-east
(negated), matching common GIS conventions.

| Layer | Geometry | Features | Description |
|-------|----------|---------:|-------------|
| `i_2294_g_dd` | MultiPolygon | 1968 | Geologic units / contacts (map polygons). |
| `i_2294_s_dd` | MultiLineString | 593 | Structural features (faults, ridges, channel lines). |

Attribute fields:

- **`i_2294_g_dd`:** `AREA`, `PERIMETER`, `I_2294_G_`, `I_2294_G_I`, `UNAME` (unit name), `UNIT`.
- **`i_2294_s_dd`:** `FNODE_`, `TNODE_`, `LPOLY_`, `RPOLY_`, `LENGTH`, `I_2294_S_`, `I_2294_S_I`,
  `LINETYPE`, `MARKER` (classic ARC/INFO coverage topology fields).

### `usa/` — United States state polygons

A polygon layer of U.S. states with area, region and population attributes (an ESRI-style
sample layer). As shipped it is a **partial** extract: **548 polygon features spanning 22
states**, predominantly the eastern seaboard, with every detached landmass stored as its own
feature (e.g. Florida is fragmented into 208 features by its keys and islands, Maine into 53).

| Layer | Geometry | Features | Description |
|-------|----------|---------:|-------------|
| `usa` | MultiPolygon | 548 | State-boundary polygons with 1990 & 1999 population. |

Attribute fields: `AREA`, `PERIMETER`, `STATE_NAME`, `ABBREV`, `REGION`, `STATE_FIPS`,
`POP_1990`, `POP_1999`. Coordinates are geographic (longitude/latitude, decimal degrees).

---

## Feature totals

| Folder | Layers | Features |
|--------|-------:|---------:|
| `tiger-line/` | 10 | 44 542 |
| `mars-i-2294/` | 2 | 2 561 |
| `usa/` | 1 | 548 |
| **Total** | **13** | **47 651** |
