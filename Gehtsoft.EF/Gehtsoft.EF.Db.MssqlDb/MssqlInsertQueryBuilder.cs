using System;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    internal class MssqlInsertQueryBuilder : InsertQueryBuilder
    {
        private readonly bool mHasAutoId = false;

        public MssqlInsertQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor descriptor, bool ignoreAutoincrement) : base(specifics, descriptor, ignoreAutoincrement)
        {
            if (ignoreAutoincrement)
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

        protected override string BuildQuery(StringBuilder leftSide, StringBuilder rightSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            if (mHasAutoId && mIgnoreAutoIncrement)
            {
                StringBuilder builder = new StringBuilder();
                builder
                    .Append("SET IDENTITY_INSERT ")
                    .Append(mTable.Name)
                    .AppendLine(" ON;")
                    .Append(base.BuildQuery(leftSide, rightSide, autoIncrement))
                    .AppendLine(";")
                    .Append("SET IDENTITY_INSERT ").Append(mTable.Name).AppendLine(" OFF;");
                return builder.ToString();
            }
            else
            {
                if (autoIncrement == null)
                    return base.BuildQuery(leftSide, rightSide, autoIncrement);
                else
                {
                    StringBuilder builder = new StringBuilder();
                    builder.Append("INSERT INTO ");
                    builder.Append(mTable.Name);
                    builder.Append(" ( ");
                    builder.Append(leftSide);
                    builder.Append(") ");
                    builder.Append(" OUTPUT INSERTED.");
                    builder.Append(autoIncrement.Name);
                    builder.Append(" VALUES (");
                    builder.Append(rightSide);
                    builder.Append(" ) ");
                    return builder.ToString();
                }
            }
        }
    }
}
