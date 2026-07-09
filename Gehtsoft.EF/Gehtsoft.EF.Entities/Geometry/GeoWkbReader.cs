using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// Reads a geometry from OGC Well-Known Binary (WKB). Honors the per-geometry byte-order marker
    /// (little- and big-endian, including a collection whose members differ), and reads the 2-D
    /// coordinates for each of the seven subtypes. A point with NaN,NaN ordinates decodes to an empty
    /// point. The PostGIS <b>EWKB</b> flags are honored — SRID (0x20000000, the embedded SRID overrides
    /// the argument passed to <see cref="Read"/>), Z (0x80000000) and M (0x40000000) — as is the ISO WKB
    /// dimensionality offset (1000 = Z, 2000 = M, 3000 = ZM). Malformed or truncated input raises
    /// <see cref="GeoFormatException"/>.
    /// </summary>
    public sealed class GeoWkbReader
    {
        private const uint SridFlag = 0x20000000;
        private const uint ZFlag = 0x80000000;
        private const uint MFlag = 0x40000000;
        private const uint BaseTypeMask = 0x1FFFFFFF;

        /// <summary>Reads a geometry from WKB, assigning the specified SRID unless the data embeds one (EWKB).</summary>
        /// <param name="wkb">The WKB bytes.</param>
        /// <param name="srid">The SRID to assign when the data does not embed one.</param>
        public GeoGeometry Read(byte[] wkb, int srid = GeoGeometry.DefaultSrid)
        {
            if (wkb == null)
                throw new ArgumentNullException(nameof(wkb));
            var reader = new Reader(wkb, srid);
            GeoGeometry geometry = reader.ReadGeometry();
            reader.EnsureAtEnd();
            return geometry;
        }

        private sealed class Reader
        {
            private readonly byte[] mData;
            private int mSrid;
            private int mPos;

            public Reader(byte[] data, int srid)
            {
                mData = data;
                mSrid = srid;
                mPos = 0;
            }

            public GeoGeometry ReadGeometry()
            {
                bool little = ReadByteOrder();
                uint raw = ReadUInt32(little);
                bool hasZ = (raw & ZFlag) != 0;
                bool hasM = (raw & MFlag) != 0;
                if ((raw & SridFlag) != 0)
                    mSrid = (int)ReadUInt32(little);
                uint type = raw & BaseTypeMask;
                // ISO WKB encodes dimensionality as a type offset (1000 = Z, 2000 = M, 3000 = ZM).
                if (type >= 3000)
                {
                    hasZ = true;
                    hasM = true;
                    type -= 3000;
                }
                else if (type >= 2000)
                {
                    hasM = true;
                    type -= 2000;
                }
                else if (type >= 1000)
                {
                    hasZ = true;
                    type -= 1000;
                }
                switch (type)
                {
                    case 1: return ReadPoint(little, hasZ, hasM);
                    case 2: return ReadLineString(little, hasZ, hasM);
                    case 3: return ReadPolygon(little, hasZ, hasM);
                    case 4: return ReadMultiPoint(little);
                    case 5: return ReadMultiLineString(little);
                    case 6: return ReadMultiPolygon(little);
                    case 7: return ReadGeometryCollection(little);
                    default: throw Error($"unsupported WKB geometry type code {type}");
                }
            }

            private GeoGeometry ReadPoint(bool little, bool hasZ, bool hasM)
            {
                GeoCoordinate coordinate = ReadOrdinates(little, hasZ, hasM);
                if (double.IsNaN(coordinate.X) && double.IsNaN(coordinate.Y))
                    return GeoPoint.Empty(mSrid);
                return new GeoPoint(coordinate, mSrid);
            }

            private GeoGeometry ReadLineString(bool little, bool hasZ, bool hasM)
                => new GeoLineString(ReadCoordinates(little, hasZ, hasM), mSrid);

            private GeoGeometry ReadPolygon(bool little, bool hasZ, bool hasM)
            {
                uint ringCount = ReadUInt32(little);
                var rings = new List<List<GeoCoordinate>>();
                for (uint i = 0; i < ringCount; i++)
                    rings.Add(ReadCoordinates(little, hasZ, hasM));
                return new GeoPolygon(rings, mSrid);
            }

            private GeoGeometry ReadMultiPoint(bool little)
            {
                uint count = ReadUInt32(little);
                var points = new List<GeoPoint>();
                for (uint i = 0; i < count; i++)
                    points.Add(ReadMember<GeoPoint>());
                return new GeoMultiPoint(points, mSrid);
            }

            private GeoGeometry ReadMultiLineString(bool little)
            {
                uint count = ReadUInt32(little);
                var lines = new List<GeoLineString>();
                for (uint i = 0; i < count; i++)
                    lines.Add(ReadMember<GeoLineString>());
                return new GeoMultiLineString(lines, mSrid);
            }

            private GeoGeometry ReadMultiPolygon(bool little)
            {
                uint count = ReadUInt32(little);
                var polygons = new List<GeoPolygon>();
                for (uint i = 0; i < count; i++)
                    polygons.Add(ReadMember<GeoPolygon>());
                return new GeoMultiPolygon(polygons, mSrid);
            }

            private GeoGeometry ReadGeometryCollection(bool little)
            {
                uint count = ReadUInt32(little);
                var geometries = new List<GeoGeometry>();
                for (uint i = 0; i < count; i++)
                    geometries.Add(ReadGeometry());
                return new GeoGeometryCollection(geometries, mSrid);
            }

            private T ReadMember<T>() where T : GeoGeometry
            {
                GeoGeometry geometry = ReadGeometry();
                if (!(geometry is T typed))
                    throw Error($"expected a {typeof(T).Name} member but found {geometry.GeometryType}");
                return typed;
            }

            private List<GeoCoordinate> ReadCoordinates(bool little, bool hasZ, bool hasM)
            {
                uint count = ReadUInt32(little);
                var list = new List<GeoCoordinate>();
                for (uint i = 0; i < count; i++)
                    list.Add(ReadOrdinates(little, hasZ, hasM));
                return list;
            }

            private GeoCoordinate ReadOrdinates(bool little, bool hasZ, bool hasM)
            {
                double x = ReadDouble(little);
                double y = ReadDouble(little);
                double z = hasZ ? ReadDouble(little) : double.NaN;
                double m = hasM ? ReadDouble(little) : double.NaN;
                return new GeoCoordinate(x, y, z, m);
            }

            private bool ReadByteOrder()
            {
                byte order = ReadByte();
                if (order == 1)
                    return true;
                if (order == 0)
                    return false;
                throw Error($"invalid WKB byte-order marker {order}");
            }

            private byte ReadByte()
            {
                Ensure(1);
                return mData[mPos++];
            }

            private uint ReadUInt32(bool little)
            {
                Ensure(4);
                uint value;
                if (little)
                    value = (uint)(mData[mPos] | (mData[mPos + 1] << 8) | (mData[mPos + 2] << 16) | (mData[mPos + 3] << 24));
                else
                    value = (uint)((mData[mPos] << 24) | (mData[mPos + 1] << 16) | (mData[mPos + 2] << 8) | mData[mPos + 3]);
                mPos += 4;
                return value;
            }

            private double ReadDouble(bool little)
            {
                Ensure(8);
                byte[] temp = new byte[8];
                Array.Copy(mData, mPos, temp, 0, 8);
                mPos += 8;
                if (little != BitConverter.IsLittleEndian)
                    Array.Reverse(temp);
                return BitConverter.ToDouble(temp, 0);
            }

            private void Ensure(int count)
            {
                if (mPos + count > mData.Length)
                    throw Error("unexpected end of WKB data");
            }

            public void EnsureAtEnd()
            {
                if (mPos != mData.Length)
                    throw Error("unexpected trailing bytes in WKB data");
            }

            private GeoFormatException Error(string message)
                => new GeoFormatException($"WKB parse error at byte {mPos}: {message}.");
        }
    }
}
