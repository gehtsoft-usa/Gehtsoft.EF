using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC MultiLineString: a collection of line strings.</summary>
    public sealed class GeoMultiLineString : GeoGeometry
    {
        private readonly GeoLineString[] mLineStrings;

        /// <summary>The member line strings.</summary>
        public IReadOnlyList<GeoLineString> LineStrings => mLineStrings;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.MultiLineString;

        /// <inheritdoc/>
        public override bool IsEmpty => mLineStrings.Length == 0;

        /// <summary>Initializes a multi-line-string from its member line strings.</summary>
        /// <param name="lineStrings">The member line strings (copied defensively).</param>
        /// <param name="srid">The SRID.</param>
        public GeoMultiLineString(IEnumerable<GeoLineString> lineStrings, int srid = DefaultSrid) : base(srid)
        {
            if (lineStrings == null)
                throw new ArgumentNullException(nameof(lineStrings));
            mLineStrings = ToArray(lineStrings);
            for (int i = 0; i < mLineStrings.Length; i++)
                if (mLineStrings[i] == null)
                    throw new ArgumentException("A member line string cannot be null.", nameof(lineStrings));
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => GeometriesEqual(mLineStrings, ((GeoMultiLineString)other).mLineStrings);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => GeometriesHash(mLineStrings);
    }
}
