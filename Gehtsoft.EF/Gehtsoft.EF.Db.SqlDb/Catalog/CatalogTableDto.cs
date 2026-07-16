using System.Collections.Generic;

namespace Gehtsoft.EF.Db.SqlDb.Catalog
{
    /// <summary>
    /// The catalogue (persisted) form of one table's DDL-relevant schema. It is a hand-owned model
    /// deliberately decoupled from the runtime <see cref="QueryBuilder.TableDescriptor"/> and its
    /// metadata classes, so refactoring those never breaks reading an existing catalogue.
    ///
    /// Enum-valued members are stored by their **name** (not their integer), so they survive enum
    /// reordering; all members are optional with a defined default, so a newer reader loads an older
    /// blob (missing members → default). See <see cref="CatalogSerializer"/>.
    /// </summary>
    public sealed class CatalogTableDto
    {
        /// <summary>The table name.</summary>
        public string Name { get; set; }

        /// <summary>The table scope, or `null`.</summary>
        public string Scope { get; set; }

        /// <summary>Whether the object is a view.</summary>
        public bool View { get; set; }

        /// <summary>Whether the table is marked obsolete (scheduled for drop).</summary>
        public bool Obsolete { get; set; }

        /// <summary>
        /// Whether the owning entity carries a dynamic-property set, i.e. an EAV <c>&lt;table&gt;_props</c>
        /// side table exists for it. A change of this flag on an already-catalogued table is reconciled by
        /// creating or dropping that side table; a first create / recreate / drop of the table itself
        /// carries the side table with it through the ordinary create/drop entity query.
        /// </summary>
        public bool HasDynamicProperties { get; set; }

        /// <summary>The columns, in declaration order.</summary>
        public List<CatalogColumnDto> Columns { get; set; } = new List<CatalogColumnDto>();

        /// <summary>The composite indexes declared on the table, in declaration order.</summary>
        public List<CatalogCompositeIndexDto> CompositeIndexes { get; set; } = new List<CatalogCompositeIndexDto>();
    }

    /// <summary>The catalogue form of one column.</summary>
    public sealed class CatalogColumnDto
    {
        /// <summary>The column identifier (entity property name, or the SQL name when unassociated).</summary>
        public string Id { get; set; }

        /// <summary>The SQL column name.</summary>
        public string Name { get; set; }

        /// <summary>The <see cref="System.Data.DbType"/> stored by name.</summary>
        public string DbType { get; set; }

        /// <summary>The column size.</summary>
        public int Size { get; set; }

        /// <summary>The numeric precision.</summary>
        public int Precision { get; set; }

        /// <summary>Whether the column is (part of) the primary key.</summary>
        public bool PrimaryKey { get; set; }

        /// <summary>Whether the column auto-increments.</summary>
        public bool Autoincrement { get; set; }

        /// <summary>Whether the column has a (single-column) sorted index.</summary>
        public bool Sorted { get; set; }

        /// <summary>Whether the column has a unique constraint/index.</summary>
        public bool Unique { get; set; }

        /// <summary>Whether the column accepts NULL.</summary>
        public bool Nullable { get; set; }

        /// <summary>The referenced table name for a foreign key, or `null`.</summary>
        public string ForeignTable { get; set; }

        /// <summary>Whether the column is skipped when all properties are read.</summary>
        public bool IgnoreRead { get; set; }

        /// <summary>The default value, or `null` when the column has none.</summary>
        public CatalogColumnDefault Default { get; set; }

        /// <summary>The geometry metadata when this is a geometry column, otherwise `null`.</summary>
        public CatalogGeometryDto Geometry { get; set; }

        /// <summary>The JSON metadata when this is a JSON column, otherwise `null`.</summary>
        public CatalogJsonDto Json { get; set; }
    }

    /// <summary>
    /// A column default value as a type-tag plus an invariant-culture string (RS1). The supported
    /// primitive set is: bool, byte, Int16, Int32, Int64, Single, Double, Decimal, string, DateTime,
    /// Guid. The value is kept as text; v1 does not reconstruct the CLR object (the diff compares text).
    /// </summary>
    public sealed class CatalogColumnDefault
    {
        /// <summary>The CLR type tag (the type's simple name, e.g. <c>Int32</c>).</summary>
        public string TypeName { get; set; }

