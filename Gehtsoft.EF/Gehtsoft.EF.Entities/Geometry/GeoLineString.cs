using System;
using System.Collections.Generic;

namespace Gehtsoft.EF.Entities.Geometry
{
    /// <summary>An OGC LineString: an ordered sequence of 2-D coordinates.</summary>
    public sealed class GeoLineString : GeoGeometry
    {
        private readonly GeoCoordinate[] mCoordinates;

        /// <summary>The ordered coordinates of the line string.</summary>
        public IReadOnlyList<GeoCoordinate> Coordinates => mCoordinates;

        /// <inheritdoc/>
        public override GeoGeometryType GeometryType => GeoGeometryType.LineString;

        /// <inheritdoc/>
        public override bool IsEmpty => mCoordinates.Length == 0;

        /// <summary>Initializes a line string from its coordinates.</summary>
        /// <param name="coordinates">The ordered coordinates (copied defensively).</param>
        /// <param name="srid">The SRID.</param>
        public GeoLineString(IEnumerable<GeoCoordinate> coordinates, int srid = DefaultSrid) : base(srid)
        {
            if (coordinates == null)
                throw new ArgumentNullException(nameof(coordinates));
            mCoordinates = ToArray(coordinates);
        }

        /// <inheritdoc/>
        protected override bool EqualsShape(GeoGeometry other) => CoordinatesEqual(mCoordinates, ((GeoLineString)other).mCoordinates);

        /// <inheritdoc/>
        protected override int ShapeHashCode() => CoordinatesHash(mCoordinates);
    }
}
