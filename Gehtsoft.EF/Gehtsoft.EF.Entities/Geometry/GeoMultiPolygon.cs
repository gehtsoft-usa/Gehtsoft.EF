using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC MultiPolygon: a collection of polygons.</summary>
    public sealed class GeoMultiPolygon : GeoGeometry
    {
        private readonly GeoPolygon[] mPolygons;

        /// <summary>The member polygons.</summary>
        public IReadOnlyList<GeoPolygon> Polygons => mPolygons;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.MultiPolygon;

        /// <inheritdoc/>
        public override bool IsEmpty => mPolygons.Length == 0;

        /// <summary>Initializes a multi-polygon from its member polygons.</summary>
        /// <param name="polygons">The member polygons (copied defensively).</param>
        /// <param name="srid">The SRID.</param>
        public GeoMultiPolygon(IEnumerable<GeoPolygon> polygons, int srid = DefaultSrid) : base(srid)
        {
            if (polygons == null)
                throw new ArgumentNullException(nameof(polygons));
            mPolygons = ToArray(polygons);
            for (int i = 0; i < mPolygons.Length; i++)
                if (mPolygons[i] == null)
                    throw new ArgumentException("A member polygon cannot be null.", nameof(polygons));
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => GeometriesEqual(mPolygons, ((GeoMultiPolygon)other).mPolygons);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => GeometriesHash(mPolygons);
    }
}
