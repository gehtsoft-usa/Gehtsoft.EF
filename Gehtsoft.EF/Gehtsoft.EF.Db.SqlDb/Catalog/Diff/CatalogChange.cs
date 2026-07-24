using Gehtsoft.EF.Db.SqlDb.Catalog;

namespace Gehtsoft.EF.Db.SqlDb.Catalog.Diff
{
    /// <summary>
    /// The kind of a single <see cref="CatalogChange"/> produced by <see cref="CatalogDiff"/>. The
    /// index kinds are deliberately explicit (single-column, composite, spatial, JSON) rather than one
    /// grouped "index" kind, so the DDL emitter can dispatch to the right builder without re-inspecting
    /// the payload.
    /// </summary>
    internal enum CatalogChangeKind
    {
        /// <summary>Create the whole table (columns and its own indexes) - there was no prior state.</summary>
        CreateTable,
        /// <summary>Drop the whole table.</summary>
        DropTable,

        /// <summary>Add a plain column (carrying any single-column indexes it declares).</summary>
        AddColumn,
        /// <summary>Drop a plain column (its dependent indexes go with it).</summary>
        DropColumn,
        /// <summary>Change a column's definition (type/size/precision/nullability/PK/autoincrement/FK/default).</summary>
        AlterColumn,

        /// <summary>Add a geometry column (carrying its spatial indexes).</summary>
        AddGeometryColumn,
        /// <summary>Drop a geometry column (its spatial indexes go with it).</summary>
        DropGeometryColumn,

        /// <summary>Add a single-column (plain or unique) index to an existing column.</summary>
        AddIndex,
        /// <summary>Drop a single-column (plain or unique) index from an existing column.</summary>
        DropIndex,

        /// <summary>Add a composite (multi-field) index.</summary>
        AddCompositeIndex,
        /// <summary>Drop a composite (multi-field) index.</summary>
        DropCompositeIndex,

        /// <summary>Add a spatial index to an existing geometry column.</summary>
        AddSpatialIndex,
        /// <summary>Drop a spatial index from an existing geometry column.</summary>
        DropSpatialIndex,

        /// <summary>Add a JSON value index to an existing JSON column.</summary>
        AddJsonIndex,
        /// <summary>Drop a JSON value index from an existing JSON column.</summary>
        DropJsonIndex,

        /// <summary>Create the dynamic-property (EAV) side table for an existing table that gained one.</summary>
        AddDynamicPropertiesTable,
        /// <summary>Drop the dynamic-property (EAV) side table for an existing table that lost it.</summary>
        DropDynamicPropertiesTable,
    }

    /// <summary>
    /// One ordered step of a table's reconciliation, produced by <see cref="CatalogDiff.Compare"/>.
    /// The payload members that are relevant depend on <see cref="Kind"/> (see each factory method);
    /// irrelevant members are `null`/`false`. Instances are immutable; construct them through the
    /// factory methods.
    /// </summary>
    internal sealed class CatalogChange
    {
        /// <summary>The kind of change.</summary>
        public CatalogChangeKind Kind { get; }

        /// <summary>The table the change applies to.</summary>
        public string TableName { get; }

        /// <summary>
        /// The whole desired table, for <see cref="CatalogChangeKind.CreateTable"/>; otherwise `null`.
        /// </summary>
        public CatalogTableDto Table { get; }

        /// <summary>
        /// The subject column: the desired column for adds and alters, the stored column for drops;
        /// `null` for non-column changes.
        /// </summary>
        public CatalogColumnDto Column { get; }

        /// <summary>
        /// The stored column an <see cref="CatalogChangeKind.AlterColumn"/> moves away from (so the
        /// emitter/log can see before → after); `null` otherwise.
        /// </summary>
        public CatalogColumnDto PreviousColumn { get; }

        /// <summary>The owning column's name for a spatial, JSON or single-column index change; `null` otherwise.</summary>
        public string ColumnName { get; }

        /// <summary>The logical index name for an index change; `null` for non-index changes.</summary>
        public string IndexName { get; }

        /// <summary>Whether a single-column index change concerns a unique index.</summary>
        public bool Unique { get; }

        /// <summary>The composite index for a composite-index change; `null` otherwise.</summary>
        public CatalogCompositeIndexDto CompositeIndex { get; }

        /// <summary>The spatial index for a spatial-index change; `null` otherwise.</summary>
        public CatalogSpatialIndexDto SpatialIndex { get; }

        /// <summary>The JSON index for a JSON-index change; `null` otherwise.</summary>
        public CatalogJsonIndexDto JsonIndex { get; }

