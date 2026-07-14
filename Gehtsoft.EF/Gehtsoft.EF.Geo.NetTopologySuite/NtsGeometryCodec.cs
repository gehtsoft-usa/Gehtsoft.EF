using System;
using System.Globalization;
using Gehtsoft.EF.Entities.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Gehtsoft.EF.Geo.NetTopologySuite
{
    /// <summary>
    /// The default <see cref="IGeometryCodec"/>, backed by NetTopologySuite. Converts NTS
    /// <see cref="Geometry"/> objects to and from WKB/WKT, honoring Z/M ordinates and (optionally) the
    /// SRID via the PostGIS EWKB flag / EWKT prefix.
    /// </summary>
    public sealed class NtsGeometryCodec : IGeometryCodec
    {
        /// <inheritdoc/>
        public bool CanHandle(Type geometryType)
        {
            if (geometryType == null)
                throw new ArgumentNullException(nameof(geometryType));
            return typeof(Geometry).IsAssignableFrom(geometryType);
        }

        /// <inheritdoc/>
        public byte[] ToWkb(object geometry, bool includeSrid)
        {
            Geometry value = AsGeometry(geometry);
            // (byteOrder, handleSRID, emitZ, emitM) — Z/M are still omitted when the geometry lacks them.
            var writer = new WKBWriter(ByteOrder.LittleEndian, includeSrid, true, true);
            return writer.Write(value);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public string ToWkt(object geometry, bool includeSrid)
        {
            Geometry value = AsGeometry(geometry);
            var writer = new WKTWriter(4) { OutputOrdinates = Ordinates.XYZM };
            string wkt = writer.Write(value);
            if (includeSrid)
                return string.Concat("SRID=", value.SRID.ToString(CultureInfo.InvariantCulture), ";", wkt);
            return wkt;
        }

        /// <inheritdoc/>
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
