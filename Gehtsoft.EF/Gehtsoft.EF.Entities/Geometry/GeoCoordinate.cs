using System;
using System.Globalization;
using System.Text;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// An immutable coordinate with X and Y ordinates and optional Z (elevation) and M (measure)
    /// ordinates. A missing Z or M is represented by <see cref="double.NaN"/> (see <see cref="HasZ"/> /
    /// <see cref="HasM"/>). Equality is exact (bit-for-bit), with two NaN ordinates comparing equal
    /// (so absent==absent, and the empty-point NaN,NaN convention holds).
    /// </summary>
    public readonly struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        /// <summary>The X ordinate (easting / longitude).</summary>
        public double X { get; }

        /// <summary>The Y ordinate (northing / latitude).</summary>
        public double Y { get; }

        /// <summary>The Z ordinate (elevation), or <see cref="double.NaN"/> when absent.</summary>
        public double Z { get; }

        /// <summary>The M ordinate (measure, e.g. distance along a route), or <see cref="double.NaN"/> when absent.</summary>
        public double M { get; }

        /// <summary>Whether the coordinate carries a Z (elevation) ordinate.</summary>
        public bool HasZ => !double.IsNaN(Z);

        /// <summary>Whether the coordinate carries an M (measure) ordinate.</summary>
        public bool HasM => !double.IsNaN(M);

        /// <summary>Initializes a 2-D coordinate (XY).</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        public GeoCoordinate(double x, double y) : this(x, y, double.NaN, double.NaN)
        {
        }

        /// <summary>Initializes a 3-D coordinate (XYZ).</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        /// <param name="z">The Z (elevation) ordinate.</param>
        public GeoCoordinate(double x, double y, double z) : this(x, y, z, double.NaN)
        {
        }

        /// <summary>Initializes a coordinate with explicit X, Y, Z and M ordinates.</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        /// <param name="z">The Z (elevation) ordinate, or <see cref="double.NaN"/> when absent.</param>
        /// <param name="m">The M (measure) ordinate, or <see cref="double.NaN"/> when absent.</param>
        public GeoCoordinate(double x, double y, double z, double m)
        {
            X = x;
            Y = y;
            Z = z;
            M = m;
        }

        /// <summary>Creates a measured 2-D coordinate (XYM) — Y and M, no Z.</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        /// <param name="m">The M (measure) ordinate.</param>
        public static GeoCoordinate CreateXYM(double x, double y, double m) => new GeoCoordinate(x, y, double.NaN, m);

        /// <summary>Returns the ordinates in OGC text order ("X Y", "X Y Z", "X Y M", or "X Y Z M").</summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Format(X)).Append(' ').Append(Format(Y));
            if (HasZ)
                builder.Append(' ').Append(Format(Z));
            if (HasM)
                builder.Append(' ').Append(Format(M));
            return builder.ToString();
        }

        internal static string Format(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

        /// <summary>Determines whether this coordinate equals another (all four ordinates, bit-exact).</summary>
        /// <param name="other">The coordinate to compare with.</param>
        public bool Equals(GeoCoordinate other)
            => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && M.Equals(other.M);

        [DocgenIgnore]
        public override bool Equals(object obj)
            => obj is GeoCoordinate other && Equals(other);

        [DocgenIgnore]
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ M.GetHashCode();
                return hash;
            }
        }

        [DocgenIgnore]
        public static bool operator ==(GeoCoordinate left, GeoCoordinate right) => left.Equals(right);

        [DocgenIgnore]
        public static bool operator !=(GeoCoordinate left, GeoCoordinate right) => !left.Equals(right);
    }
}
