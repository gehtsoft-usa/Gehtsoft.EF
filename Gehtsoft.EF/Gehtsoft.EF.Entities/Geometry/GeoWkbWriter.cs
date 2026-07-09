using System;
using System.Collections.Generic;
using System.IO;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Writes a geometry to Well-Known Binary (WKB): little-endian (NDR). Z (elevation) and M (measure)
    /// ordinates are emitted whenever the geometry carries them (PostGIS <b>EWKB</b> 0x80000000 / 0x40000000
    /// flags). An empty point is encoded as a point with NaN,NaN ordinates. By default the SRID is also
    /// carried (the 0x20000000 flag on the top-level type followed by the SRID); pass
    /// <c>includeSrid: false</c> for the SRID-less form the database constructor functions
    /// (<c>ST_GeomFromWKB</c> / <c>STGeomFromWKB</c> / <c>SDO_UTIL.FROM_WKBGEOMETRY</c>) expect.
    /// </summary>
    public sealed class GeoWkbWriter
    {
        private const uint SridFlag = 0x20000000;
        private const uint ZFlag = 0x80000000;
        private const uint MFlag = 0x40000000;

        /// <summary>Writes the geometry as WKB.</summary>
        /// <param name="geometry">The geometry to write.</param>
        /// <param name="includeSrid">When true (default), carry the SRID (EWKB); when false, omit it.</param>
        public byte[] Write(GeoGeometry geometry, bool includeSrid = true)
        {
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                WriteGeometry(writer, geometry, includeSrid);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private void WriteGeometry(BinaryWriter writer, GeoGeometry geometry, bool includeSrid)
        {
            bool hasZ = geometry.HasZ;
            bool hasM = geometry.HasM;

            writer.Write((byte)1); // NDR (little-endian) byte order marker

            uint type = (uint)geometry.GeometryType;
            if (hasZ)
                type |= ZFlag;
            if (hasM)
                type |= MFlag;
            if (includeSrid)
                type |= SridFlag;
            writer.Write(type);
            if (includeSrid)
                writer.Write((uint)geometry.Srid);

            switch (geometry.GeometryType)
            {
                case GeoGeometryType.Point:
                    WritePoint(writer, (GeoPoint)geometry, hasZ, hasM);
                    break;
                case GeoGeometryType.LineString:
                    WriteCoordinates(writer, ((GeoLineString)geometry).Coordinates, hasZ, hasM);
                    break;
                case GeoGeometryType.Polygon:
                    WriteRings(writer, ((GeoPolygon)geometry).Rings, hasZ, hasM);
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

        private void WritePoint(BinaryWriter writer, GeoPoint point, bool hasZ, bool hasM)
        {
            if (point.IsEmpty)
                WriteOrdinates(writer, new GeoCoordinate(double.NaN, double.NaN), hasZ, hasM);
            else
                WriteOrdinates(writer, point.Coordinate, hasZ, hasM);
        }

        private void WriteRings(BinaryWriter writer, IReadOnlyList<IReadOnlyList<GeoCoordinate>> rings, bool hasZ, bool hasM)
        {
            writer.Write((uint)rings.Count);
            for (int i = 0; i < rings.Count; i++)
                WriteCoordinates(writer, rings[i], hasZ, hasM);
        }

        private void WriteCoordinates(BinaryWriter writer, IReadOnlyList<GeoCoordinate> coordinates, bool hasZ, bool hasM)
        {
            writer.Write((uint)coordinates.Count);
            for (int i = 0; i < coordinates.Count; i++)
                WriteOrdinates(writer, coordinates[i], hasZ, hasM);
        }

        private void WriteOrdinates(BinaryWriter writer, GeoCoordinate coordinate, bool hasZ, bool hasM)
        {
            writer.Write(coordinate.X);
            writer.Write(coordinate.Y);
            if (hasZ)
                writer.Write(coordinate.Z);
            if (hasM)
                writer.Write(coordinate.M);
        }

        private void WriteMembers<T>(BinaryWriter writer, IReadOnlyList<T> members) where T : GeoGeometry
        {
            writer.Write((uint)members.Count);
            for (int i = 0; i < members.Count; i++)
                WriteGeometry(writer, members[i], false);
        }
    }
}
