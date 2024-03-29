﻿using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleInsertQueryBuilder : InsertQueryBuilder
    {
        public OracleInsertQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, bool ignoreAutoIncrement) : base(specifics, descriptor, ignoreAutoIncrement)
        {
        }

        protected override string BuildQuery(StringBuilder leftSide, StringBuilder rightSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, rightSide, autoIncrement));
            if (autoIncrement != null)
            {
                builder.Append(" RETURNING ");
                builder.Append(autoIncrement.Name);
                builder.Append(" INTO :");
                builder.Append(autoIncrement.Name);
            }
            return builder.ToString();
        }

        protected override bool HasExpressionForAutoincrement => true;

        protected override string ExpressionForAutoincrement(TableDescriptor.ColumnInfo autoIncrement)
        {
            return $"{mTable.Name}_{autoIncrement.Name}.nextval";
        }
    }
}
