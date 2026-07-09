using System;
using System.Collections.Generic;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// The base class of the in-house 2-D geometry hierarchy. A geometry carries an integer SRID and
    /// is round-tripped to and from the database as Well-Known Binary (WKB); Well-Known Text (WKT) is
    /// provided for debugging and human-readable use. Instances are immutable and compared for
    /// structural (value) equality; the geometry types themselves compute nothing spatial — area,
    /// length, and topology are computed by the database engine.
    /// </summary>
    public abstract class GeoGeometry : IEquatable<GeoGeometry>
    {
        /// <summary>The default SRID (EPSG:4326, WGS84 lon/lat) used when none is specified.</summary>
        public const int DefaultSrid = 4326;

        /// <summary>The spatial reference identifier of the geometry.</summary>
        public int Srid { get; }

        /// <summary>The OGC subtype of the geometry.</summary>
        public abstract GeoGeometryType GeometryType { get; }

        /// <summary>Gets a value indicating whether the geometry holds no coordinates.</summary>
        public abstract bool IsEmpty { get; }

        /// <summary>Initializes the base geometry with the specified SRID.</summary>
        /// <param name="srid">The spatial reference identifier.</param>
        protected GeoGeometry(int srid)
        {
            Srid = srid;
        }

        /// <summary>Returns the Well-Known Text representation of the geometry.</summary>
        public string ToWkt() => new GeoWktWriter().Write(this);

        /// <summary>Returns the Well-Known Binary representation of the geometry (little-endian, no SRID).</summary>
        public byte[] ToWkb() => new GeoWkbWriter().Write(this);

        /// <summary>Parses a geometry from its Well-Known Text representation.</summary>
        /// <param name="wkt">The WKT string.</param>
        /// <param name="srid">The SRID to assign (WKT carries none).</param>
        public static GeoGeometry Parse(string wkt, int srid = DefaultSrid) => new GeoWktReader().Read(wkt, srid);

        /// <summary>Reads a geometry from its Well-Known Binary representation.</summary>
        /// <param name="wkb">The WKB bytes.</param>
        /// <param name="srid">The SRID to assign (plain WKB carries none).</param>
        public static GeoGeometry FromWkb(byte[] wkb, int srid = DefaultSrid) => new GeoWkbReader().Read(wkb, srid);

        /// <summary>Returns the Well-Known Text representation of the geometry.</summary>
        public override string ToString() => ToWkt();

        /// <summary>Determines whether this geometry is structurally equal to another.</summary>
        /// <param name="other">The geometry to compare with.</param>
        public bool Equals(GeoGeometry other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            if (other.GetType() != GetType())
                return false;
            if (Srid != other.Srid)
                return false;
            return EqualsShape(other);
        }

        [DocgenIgnore]
        public override bool Equals(object obj) => Equals(obj as GeoGeometry);

        [DocgenIgnore]
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Srid;
                hash = CombineHash(hash, (int)GeometryType);
                hash = CombineHash(hash, ShapeHashCode());
                return hash;
            }
        }

        [DocgenIgnore]
        public static bool operator ==(GeoGeometry left, GeoGeometry right)
        {
            if (left is null || right is null)
                return Object.Equals(left, right);
            return left.Equals(right);
        }

        [DocgenIgnore]
        public static bool operator !=(GeoGeometry left, GeoGeometry right) => !(left == right);

        /// <summary>Compares the shape (subtype-specific payload) of two geometries known to share type and SRID.</summary>
        /// <param name="other">The other geometry, guaranteed to be the same runtime type.</param>
        protected abstract bool EqualsShape(GeoGeometry other);

        /// <summary>Computes the hash of the subtype-specific payload.</summary>
        protected abstract int ShapeHashCode();

        /// <summary>Folds a value into a running hash code.</summary>
        /// <param name="hash">The running hash.</param>
        /// <param name="value">The value to fold in.</param>
        protected static int CombineHash(int hash, int value)
        {
            unchecked
            {
                return (hash * 397) ^ value;
            }
        }

        /// <summary>Snapshots an enumerable into an array (defensive copy; avoids LINQ).</summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="items">The source enumerable.</param>
        protected static T[] ToArray<T>(IEnumerable<T> items)
        {
            var list = new List<T>();
            foreach (var item in items)
                list.Add(item);
            return list.ToArray();
        }

        /// <summary>Compares two coordinate lists element-wise.</summary>
        protected static bool CoordinatesEqual(IReadOnlyList<GeoCoordinate> left, IReadOnlyList<GeoCoordinate> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (!left[i].Equals(right[i]))
                    return false;
            return true;
        }

        /// <summary>Computes an order-sensitive hash of a coordinate list.</summary>
        protected static int CoordinatesHash(IReadOnlyList<GeoCoordinate> coordinates)
        {
            int hash = coordinates.Count;
            for (int i = 0; i < coordinates.Count; i++)
                hash = CombineHash(hash, coordinates[i].GetHashCode());
            return hash;
        }

        /// <summary>Compares two geometry lists element-wise.</summary>
        protected static bool GeometriesEqual(IReadOnlyList<GeoGeometry> left, IReadOnlyList<GeoGeometry> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (!left[i].Equals(right[i]))
                    return false;
            return true;
        }

        /// <summary>Computes an order-sensitive hash of a geometry list.</summary>
        protected static int GeometriesHash(IReadOnlyList<GeoGeometry> geometries)
        {
            int hash = geometries.Count;
            for (int i = 0; i < geometries.Count; i++)
                hash = CombineHash(hash, geometries[i].GetHashCode());
            return hash;
        }
    }
}
