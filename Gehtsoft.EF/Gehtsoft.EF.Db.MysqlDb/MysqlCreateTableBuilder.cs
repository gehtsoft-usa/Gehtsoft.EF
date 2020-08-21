using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    class MysqlCreateTableBuilder : CreateTableBuilder
    {
        public MysqlCreateTableBuilder(MysqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {

        }

        protected override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append($"{column.Name} {type}");
            if (column.PrimaryKey)
                builder.Append($" PRIMARY KEY");
            if (column.Autoincrement)
                builder.Append($" AUTO_INCREMENT");
            if (!column.Nullable)
                builder.Append($" NOT NULL");
            if (column.Unique)
                builder.Append($" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append($" DEFAULT {mSpecifics.FormatValue(column.DefaultValue)}");

        }

        protected override void HandlePostfixDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            if (column.ForeignKey && column.ForeignTable != column.Table)
                builder.Append($", CONSTRAINT fk_{column.Table.Name}_{column.Name} FOREIGN KEY ({column.Name}) REFERENCES {column.ForeignTable.Name}({column.ForeignTable.PrimaryKey.Name})");
        }

    }
}
