using System;
using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Marks an entity property as a geometry (spatial) column, stored in a native spatial column and round-tripped as Well-Known Binary (WKB).
    /// </summary>
    /// <remarks>
    /// The property may be declared as <c>byte[]</c> (raw WKB - no geometry library needed) or as a
    /// geometry object handled by a registered <see cref="IGeometryCodec"/> (for example a
    /// NetTopologySuite geometry via the <c>Gehtsoft.EF.Geo.NetTopologySuite</c> module).
    /// Use this attribute instead of <see cref="EntityPropertyAttribute"/> on the property.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public class GeometryEntityPropertyAttribute : Attribute
    {
        /// <summary>The name of the column; if not set, it is derived from the entity naming policy.</summary>
        public string Field { get; set; }

        /// <summary>The spatial reference identifier of the column (defaults to 4326, EPSG:4326 / WGS84).</summary>
        public int Srid { get; set; } = 4326;

        /// <summary>The declared geometry subtype of the column (defaults to Geometry, meaning any subtype).</summary>
        public GeometrySubtype Subtype { get; set; } = GeometrySubtype.Geometry;

        /// <summary>The flag indicating that the column carries Z (elevation) ordinates.</summary>
        public bool HasZ { get; set; }

        /// <summary>The flag indicating that the column carries M (measure) ordinates.</summary>
        public bool HasM { get; set; }

        /// <summary>The flag indicating that the column can hold a NULL value.</summary>
        public bool Nullable { get; set; }
    }
}
