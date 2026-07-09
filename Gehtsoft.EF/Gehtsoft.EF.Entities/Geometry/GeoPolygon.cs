using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>
    /// An OGC Polygon: an exterior ring followed by zero or more interior rings (holes). Each ring is
    /// a coordinate list. Ring closure and orientation are not validated by this type — the database
    /// engine is responsible for geometric validity.
    /// </summary>
    public sealed class GeoPolygon : GeoGeometry
    {
        private readonly IReadOnlyList<GeoCoordinate>[] mRings;

        /// <summary>All rings; index 0 is the exterior ring, the rest are interior rings (holes).</summary>
        public IReadOnlyList<IReadOnlyList<GeoCoordinate>> Rings => mRings;

        /// <summary>The exterior ring, or an empty list if the polygon is empty.</summary>
        public IReadOnlyList<GeoCoordinate> ExteriorRing => mRings.Length > 0 ? mRings[0] : Array.Empty<GeoCoordinate>();

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.Polygon;

        /// <inheritdoc/>
        public override bool IsEmpty => mRings.Length == 0;

        /// <summary>Initializes a polygon from its rings.</summary>
        /// <param name="rings">The rings (exterior first, then holes); copied defensively.</param>
        /// <param name="srid">The SRID.</param>
        public GeoPolygon(IEnumerable<IEnumerable<GeoCoordinate>> rings, int srid = DefaultSrid) : base(srid)
        {
            if (rings == null)
                throw new ArgumentNullException(nameof(rings));
            var snapshot = new List<IReadOnlyList<GeoCoordinate>>();
            foreach (var ring in rings)
            {
                if (ring == null)
                    throw new ArgumentException("A polygon ring cannot be null.", nameof(rings));
                snapshot.Add(ToArray(ring));
            }
            mRings = snapshot.ToArray();
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other)
        {
            var o = (GeoPolygon)other;
            if (mRings.Length != o.mRings.Length)
                return false;
            for (int i = 0; i < mRings.Length; i++)
                if (!CoordinatesEqual(mRings[i], o.mRings[i]))
                    return false;
            return true;
        }

        /// <inheritdoc/>
        protected override int ShapeHashCode()
        {
            int hash = mRings.Length;
            for (int i = 0; i < mRings.Length; i++)
                hash = CombineHash(hash, CoordinatesHash(mRings[i]));
            return hash;
        }

        internal override GeoCoordinate? FirstCoordinate()
            => mRings.Length > 0 && mRings[0].Count > 0 ? (GeoCoordinate?)mRings[0][0] : null;
    }
}