        /// <summary>The value formatted with the invariant culture.</summary>
        public string Value { get; set; }
    }

    /// <summary>The catalogue form of a geometry column's metadata.</summary>
    public sealed class CatalogGeometryDto
    {
        /// <summary>The spatial reference identifier.</summary>
        public int Srid { get; set; }

        /// <summary>The declared geometry subtype, stored by name.</summary>
        public string Subtype { get; set; }

        /// <summary>Whether the column carries Z (elevation) ordinates.</summary>
        public bool HasZ { get; set; }

        /// <summary>Whether the column carries M (measure) ordinates.</summary>
        public bool HasM { get; set; }

        /// <summary>Whether the column accepts NULL.</summary>
        public bool Nullable { get; set; }

        /// <summary>The spatial indexes declared on the column, in declaration order.</summary>
        public List<CatalogSpatialIndexDto> Indexes { get; set; } = new List<CatalogSpatialIndexDto>();
    }

    /// <summary>The catalogue form of one spatial index.</summary>
    public sealed class CatalogSpatialIndexDto
    {
        /// <summary>The logical index name.</summary>
        public string Name { get; set; }

        /// <summary>Whether a complete bounding box is declared.</summary>
        public bool HasBoundingBox { get; set; }

        /// <summary>The minimum X of the bounding box (NaN when not declared).</summary>
        public double MinX { get; set; }

        /// <summary>The minimum Y of the bounding box (NaN when not declared).</summary>
        public double MinY { get; set; }

        /// <summary>The maximum X of the bounding box (NaN when not declared).</summary>
        public double MaxX { get; set; }

        /// <summary>The maximum Y of the bounding box (NaN when not declared).</summary>
        public double MaxY { get; set; }

        /// <summary>The tolerance (used by Oracle metadata).</summary>
        public double Tolerance { get; set; }
    }

    /// <summary>The catalogue form of a JSON column's metadata.</summary>
    public sealed class CatalogJsonDto
    {
        /// <summary>The CLR type stored in the column, by full name (opaque text for diffing, RS2).</summary>
        public string ClrType { get; set; }

        /// <summary>The value indexes declared on the JSON document, in declaration order.</summary>
        public List<CatalogJsonIndexDto> Indexes { get; set; } = new List<CatalogJsonIndexDto>();
    }

    /// <summary>The catalogue form of one JSON value index.</summary>
    public sealed class CatalogJsonIndexDto
    {
        /// <summary>The logical index name (derived from column, path and type).</summary>
        public string Name { get; set; }

        /// <summary>The JSON path to the indexed value, for example <c>$.age</c>.</summary>
        public string Path { get; set; }

        /// <summary>The primitive type of the value at the path, stored by name.</summary>
        public string DbType { get; set; }

        /// <summary>Whether the index is unique.</summary>
        public bool Unique { get; set; }
    }

    /// <summary>The catalogue form of one composite index.</summary>
    public sealed class CatalogCompositeIndexDto
    {
        /// <summary>The logical index name.</summary>
        public string Name { get; set; }

        /// <summary>The driver identifiers this index is excluded for, or `null`.</summary>
        public List<string> ExcludeFor { get; set; }

        /// <summary>The index fields, in order.</summary>
        public List<CatalogCompositeIndexFieldDto> Fields { get; set; } = new List<CatalogCompositeIndexFieldDto>();
    }

    /// <summary>The catalogue form of one composite-index field.</summary>
    public sealed class CatalogCompositeIndexFieldDto
    {
        /// <summary>The column (or JSON column) name.</summary>
        public string Name { get; set; }

        /// <summary>The applied function stored by name, or `null` for a plain column.</summary>
        public string Function { get; set; }

        /// <summary>The sort direction, stored by name.</summary>
        public string Direction { get; set; }

        /// <summary>For a JSON value field, the JSON path; otherwise `null`.</summary>
        public string JsonPath { get; set; }

        /// <summary>For a JSON value field, the value type stored by name; otherwise `null`.</summary>
        public string JsonType { get; set; }
    }
}
