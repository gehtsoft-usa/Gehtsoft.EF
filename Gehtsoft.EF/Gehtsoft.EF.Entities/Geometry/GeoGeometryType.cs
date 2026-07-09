namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// The OGC geometry subtypes supported by the geo feature. The numeric values intentionally
    /// match the OGC Well-Known Binary (WKB) type codes so the WKB codec maps them directly.
    /// </summary>
    public enum GeoGeometryType
    {
        /// <summary>A single point (WKB code 1).</summary>
        Point = 1,

        /// <summary>A sequence of connected points (WKB code 2).</summary>
        LineString = 2,

        /// <summary>A surface bounded by an exterior ring and zero or more interior rings (WKB code 3).</summary>
        Polygon = 3,

        /// <summary>A collection of points (WKB code 4).</summary>
        MultiPoint = 4,

        /// <summary>A collection of line strings (WKB code 5).</summary>
        MultiLineString = 5,

        /// <summary>A collection of polygons (WKB code 6).</summary>
        MultiPolygon = 6,

        /// <summary>A heterogeneous collection of geometries (WKB code 7).</summary>
        GeometryCollection = 7,
    }
}
