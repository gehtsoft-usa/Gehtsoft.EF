using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleTableDdlBuilder : TableDdlBuilder
    {
        public OracleTableDdlBuilder(SqlDb.SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        public override void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            GeometryColumnMetadata geo = column.Geometry;
            string tableName = column.Table.Name.ToUpperInvariant();
            string columnName = column.Name.ToUpperInvariant();
            var indexes = geo.Indexes;
            for (int i = 0; i < indexes.Count; i++)
            {
                SpatialIndexDefinition ix = indexes[i];
                if (!ix.HasBoundingBox)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported); // Oracle needs dimension bounds

                string tolerance = GeometryDdlHelper.Number(ix.Tolerance);
                // Emitted inside EXECUTE IMMEDIATE '...', so every single quote is doubled.
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("INSERT INTO USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES (''")
                    .Append(tableName).Append("'', ''").Append(columnName).Append("'', SDO_DIM_ARRAY(SDO_DIM_ELEMENT(''X'', ")
                    .Append(GeometryDdlHelper.Number(ix.MinX)).Append(", ").Append(GeometryDdlHelper.Number(ix.MaxX)).Append(", ").Append(tolerance)
                    .Append("), SDO_DIM_ELEMENT(''Y'', ")
                    .Append(GeometryDdlHelper.Number(ix.MinY)).Append(", ").Append(GeometryDdlHelper.Number(ix.MaxY)).Append(", ").Append(tolerance)
                    .Append(")), ").Append(geo.Srid).Append(")");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);

                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE INDEX ")
                    .Append(mSpecifics.IndexName(column.Table.Name, ix.Name))
                    .Append(" ON ")
                    .Append(column.Table.Name)
                    .Append('(')
                    .Append(column.Name)
                    .Append(") INDEXTYPE IS MDSYS.SPATIAL_INDEX_V2");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        // ALTER-time primitives (catalogue). Statements run bare (not inside EXECUTE IMMEDIATE), so single
        // quotes — unlike the create path, which lives inside a PL/SQL block and doubles them. Oracle keeps
        // the SDO metadata coupled to the index, mirroring the create path.
        public override void CollectCreateSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            if (!index.HasBoundingBox)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported); // Oracle needs dimension bounds

            string tableName = column.Table.Name.ToUpperInvariant();
            string columnName = column.Name.ToUpperInvariant();
            string tolerance = GeometryDdlHelper.Number(index.Tolerance);

            queries.Add(
                "INSERT INTO USER_SDO_GEOM_METADATA (TABLE_NAME, COLUMN_NAME, DIMINFO, SRID) VALUES ('" +
                tableName + "', '" + columnName + "', SDO_DIM_ARRAY(SDO_DIM_ELEMENT('X', " +
                GeometryDdlHelper.Number(index.MinX) + ", " + GeometryDdlHelper.Number(index.MaxX) + ", " + tolerance +
                "), SDO_DIM_ELEMENT('Y', " +
                GeometryDdlHelper.Number(index.MinY) + ", " + GeometryDdlHelper.Number(index.MaxY) + ", " + tolerance +
                ")), " + column.Geometry.Srid + ")");

            queries.Add(
                $"CREATE INDEX {mSpecifics.IndexName(column.Table.Name, index.Name)} ON {column.Table.Name}({column.Name}) INDEXTYPE IS MDSYS.SPATIAL_INDEX_V2");
        }

        public override void CollectDropSpatialIndex(List<string> queries, TableDescriptor.ColumnInfo column, SpatialIndexDefinition index)
        {
            queries.Add($"DROP INDEX {mSpecifics.IndexName(column.Table.Name, index.Name)}");
            queries.Add(
                "DELETE FROM USER_SDO_GEOM_METADATA WHERE TABLE_NAME = '" +
                column.Table.Name.ToUpperInvariant() + "' AND COLUMN_NAME = '" +
                column.Name.ToUpperInvariant() + "'");
        }

        public override void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            base.HandleAfterQuery(builder, column);

            if (column.Autoincrement)
            {
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE SEQUENCE ")
                    .Append(column.Table.Name)
                    .Append('_')
                    .Append(column.Name)
                    .Append(" START WITH 1 INCREMENT BY 1 MINVALUE 1");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
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
            if (!column.Nullable && column.DefaultValue == null)
                builder.Append(" NOT NULL");
            if (column.Unique)
                builder.Append(" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append(" DEFAULT ").Append(mSpecifics.FormatValue(column.DefaultValue));
        }

        public override bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return (column.Sorted || column.ForeignKey) && !column.Unique && !column.PrimaryKey;
        }
    }
}
