namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// The declared subtype of a geometry column.
    /// </summary>
    /// <remarks>
    /// Values align with the OGC WKB type codes; the extra <see cref="Geometry"/> member (0) means the
    /// column is not restricted to a single subtype.
    /// </remarks>
    public enum GeometrySubtype
    {
        /// <summary>Any geometry (the column is not restricted to a specific subtype).</summary>
        Geometry = 0,

        /// <summary>A point.</summary>
        Point = 1,

        /// <summary>A line string.</summary>
        LineString = 2,

        /// <summary>A polygon.</summary>
        Polygon = 3,

        /// <summary>A collection of points.</summary>
        MultiPoint = 4,

        /// <summary>A collection of line strings.</summary>
        MultiLineString = 5,

        /// <summary>A collection of polygons.</summary>
        MultiPolygon = 6,

        /// <summary>A heterogeneous collection of geometries.</summary>
        GeometryCollection = 7,
    }
}
