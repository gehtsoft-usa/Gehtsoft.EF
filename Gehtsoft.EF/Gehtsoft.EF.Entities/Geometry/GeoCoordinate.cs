using System;
using System.Globalization;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// An immutable 2-D coordinate: a pair of X and Y ordinates. Equality is exact (bit-for-bit),
    /// with two NaN ordinates comparing equal (the empty-point convention).
    /// </summary>
    public readonly struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        /// <summary>The X ordinate (easting / longitude).</summary>
        public double X { get; }

        /// <summary>The Y ordinate (northing / latitude).</summary>
        public double Y { get; }

        /// <summary>Initializes a new coordinate from its two ordinates.</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        public GeoCoordinate(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Returns the OGC text form of the coordinate ("X Y", invariant culture).</summary>
        public override string ToString()
            => string.Concat(X.ToString("G17", CultureInfo.InvariantCulture), " ", Y.ToString("G17", CultureInfo.InvariantCulture));

        /// <summary>Determines whether this coordinate equals another.</summary>
        /// <param name="other">The coordinate to compare with.</param>
        public bool Equals(GeoCoordinate other)
            => X.Equals(other.X) && Y.Equals(other.Y);

        [DocgenIgnore]
        public override bool Equals(object obj)
            => obj is GeoCoordinate other && Equals(other);

        [DocgenIgnore]
        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        [DocgenIgnore]
        public static bool operator ==(GeoCoordinate left, GeoCoordinate right) => left.Equals(right);

        [DocgenIgnore]
        public static bool operator !=(GeoCoordinate left, GeoCoordinate right) => !left.Equals(right);
    }
}
