using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlTableDdlBuilder : TableDdlBuilder
    {
        public MysqlTableDdlBuilder(SqlDb.SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        public override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.Geometry != null)
            {
                if (!mSpecifics.SupportsGeometry)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                builder.Append(column.Name).Append(' ').Append(mSpecifics.GeometryColumnDDL(column));
                return;
            }

            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append(column.Name).Append(' ').Append(type);
            if (column.PrimaryKey)
                builder.Append(" PRIMARY KEY");
            if (column.Autoincrement)
                builder.Append(" AUTO_INCREMENT");
            if (!column.Nullable)
                builder.Append(" NOT NULL");
            if (column.Unique)
                builder.Append(" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append(" DEFAULT ").Append(mSpecifics.FormatValue(column.DefaultValue));
        }

        public override void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            var indexes = column.Geometry.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE SPATIAL INDEX ")
                    .Append(mSpecifics.IndexName(column.Table.Name, indexes[i].Name))
                    .Append(" ON ")
                    .Append(column.Table.Name)
                    .Append('(')
                    .Append(column.Name)
                    .Append(')');
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        public override void CollectCreateSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            queries.Add($"CREATE SPATIAL INDEX {mSpecifics.IndexName(column.Table.Name, index.Name)} ON {column.Table.Name}({column.Name})");
        }

        public override void CollectDropSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            queries.Add($"DROP INDEX {mSpecifics.IndexName(column.Table.Name, index.Name)} ON {column.Table.Name}");
        }

        public override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
            {
                if (!alterTable)
                    builder.Append(", ");

                builder.Append("CONSTRAINT fk_")
                    .Append(column.Table.Name)
                    .Append('_')
                    .Append(column.Name)
                    .Append(" FOREIGN KEY (")
                    .Append(column.Name)
                    .Append(") REFERENCES ")
                    .Append(column.ForeignTable.Name)
                    .Append('(')
                    .Append(column.ForeignTable.PrimaryKey.Name)
                    .Append(')');
            }
        }
    }
}
