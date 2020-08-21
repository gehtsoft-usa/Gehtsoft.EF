using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqliteDb
{
    class SqliteCreateTableBuilder : CreateTableBuilder
    {
        public SqliteCreateTableBuilder(SqliteDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            
        }

        protected override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append($"{column.Name} {type}");
            if (column.PrimaryKey)
                builder.Append($" PRIMARY KEY");
            if (column.Autoincrement)
                builder.Append($" AUTOINCREMENT");
            if (!column.Nullable)
                builder.Append($" NOT NULL");
            if (column.Unique)
                builder.Append($" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append($" DEFAULT {mSpecifics.FormatValue(column.DefaultValue)}");

        }

        protected override bool NeedIndex(TableDescriptor.ColumnInfo column)
        {
            return base.NeedIndex(column) || column.ForeignKey;
        }
    }
}
