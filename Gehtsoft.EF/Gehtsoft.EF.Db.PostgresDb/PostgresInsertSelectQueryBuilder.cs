﻿using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresInsertSelectQueryBuilder : InsertSelectQueryBuilder
    {
        private readonly bool mHasAutoId;

        public PostgresInsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics, descriptor, selectQuery, ignoreAutoIncrement)
        {
            if (ignoreAutoIncrement)
            {
                bool hasAutoId = false;
                foreach (TableDescriptor.ColumnInfo column in descriptor)
                {
                    if (column.Autoincrement && column.PrimaryKey)
                    {
                        hasAutoId = true;
                        break;
                    }
                }

                mHasAutoId = hasAutoId;
            }
        }

        protected override string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(base.BuildQuery(leftSide, autoIncrement));
            if (autoIncrement != null)
                builder
                    .Append("; SELECT last_value from ")
                    .Append(mTable.Name)
                    .Append('_')
                    .Append(autoIncrement.Name)
                    .Append("_seq;");
            else if (mHasAutoId)
                builder
                    .Append("; SELECT pg_catalog.setval(pg_get_serial_sequence('")
                    .Append(mTable.Name)
                    .Append("', '")
                    .Append(mTable.PrimaryKey.Name.ToLower())
                    .Append("'), (SELECT MAX(")
                    .Append(mTable.PrimaryKey.Name.ToLower())
                    .Append(") FROM ")
                    .Append(mTable.Name)
                    .Append("));");
            return builder.ToString();
        }
    }
}
