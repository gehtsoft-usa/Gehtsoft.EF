using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqliteDb
{
    internal class SqliteTableDdlBuilder : TableDdlBuilder
    {
        public SqliteTableDdlBuilder(SqlDb.SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        // SpatiaLite geometry columns are not declared inline; they are added post-create.
        public override bool SkipInlineColumn(TableDescriptor.ColumnInfo column) => column.Geometry != null;

        public override void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            GeometryColumnMetadata geo = column.Geometry;
            string table = column.Table.Name;
            string subtype = GeometryDdlHelper.SubtypeName(geo.Subtype).ToUpperInvariant();
            string dimension = GeometryDdlHelper.DimensionToken(geo.HasZ, geo.HasM);
            string notNull = geo.Nullable ? "0" : "1";

            builder.Append("\r\n");
            builder.Append(mSpecifics.PreQueryInBlock);
            builder
                .Append("SELECT AddGeometryColumn('").Append(table).Append("', '").Append(column.Name).Append("', ")
                .Append(geo.Srid).Append(", '").Append(subtype).Append("', '").Append(dimension).Append("', ").Append(notNull).Append(')');
            if (mSpecifics.TerminateWithSemicolon)
                builder.Append(';');
            builder.Append(mSpecifics.PostQueryInBlock);

            var indexes = geo.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder.Append("SELECT CreateSpatialIndex('").Append(table).Append("', '").Append(column.Name).Append("')");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        public override void CollectRegisterGeometryColumn(List<string> queries, TableDescriptor.ColumnInfo column)
        {
            GeometryColumnMetadata geo = column.Geometry;
            string subtype = GeometryDdlHelper.SubtypeName(geo.Subtype).ToUpperInvariant();
            string dimension = GeometryDdlHelper.DimensionToken(geo.HasZ, geo.HasM);
            string notNull = geo.Nullable ? "0" : "1";
            queries.Add(
                $"SELECT AddGeometryColumn('{column.Table.Name}', '{column.Name}', {geo.Srid}, '{subtype}', '{dimension}', {notNull})");
        }

        public override void CollectUnregisterGeometryColumn(List<string> queries, TableDescriptor.ColumnInfo column)
        {
            queries.Add($"SELECT DiscardGeometryColumn('{column.Table.Name}', '{column.Name}')");
        }

        public override void CollectCreateSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            queries.Add($"SELECT CreateSpatialIndex('{column.Table.Name}', '{column.Name}')");
        }

        public override void CollectDropSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            queries.Add($"SELECT DisableSpatialIndex('{column.Table.Name}', '{column.Name}')");
            queries.Add($"DROP TABLE IF EXISTS idx_{column.Table.Name}_{column.Name}");
        }

        public override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            base.HandleColumnDDL(builder, column, alterTable);
            if (alterTable && column.ForeignKey && column.ForeignTable != column.Table)
            {
                builder
                    .Append(" REFERENCES ")
                    .Append(column.ForeignTable.Name)
                    .Append('(')
                    .Append(column.ForeignTable.PrimaryKey.Name)
                    .Append(')');
            }
        }

        public override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && alterTable)
                return;
            base.HandlePostfixDDL(builder, column, alterTable);
        }

        public override bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return base.NeedIndex(column) || column.ForeignKey;
        }
    }
}
