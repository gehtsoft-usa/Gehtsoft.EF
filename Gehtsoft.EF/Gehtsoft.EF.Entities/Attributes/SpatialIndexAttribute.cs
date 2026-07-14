using System;

namespace Gehtsoft.EF.Entities
{
    /// <summary>
    /// Declares a spatial index on a <see cref="GeometryEntityPropertyAttribute">geometry property</see>.
    /// The attribute is repeatable (apply it once per index).
    ///
    /// A bounding box (<see cref="MinX"/>, <see cref="MinY"/>, <see cref="MaxX"/>, <see cref="MaxY"/>)
    /// and a <see cref="Tolerance"/> may be declared; some engines require them (SQL Server's bounding
    /// box, Oracle's dimension metadata / tolerance) while others ignore them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public class SpatialIndexAttribute : Attribute
    {
        /// <summary>The default Oracle tolerance used when none is declared.</summary>
        public const double DefaultTolerance = 0.005;

        /// <summary>The logical index name. If not set, the name is derived from the column.</summary>
        public string Name { get; set; }

        /// <summary>The minimum X of the declared bounding box, or <see cref="double.NaN"/> when not declared.</summary>
        public double MinX { get; set; } = double.NaN;

        /// <summary>The minimum Y of the declared bounding box, or <see cref="double.NaN"/> when not declared.</summary>
        public double MinY { get; set; } = double.NaN;

        /// <summary>The maximum X of the declared bounding box, or <see cref="double.NaN"/> when not declared.</summary>
        public double MaxX { get; set; } = double.NaN;

        /// <summary>The maximum Y of the declared bounding box, or <see cref="double.NaN"/> when not declared.</summary>
        public double MaxY { get; set; } = double.NaN;

        /// <summary>The tolerance (used by Oracle metadata). Defaults to <see cref="DefaultTolerance"/>.</summary>
        public double Tolerance { get; set; } = DefaultTolerance;

        /// <summary>Whether a complete bounding box has been declared.</summary>
        public bool HasBoundingBox
            => !double.IsNaN(MinX) && !double.IsNaN(MinY) && !double.IsNaN(MaxX) && !double.IsNaN(MaxY);

        /// <summary>Initializes a spatial index with no declared bounding box.</summary>
        public SpatialIndexAttribute()
        {
        }

        /// <summary>Initializes a spatial index with a declared bounding box.</summary>
        /// <param name="minX">The minimum X.</param>
        /// <param name="minY">The minimum Y.</param>
        /// <param name="maxX">The maximum X.</param>
        /// <param name="maxY">The maximum Y.</param>
        public SpatialIndexAttribute(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }
}
