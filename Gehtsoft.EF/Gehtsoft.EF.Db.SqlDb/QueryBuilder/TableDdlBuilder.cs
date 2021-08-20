using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class TableDdlBuilder
    {
        protected TableDescriptor mDescriptor;
        protected SqlDbLanguageSpecifics mSpecifics;

        public TableDdlBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor)
        {
            mDescriptor = tableDescriptor;
            mSpecifics = specifics;
        }

        public virtual void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column, bool alterTable)
        {
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

        public virtual void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (NeedIndex(column))
            {
                builder.Append("\r\n");
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE INDEX ")
                    .Append(mDescriptor.Name)
                    .Append('_')
                    .Append(column.Name)
                    .Append(" ON ")
                    .Append(mDescriptor.Name)
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