using System;
using System.Globalization;
using Gehtsoft.EF.Entities.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>
    /// The default geometry codec, backed by NetTopologySuite.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IGeometryCodec"/>, converting NTS <see cref="Geometry"/> objects to and from
    /// WKB/WKT, honoring Z/M ordinates and (optionally) the SRID via the PostGIS EWKB flag / EWKT prefix.
    /// </remarks>
    public sealed class NtsGeometryCodec : IGeometryCodec
    {
        /// <summary>Whether this codec can convert values of the specified CLR geometry type.</summary>
        /// <param name="geometryType">The CLR type of the entity's geometry property.</param>
        public bool CanHandle(Type geometryType)
        {
            if (geometryType == null)
                throw new ArgumentNullException(nameof(geometryType));
            return typeof(Geometry).IsAssignableFrom(geometryType);
        }

        /// <summary>Serializes a geometry object to Well-Known Binary.</summary>
        /// <param name="geometry">The geometry object (never null).</param>
        /// <param name="includeSrid">When true, carry the SRID (EWKB); when false, plain OGC WKB.</param>
        public byte[] ToWkb(object geometry, bool includeSrid)
        {
            Geometry value = AsGeometry(geometry);
            // Emit exactly the ordinates the geometry actually carries. Forcing Z/M on a 2-D geometry
            // would promote it to XYZM (with NaN Z/M) and databases with a fixed-dimension geometry
            // column (SpatiaLite XY, MySQL 2-D, ...) reject the mismatched dimensionality.
            Ordinates ordinates = GeometryOrdinates(value);
            var writer = new WKBWriter(ByteOrder.LittleEndian, includeSrid,
                (ordinates & Ordinates.Z) != 0, (ordinates & Ordinates.M) != 0);
            return writer.Write(value);
        }

        // The union of the ordinates present across every coordinate sequence of the geometry.
        private static Ordinates GeometryOrdinates(Geometry geometry)
        {
            var collector = new OrdinatesCollector();
            geometry.Apply(collector);
            return collector.Ordinates;
        }

        private sealed class OrdinatesCollector : ICoordinateSequenceFilter
        {
            public Ordinates Ordinates { get; private set; } = Ordinates.XY;
            public bool Done => Ordinates == Ordinates.XYZM;
            public bool GeometryChanged => false;
            public void Filter(CoordinateSequence seq, int i) => Ordinates |= seq.Ordinates;
        }

        /// <summary>Deserializes a geometry object from Well-Known Binary.</summary>
        /// <param name="wkb">The WKB bytes (never null).</param>
        /// <param name="srid">The SRID to assign when the bytes do not embed one.</param>
        public object FromWkb(byte[] wkb, int srid)
        {
            if (wkb == null)
                throw new ArgumentNullException(nameof(wkb));
            var reader = new WKBReader();
            Geometry value = reader.Read(wkb);
            // NTS reads the SRID from EWKB; when the bytes carry none, assign the requested SRID.
            if (value != null && !WkbCarriesSrid(wkb))
                value.SRID = srid;
            return value;
        }

        private static bool WkbCarriesSrid(byte[] wkb)
        {
            if (wkb.Length < 5)
                return false;
            bool little = wkb[0] != 0;
            uint type = little
                ? (uint)(wkb[1] | (wkb[2] << 8) | (wkb[3] << 16) | (wkb[4] << 24))
                : (uint)((wkb[1] << 24) | (wkb[2] << 16) | (wkb[3] << 8) | wkb[4]);
            return (type & 0x20000000u) != 0; // PostGIS EWKB SRID flag
        }

        /// <summary>Serializes a geometry object to Well-Known Text.</summary>
        /// <param name="geometry">The geometry object (never null).</param>
        /// <param name="includeSrid">When true, prefix with the EWKT SRID header; when false, plain WKT.</param>
        public string ToWkt(object geometry, bool includeSrid)
        {
            Geometry value = AsGeometry(geometry);
            var writer = new WKTWriter(4) { OutputOrdinates = Ordinates.XYZM };
            string wkt = writer.Write(value);
            if (includeSrid)
                return string.Concat("SRID=", value.SRID.ToString(CultureInfo.InvariantCulture), ";", wkt);
            return wkt;
        }

        /// <summary>Deserializes a geometry object from Well-Known Text.</summary>
        /// <param name="wkt">The WKT (or EWKT) string (never null).</param>
        /// <param name="srid">The SRID to assign when the text does not embed one.</param>
        public object FromWkt(string wkt, int srid)
        {
            if (wkt == null)
                throw new ArgumentNullException(nameof(wkt));
            string text = wkt.TrimStart();
            int effectiveSrid = srid;
            if (text.StartsWith("SRID=", StringComparison.OrdinalIgnoreCase))
            {
                int semicolon = text.IndexOf(';');
                if (semicolon < 0)
                    throw new ArgumentException("Malformed EWKT: missing ';' after the SRID prefix.", nameof(wkt));
                string sridText = text.Substring(5, semicolon - 5).Trim();
                if (!int.TryParse(sridText, NumberStyles.Integer, CultureInfo.InvariantCulture, out effectiveSrid))
                    throw new ArgumentException($"Malformed EWKT SRID '{sridText}'.", nameof(wkt));
                text = text.Substring(semicolon + 1);
            }
            var reader = new WKTReader();
            Geometry value = reader.Read(text);
            if (value != null)
                value.SRID = effectiveSrid;
            return value;
        }

        private static Geometry AsGeometry(object geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            if (!(geometry is Geometry value))
                throw new ArgumentException(
                    $"Expected a NetTopologySuite geometry but got '{geometry.GetType().FullName}'.", nameof(geometry));
            return value;
        }
    }
}
