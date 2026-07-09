using System;
using System.Collections.Generic;
using System.IO;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Writes a geometry to standard OGC Well-Known Binary (WKB): little-endian (NDR), 2-D, no SRID
    /// prefix and no Z/M ordinates. This is exactly the wire form the database constructor functions
    /// (<c>ST_GeomFromWKB</c> / <c>STGeomFromWKB</c> / <c>SDO_UTIL.FROM_WKBGEOMETRY</c>) expect.
    /// An empty point is encoded as a point with NaN,NaN ordinates.
    /// </summary>
    public sealed class GeoWkbWriter
    {
        /// <summary>Writes the geometry as WKB.</summary>
        /// <param name="geometry">The geometry to write.</param>
        public byte[] Write(GeoGeometry geometry)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                WriteGeometry(writer, geometry);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private void WriteGeometry(BinaryWriter writer, GeoGeometry geometry)
        {
            writer.Write((byte)1); // NDR (little-endian) byte order marker
            writer.Write((uint)geometry.GeometryType);
            switch (geometry.GeometryType)
            {
                case GeoGeometryType.Point:
                    WritePoint(writer, (GeoPoint)geometry);
                    break;
                case GeoGeometryType.LineString:
                    WriteCoordinates(writer, ((GeoLineString)geometry).Coordinates);
                    break;
                case GeoGeometryType.Polygon:
                    WriteRings(writer, ((GeoPolygon)geometry).Rings);
                    break;
                case GeoGeometryType.MultiPoint:
                    WriteMembers(writer, ((GeoMultiPoint)geometry).Points);
                    break;
                case GeoGeometryType.MultiLineString:
                    WriteMembers(writer, ((GeoMultiLineString)geometry).LineStrings);
                    break;
                case GeoGeometryType.MultiPolygon:
                    WriteMembers(writer, ((GeoMultiPolygon)geometry).Polygons);
                    break;
                case GeoGeometryType.GeometryCollection:
                    WriteMembers(writer, ((GeoGeometryCollection)geometry).Geometries);
                    break;
                default:
                    throw new GeoFormatException($"Unsupported geometry type '{geometry.GeometryType}'.");
            }
        }

        private void WritePoint(BinaryWriter writer, GeoPoint point)
        {
            if (point.IsEmpty)
            {
                writer.Write(double.NaN);
                writer.Write(double.NaN);
            }
            else
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
        }

        private void WriteRings(BinaryWriter writer, IReadOnlyList<IReadOnlyList<GeoCoordinate>> rings)
        {
            writer.Write((uint)rings.Count);
            for (int i = 0; i < rings.Count; i++)
                WriteCoordinates(writer, rings[i]);
        }

        private void WriteCoordinates(BinaryWriter writer, IReadOnlyList<GeoCoordinate> coordinates)
        {
            writer.Write((uint)coordinates.Count);
            for (int i = 0; i < coordinates.Count; i++)
            {
                writer.Write(coordinates[i].X);
                writer.Write(coordinates[i].Y);
            }
        }

        private void WriteMembers<T>(BinaryWriter writer, IReadOnlyList<T> members) where T : GeoGeometry
        {
            writer.Write((uint)members.Count);
            for (int i = 0; i < members.Count; i++)
                WriteGeometry(writer, members[i]);
        }
    }
}
