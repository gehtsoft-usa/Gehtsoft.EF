using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC MultiPoint: a collection of points.</summary>
    public sealed class GeoMultiPoint : GeoGeometry
    {
        private readonly GeoPoint[] mPoints;

        /// <summary>The member points.</summary>
        public IReadOnlyList<GeoPoint> Points => mPoints;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.MultiPoint;

        /// <inheritdoc/>
        public override bool IsEmpty => mPoints.Length == 0;

        /// <summary>Initializes a multi-point from its member points.</summary>
        /// <param name="points">The member points (copied defensively).</param>
        /// <param name="srid">The SRID.</param>
        public GeoMultiPoint(IEnumerable<GeoPoint> points, int srid = DefaultSrid) : base(srid)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            mPoints = ToArray(points);
            for (int i = 0; i < mPoints.Length; i++)
                if (mPoints[i] == null)
                    throw new ArgumentException("A member point cannot be null.", nameof(points));
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => GeometriesEqual(mPoints, ((GeoMultiPoint)other).mPoints);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => GeometriesHash(mPoints);
    }
}
