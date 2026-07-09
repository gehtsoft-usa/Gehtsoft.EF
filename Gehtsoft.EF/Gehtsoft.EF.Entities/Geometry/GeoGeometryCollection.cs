using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC GeometryCollection: a heterogeneous collection of geometries.</summary>
    public sealed class GeoGeometryCollection : GeoGeometry
    {
        private readonly GeoGeometry[] mGeometries;

        /// <summary>The member geometries.</summary>
        public IReadOnlyList<GeoGeometry> Geometries => mGeometries;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.GeometryCollection;

        /// <inheritdoc/>
        public override bool IsEmpty => mGeometries.Length == 0;

        /// <summary>Initializes a geometry collection from its member geometries.</summary>
        /// <param name="geometries">The member geometries (copied defensively).</param>
        /// <param name="srid">The SRID.</param>
        public GeoGeometryCollection(IEnumerable<GeoGeometry> geometries, int srid = DefaultSrid) : base(srid)
        {
            if (geometries == null)
                throw new ArgumentNullException(nameof(geometries));
            mGeometries = ToArray(geometries);
            for (int i = 0; i < mGeometries.Length; i++)
                if (mGeometries[i] == null)
                    throw new ArgumentException("A member geometry cannot be null.", nameof(geometries));
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => GeometriesEqual(mGeometries, ((GeoGeometryCollection)other).mGeometries);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => GeometriesHash(mGeometries);

        internal override GeoCoordinate? FirstCoordinate() => mGeometries.Length > 0 ? mGeometries[0].FirstCoordinate() : null;
    }
}
