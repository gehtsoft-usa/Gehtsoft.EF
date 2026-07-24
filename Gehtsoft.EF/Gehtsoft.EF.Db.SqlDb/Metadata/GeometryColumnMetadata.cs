using System;
using System.Collections.Generic;
using Gehtsoft.EF.Entities.Geometry;

namespace Gehtsoft.EF.Db.SqlDb.Metadata
{
    /// <summary>
    /// Describes a geometry column: its CLR type, SRID, declared subtype, dimensionality, nullability and spatial indexes.
    /// </summary>
    /// <remarks>
    /// It is attached to <see cref="QueryBuilder.TableDescriptor.ColumnInfo.Geometry"/> when the entity
    /// property is marked with <see cref="Gehtsoft.EF.Entities.GeometryEntityPropertyAttribute"/>.
    /// </remarks>
    public sealed class GeometryColumnMetadata
    {
        /// <summary>The CLR type of the property (either byte[] or a codec-handled geometry type).</summary>
        public Type ClrType { get; }

        /// <summary>The spatial reference identifier of the column.</summary>
        public int Srid { get; }

        /// <summary>The declared geometry subtype (Geometry means any).</summary>
        public GeometrySubtype Subtype { get; }

        /// <summary>Whether the column carries Z (elevation) ordinates.</summary>
        public bool HasZ { get; }

        /// <summary>Whether the column carries M (measure) ordinates.</summary>
        public bool HasM { get; }

        /// <summary>Whether the column accepts NULL.</summary>
        public bool Nullable { get; }

        /// <summary>The spatial indexes declared on the column (may be empty).</summary>
        public IReadOnlyList<SpatialIndexDefinition> Indexes { get; }

        /// <summary>Initializes a new instance of the GeometryColumnMetadata class.</summary>
        /// <param name="clrType">The CLR type of the property.</param>
        /// <param name="srid">The SRID.</param>
        /// <param name="subtype">The declared subtype.</param>
        /// <param name="hasZ">Whether the column carries Z ordinates.</param>
        /// <param name="hasM">Whether the column carries M ordinates.</param>
        /// <param name="nullable">Whether the column accepts NULL.</param>
        /// <param name="indexes">The declared spatial indexes (may be empty).</param>
        public GeometryColumnMetadata(Type clrType, int srid, GeometrySubtype subtype, bool hasZ, bool hasM, bool nullable, IReadOnlyList<SpatialIndexDefinition> indexes)
        {
            ClrType = clrType;
            Srid = srid;
            Subtype = subtype;
            HasZ = hasZ;
            HasM = hasM;
            Nullable = nullable;
            Indexes = indexes ?? Array.Empty<SpatialIndexDefinition>();
        }
    }

    /// <summary>Describes one spatial index declared on a geometry column.</summary>
    public sealed class SpatialIndexDefinition
    {
        /// <summary>The logical index name; the physical name combines the table name and this name.</summary>
        public string Name { get; }

        /// <summary>Whether a complete bounding box is declared.</summary>
        public bool HasBoundingBox { get; }

        /// <summary>The minimum X of the bounding box (NaN when not declared).</summary>
        public double MinX { get; }

        /// <summary>The minimum Y of the bounding box (NaN when not declared).</summary>
        public double MinY { get; }

        /// <summary>The maximum X of the bounding box (NaN when not declared).</summary>
        public double MaxX { get; }

        /// <summary>The maximum Y of the bounding box (NaN when not declared).</summary>
        public double MaxY { get; }

        /// <summary>The tolerance (used by Oracle metadata).</summary>
        public double Tolerance { get; }

        /// <summary>Initializes a new instance of the SpatialIndexDefinition class.</summary>
        /// <param name="name">The logical index name.</param>
        /// <param name="hasBoundingBox">Whether a bounding box is declared.</param>
        /// <param name="minX">The minimum X.</param>
        /// <param name="minY">The minimum Y.</param>
        /// <param name="maxX">The maximum X.</param>
        /// <param name="maxY">The maximum Y.</param>
        /// <param name="tolerance">The tolerance.</param>
        public SpatialIndexDefinition(string name, bool hasBoundingBox, double minX, double minY, double maxX, double maxY, double tolerance)
        {
            Name = name;
            HasBoundingBox = hasBoundingBox;
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
            Tolerance = tolerance;
        }
    }
}
