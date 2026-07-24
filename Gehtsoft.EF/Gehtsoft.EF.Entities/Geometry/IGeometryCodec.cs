using System;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Converts an application-chosen geometry object to and from the portable wire forms (WKB and WKT).
    /// </summary>
    /// <remarks>
    /// The framework itself never owns a geometry type or a parser: a geometry column is fundamentally a
    /// <c>byte[]</c> (WKB), and an application that maps it to a richer object type supplies an
    /// <see cref="IGeometryCodec"/> (see <see cref="IGeometryCodecFactory"/> and <see cref="GeometryCodecs"/>).
    /// The database wire form is WKB; WKT is provided for interchange and debugging.
    /// </remarks>
    public interface IGeometryCodec
    {
        /// <summary>Whether this codec can convert values of the specified CLR geometry type.</summary>
        /// <param name="geometryType">The CLR type of the entity's geometry property.</param>
        bool CanHandle(Type geometryType);

        /// <summary>Serializes a geometry object to Well-Known Binary.</summary>
        /// <param name="geometry">The geometry object (never null).</param>
        /// <param name="includeSrid">When true, carry the SRID (EWKB); when false, plain OGC WKB.</param>
        byte[] ToWkb(object geometry, bool includeSrid);

        /// <summary>Deserializes a geometry object from Well-Known Binary.</summary>
        /// <param name="wkb">The WKB bytes (never null).</param>
        /// <param name="srid">The SRID to assign when the bytes do not embed one.</param>
        object FromWkb(byte[] wkb, int srid);

        /// <summary>Serializes a geometry object to Well-Known Text.</summary>
        /// <param name="geometry">The geometry object (never null).</param>
        /// <param name="includeSrid">When true, prefix with the EWKT <c>SRID=&lt;n&gt;;</c>; when false, plain WKT.</param>
        string ToWkt(object geometry, bool includeSrid);

        /// <summary>Deserializes a geometry object from Well-Known Text.</summary>
        /// <param name="wkt">The WKT (or EWKT) string (never null).</param>
        /// <param name="srid">The SRID to assign when the text does not embed one.</param>
        object FromWkt(string wkt, int srid);
    }
}
