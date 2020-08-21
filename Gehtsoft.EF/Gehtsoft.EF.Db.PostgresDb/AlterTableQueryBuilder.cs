using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    class PostgresAlterTableQueryBuilder : AlterTableQueryBuilder
    {
        public PostgresAlterTableQueryBuilder(SqlDbLanguageSpecifics specifics) : base(specifics)
        {
        }

        protected override string GetDDL(TableDescriptor.ColumnInfo column)
        {
            StringBuilder builder = new StringBuilder();
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append($"{column.Name} {type}");
            if (column.PrimaryKey)
                builder.Append($" PRIMARY KEY");
            if (!column.Nullable)
                builder.Append($" NOT NULL");
            if (column.Unique)
                builder.Append($" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append($" DEFAULT {mSpecifics.FormatValue(column.DefaultValue)}");
            if (column.ForeignKey && column.ForeignTable != column.Table)
                builder.Append($" REFERENCES {column.ForeignTable.Name}({column.ForeignTable.PrimaryKey.Name})");
            return builder.ToString();
        }


        protected override void HandleCreateQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} ADD {GetDDL(column)}");
        }

        protected override void HandleDropQuery(TableDescriptor.ColumnInfo column)
        {
            mQueries.Add($"ALTER TABLE {mDescriptor.Name} DROP COLUMN {column.Name}");
        }
    }
}
