using System.Globalization;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// Shared helpers for rendering geometry column / spatial-index DDL fragments across the drivers.
    /// </summary>
    [DocgenIgnore]
    public static class GeometryDdlHelper
    {
        /// <summary>
        /// The OGC subtype token (Pascal case, e.g. <c>Point</c>, <c>MultiPolygon</c>, or
        /// <c>Geometry</c> for an unrestricted column). Uppercase it at the call site for engines that
        /// want <c>POINT</c>.
        /// </summary>
        public static string SubtypeName(GeometrySubtype subtype)
        {
            switch (subtype)
            {
                case GeometrySubtype.Point: return "Point";
                case GeometrySubtype.LineString: return "LineString";
                case GeometrySubtype.Polygon: return "Polygon";
                case GeometrySubtype.MultiPoint: return "MultiPoint";
                case GeometrySubtype.MultiLineString: return "MultiLineString";
                case GeometrySubtype.MultiPolygon: return "MultiPolygon";
                case GeometrySubtype.GeometryCollection: return "GeometryCollection";
                default: return "Geometry";
            }
        }

        /// <summary>The PostGIS type suffix for the dimensionality: <c>""</c>, <c>Z</c>, <c>M</c> or <c>ZM</c>.</summary>
        public static string DimensionSuffix(bool hasZ, bool hasM)
            => (hasZ ? "Z" : "") + (hasM ? "M" : "");

        /// <summary>The SpatiaLite dimension token: <c>XY</c>, <c>XYZ</c>, <c>XYM</c> or <c>XYZM</c>.</summary>
        public static string DimensionToken(bool hasZ, bool hasM)
            => "XY" + (hasZ ? "Z" : "") + (hasM ? "M" : "");

        /// <summary>Formats a double for embedding in DDL (invariant culture, round-trip).</summary>
        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
