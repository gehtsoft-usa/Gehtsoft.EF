﻿using System.Text;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleTableDdlBuilder : TableDdlBuilder
    {
        public OracleTableDdlBuilder(SqlDb.SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
        }

        public override void HandleAfterQuery(StringBuilder builder, TableDescriptor.ColumnInfo column)
        {
            base.HandleAfterQuery(builder, column);

            if (column.Autoincrement)
            {
                builder.Append(mSpecifics.PreQueryInBlock);
                builder
                    .Append("CREATE SEQUENCE ")
                    .Append(mDescriptor.Name)
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
