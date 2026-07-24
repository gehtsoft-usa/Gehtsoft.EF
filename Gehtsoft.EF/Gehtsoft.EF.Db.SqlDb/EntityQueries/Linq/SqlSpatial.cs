using System;
using System.Diagnostics.CodeAnalysis;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// Marker methods for geometry (spatial) operations inside a LINQ query.
    /// </summary>
    /// <remarks>
    /// The geo twin of <see cref="SqlFunction"/>. They exist only to be recognised by the LINQ expression
    /// compiler and translated to the dialect's spatial SQL (<c>ST_Intersects</c>, <c>ST_Area</c>, and so
    /// on); calling one directly throws.
    ///
    /// The first argument is always the geometry property (a <c>byte[]</c> WKB column). The operand geometry
    /// is plain OGC WKB (<c>byte[]</c>) - encode a geometry object at the call site with the application's
    /// codec, for example <c>GeometryCodecs.Resolve().ToWkb(point, false)</c>. Keeping the operand a
    /// <c>byte[]</c> lets this marker live in core (no NetTopologySuite dependency); the encode is app code.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static class SqlSpatial
    {
        private static T Marker<T>() => throw new InvalidOperationException(
            "SqlSpatial methods are LINQ query markers and cannot be executed directly - use them only inside a LINQ query expression.");

        // ---- topological predicates (a DE-9IM relationship between the column and the operand) ----

        /// <summary>The geometry and the operand intersect.</summary>
        public static bool Intersects(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry contains the operand.</summary>
        public static bool Contains(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry is within the operand.</summary>
        public static bool Within(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry and the operand are disjoint.</summary>
        public static bool Disjoint(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry and the operand touch.</summary>
        public static bool Touches(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry and the operand overlap.</summary>
        public static bool Overlaps(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry and the operand cross (throws FeatureNotSupported on Oracle).</summary>
        public static bool Crosses(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry and the operand are spatially equal.</summary>
        public static bool SpatialEquals(byte[] geometry, byte[] other) => Marker<bool>();

        /// <summary>The geometry is within the given distance of the operand (planar units).</summary>
        public static bool DWithin(byte[] geometry, byte[] other, double distance) => Marker<bool>();

        // ---- scalars (a measurement or accessor yielding a number) ----

        /// <summary>The area of the geometry (planar).</summary>
        public static double Area(byte[] geometry) => Marker<double>();

        /// <summary>The length / perimeter of the geometry (planar).</summary>
        public static double Length(byte[] geometry) => Marker<double>();

        /// <summary>The planar distance between the geometry and the operand.</summary>
        public static double Distance(byte[] geometry, byte[] other) => Marker<double>();

        /// <summary>The X ordinate of a point geometry.</summary>
        public static double X(byte[] geometry) => Marker<double>();

        /// <summary>The Y ordinate of a point geometry.</summary>
        public static double Y(byte[] geometry) => Marker<double>();
    }
}