        private CatalogChange(CatalogChangeKind kind, string tableName,
            CatalogTableDto table = null,
            CatalogColumnDto column = null, CatalogColumnDto previousColumn = null,
            string columnName = null, string indexName = null, bool unique = false,
            CatalogCompositeIndexDto compositeIndex = null,
            CatalogSpatialIndexDto spatialIndex = null,
            CatalogJsonIndexDto jsonIndex = null)
        {
            Kind = kind;
            TableName = tableName;
            Table = table;
            Column = column;
            PreviousColumn = previousColumn;
            ColumnName = columnName;
            IndexName = indexName;
            Unique = unique;
            CompositeIndex = compositeIndex;
            SpatialIndex = spatialIndex;
            JsonIndex = jsonIndex;
        }

        /// <summary>Creates a <see cref="CatalogChangeKind.CreateTable"/> change carrying the whole desired table.</summary>
        public static CatalogChange CreateTable(CatalogTableDto table)
            => new CatalogChange(CatalogChangeKind.CreateTable, table.Name, table: table);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropTable"/> change.</summary>
        public static CatalogChange DropTable(string tableName)
            => new CatalogChange(CatalogChangeKind.DropTable, tableName);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddColumn"/> change.</summary>
        public static CatalogChange AddColumn(string tableName, CatalogColumnDto column)
            => new CatalogChange(CatalogChangeKind.AddColumn, tableName, column: column);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropColumn"/> change.</summary>
        public static CatalogChange DropColumn(string tableName, CatalogColumnDto column)
            => new CatalogChange(CatalogChangeKind.DropColumn, tableName, column: column);

        /// <summary>Creates an <see cref="CatalogChangeKind.AlterColumn"/> change (previous → desired).</summary>
        public static CatalogChange AlterColumn(string tableName, CatalogColumnDto desired, CatalogColumnDto previous)
            => new CatalogChange(CatalogChangeKind.AlterColumn, tableName, column: desired, previousColumn: previous);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddGeometryColumn"/> change.</summary>
        public static CatalogChange AddGeometryColumn(string tableName, CatalogColumnDto column)
            => new CatalogChange(CatalogChangeKind.AddGeometryColumn, tableName, column: column);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropGeometryColumn"/> change.</summary>
        public static CatalogChange DropGeometryColumn(string tableName, CatalogColumnDto column)
            => new CatalogChange(CatalogChangeKind.DropGeometryColumn, tableName, column: column);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddIndex"/> change for a single column.</summary>
        public static CatalogChange AddIndex(string tableName, string columnName, bool unique)
            => new CatalogChange(CatalogChangeKind.AddIndex, tableName, columnName: columnName, unique: unique);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropIndex"/> change for a single column.</summary>
        public static CatalogChange DropIndex(string tableName, string columnName, bool unique)
            => new CatalogChange(CatalogChangeKind.DropIndex, tableName, columnName: columnName, unique: unique);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddCompositeIndex"/> change.</summary>
        public static CatalogChange AddCompositeIndex(string tableName, CatalogCompositeIndexDto index)
            => new CatalogChange(CatalogChangeKind.AddCompositeIndex, tableName, indexName: index.Name, compositeIndex: index);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropCompositeIndex"/> change.</summary>
        public static CatalogChange DropCompositeIndex(string tableName, CatalogCompositeIndexDto index)
            => new CatalogChange(CatalogChangeKind.DropCompositeIndex, tableName, indexName: index.Name, compositeIndex: index);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddSpatialIndex"/> change.</summary>
        public static CatalogChange AddSpatialIndex(string tableName, string columnName, CatalogSpatialIndexDto index)
            => new CatalogChange(CatalogChangeKind.AddSpatialIndex, tableName, columnName: columnName, indexName: index.Name, spatialIndex: index);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropSpatialIndex"/> change.</summary>
        public static CatalogChange DropSpatialIndex(string tableName, string columnName, CatalogSpatialIndexDto index)
            => new CatalogChange(CatalogChangeKind.DropSpatialIndex, tableName, columnName: columnName, indexName: index.Name, spatialIndex: index);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddJsonIndex"/> change.</summary>
        public static CatalogChange AddJsonIndex(string tableName, string columnName, CatalogJsonIndexDto index)
            => new CatalogChange(CatalogChangeKind.AddJsonIndex, tableName, columnName: columnName, indexName: index.Name, jsonIndex: index);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropJsonIndex"/> change.</summary>
        public static CatalogChange DropJsonIndex(string tableName, string columnName, CatalogJsonIndexDto index)
            => new CatalogChange(CatalogChangeKind.DropJsonIndex, tableName, columnName: columnName, indexName: index.Name, jsonIndex: index);

        /// <summary>Creates an <see cref="CatalogChangeKind.AddDynamicPropertiesTable"/> change.</summary>
        public static CatalogChange AddDynamicPropertiesTable(string tableName)
            => new CatalogChange(CatalogChangeKind.AddDynamicPropertiesTable, tableName);

        /// <summary>Creates a <see cref="CatalogChangeKind.DropDynamicPropertiesTable"/> change.</summary>
        public static CatalogChange DropDynamicPropertiesTable(string tableName)
            => new CatalogChange(CatalogChangeKind.DropDynamicPropertiesTable, tableName);
    }
}
