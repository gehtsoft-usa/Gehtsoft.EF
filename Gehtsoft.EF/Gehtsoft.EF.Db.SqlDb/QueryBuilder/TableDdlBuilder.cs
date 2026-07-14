using System.Text;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// Infrastructure class
    /// </summary>
    [DocgenIgnore]
    public class TableDdlBuilder
    {
        protected SqlDbLanguageSpecifics mSpecifics;

        public TableDdlBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        public virtual void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.Json != null && !mSpecifics.SupportsJson)
                throw new EfSqlException(EfExceptionCode.FeatureNotSupported);

            if (column.Geometry != null)
            {
                if (!mSpecifics.SupportsGeometry)
                    throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
                // The dialect renders the whole geometry column tail (type + any SRID / NOT NULL).
                builder.Append(column.Name).Append(' ').Append(mSpecifics.GeometryColumnDDL(column));
                return;
            }

            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append(column.Name).Append(' ').Append(type);
            if (column.PrimaryKey)
                builder.Append(" PRIMARY KEY");
            if (column.Autoincrement)
                HandleAutoincrement(builder, column);
            if (!column.Nullable && !column.PrimaryKey)
                builder.Append(" NOT NULL");
            if (column.Unique)
                builder.Append(" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append(" DEFAULT ").Append(mSpecifics.FormatValue(column.DefaultValue));
        }

        public virtual void HandleAutoincrement(StringBuilder builder, TableDescriptor.ColumnInfo ci)
        {
            builder.Append(" AUTOINCREMENT");
        }

        public virtual void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
            {
                if (!alterTable)
                    builder.Append(", ");
                builder
                    .Append("FOREIGN KEY (")
                    .Append(column.Name)
                    .Append(") REFERENCES ")
                    .Append(column.ForeignTable.Name)
                    .Append('(')
                    .Append(column.ForeignTable.PrimaryKey.Name)
                    .Append(')');
            }
        }

        public virtual bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return column.Sorted || (column.ForeignKey && column.ForeignTable == column.Table) || (column.ForeignKey && !mSpecifics.IndexForFKCreatedAutomatically);
        }

        /// <summary>
        /// Whether the column must be omitted from the inline CREATE TABLE column list (it is added
        /// afterwards). SpatiaLite geometry columns are added post-create via <c>AddGeometryColumn</c>.
        /// </summary>
        public virtual bool SkipInlineColumn(TableDescriptor.ColumnInfo column) => false;

        /// <summary>
        /// Emits the post-create statements for a geometry column (spatial index, and — on SpatiaLite —
        /// the <c>AddGeometryColumn</c> registration). The default is a no-op; spatial-capable drivers
        /// override it.
        /// </summary>
        public virtual void HandleGeometryAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
        }

        public virtual void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (column.Geometry != null)
            {
                HandleGeometryAfterQuery(builder, column);
                return;
            }

            if (NeedIndex(column))
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE INDEX ")
                    .Append(mSpecifics.IndexName(column.Table.Name, column.Name))
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
    }
}