using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlCreateTableBuilder : CreateTableBuilder
    {
        public MysqlCreateTableBuilder(MysqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        protected override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
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

        protected override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
                builder.Append(", CONSTRAINT fk_")
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
