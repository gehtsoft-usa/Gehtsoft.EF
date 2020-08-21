﻿using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    class OracleCreateTableBuilder : CreateTableBuilder
    {
        public OracleCreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {

        }

        protected override void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            base.HandleAfterQuery(builder, column);

            if (column.Autoincrement)
            {
                builder.Append(mSpecifics.PreQueryInBlock);
                builder.Append($"CREATE SEQUENCE {mDescriptor.Name}_{column.Name} START WITH 1 INCREMENT BY 1 MINVALUE 1");
                if (mSpecifics.TerminateWithSemicolon)
                    builder.Append(';');
                builder.Append(mSpecifics.PostQueryInBlock);
            }
        }

        protected override void HandleColumnDDL(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            string type = mSpecifics.TypeName(column.DbType, column.Size, column.Precision, column.Autoincrement);
            builder.Append($"{column.Name} {type}");
            if (column.PrimaryKey)
                builder.Append($" PRIMARY KEY");
            if (!column.Nullable && column.DefaultValue == null)
                builder.Append($" NOT NULL");
            if (column.Unique)
                builder.Append($" UNIQUE");
            if (column.DefaultValue != null)
                builder.Append($" DEFAULT {mSpecifics.FormatValue(column.DefaultValue)}");

        }
    }
}
