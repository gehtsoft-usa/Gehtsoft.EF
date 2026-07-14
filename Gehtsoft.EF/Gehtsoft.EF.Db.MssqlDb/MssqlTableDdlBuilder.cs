using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlTableDdlBuilder : TableDdlBuilder
    {
        public MssqlTableDdlBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        public override void HandleAutoincrement(StringBuilder builder, TableDescriptor.ColumnInfo ci)
        {
            //do nothing for autoincrement
        }

        public override void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            var indexes = column.Geometry.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                SpatialIndexDefinition ix = indexes[i];
                if (!ix.HasBoundingBox)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported); // SQL Server spatial index requires a bounding box

                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE SPATIAL INDEX ")
                    .Append(mSpecifics.IndexName(column.Table.Name, ix.Name))
                    .Append(" ON ")
                    .Append(column.Table.Name)
                    .Append('(')
                    .Append(column.Name)
                    .Append(") USING GEOMETRY_GRID WITH (BOUNDING_BOX = (")
                    .Append(GeometryDdlHelper.Number(ix.MinX)).Append(", ")
                    .Append(GeometryDdlHelper.Number(ix.MinY)).Append(", ")
                    .Append(GeometryDdlHelper.Number(ix.MaxX)).Append(", ")
                    .Append(GeometryDdlHelper.Number(ix.MaxY)).Append("))");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        public override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
            {
                if (!alterTable)
                    builder.Append(", ");
                builder
                    .Append("CONSTRAINT ")
                    .Append(column.Table.Name)
                    .Append('_')
                    .Append(column.Name)
                    .Append("_fk FOREIGN KEY (")
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
