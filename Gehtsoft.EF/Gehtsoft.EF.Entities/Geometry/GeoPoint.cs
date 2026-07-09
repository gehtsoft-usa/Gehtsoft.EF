namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC Point: a single 2-D coordinate (or empty).</summary>
    public sealed class GeoPoint : GeoGeometry
    {
        private readonly GeoCoordinate mCoordinate;

        /// <summary>The point's coordinate. For an empty point both ordinates are NaN.</summary>
        public GeoCoordinate Coordinate => mCoordinate;

        /// <summary>The X ordinate.</summary>
        public double X => mCoordinate.X;

        /// <summary>The Y ordinate.</summary>
        public double Y => mCoordinate.Y;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.Point;

        /// <inheritdoc/>
        public override bool IsEmpty => double.IsNaN(mCoordinate.X) && double.IsNaN(mCoordinate.Y);

        /// <summary>Initializes a point from a coordinate.</summary>
        /// <param name="coordinate">The point's coordinate.</param>
        /// <param name="srid">The SRID.</param>
        public GeoPoint(GeoCoordinate coordinate, int srid = DefaultSrid) : base(srid)
        {
            mCoordinate = coordinate;
        }

        /// <summary>Initializes a point from its two ordinates.</summary>
        /// <param name="x">The X ordinate.</param>
        /// <param name="y">The Y ordinate.</param>
        /// <param name="srid">The SRID.</param>
        public GeoPoint(double x, double y, int srid = DefaultSrid) : this(new GeoCoordinate(x, y), srid)
        {
        }

        /// <summary>Creates an empty point (coordinate NaN,NaN) with the specified SRID.</summary>
        /// <param name="srid">The SRID.</param>
        public static GeoPoint Empty(int srid = DefaultSrid) => new GeoPoint(new GeoCoordinate(double.NaN, double.NaN), srid);

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => mCoordinate.Equals(((GeoPoint)other).mCoordinate);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => mCoordinate.GetHashCode();

        internal override GeoCoordinate? FirstCoordinate() => IsEmpty ? (GeoCoordinate?)null : mCoordinate;
    }
}
