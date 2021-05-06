using System.Collections.Generic;
using System.Text;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class InsertQueryBuilder : AQueryBuilder
    {
        protected TableDescriptor mTable;
        protected bool mIgnoreAutoIncrement;

        public InsertQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, bool ignoreAutoIncrement = false) : base(specifics)
        {
            mTable = table;
            mIgnoreAutoIncrement = ignoreAutoIncrement;
        }

        public override void PrepareQuery()
        {
            StringBuilder leftSide = new StringBuilder();
            StringBuilder rightSide = new StringBuilder();
            bool first = true;
            TableDescriptor.ColumnInfo autoIncrement = null;
            foreach (TableDescriptor.ColumnInfo info in mTable)
            {
                if (info.Autoincrement && !HasExpressionForAutoincrement && !mIgnoreAutoIncrement)
                {
                    autoIncrement = info;
                    continue;
                }

                if (first)
                    first = false;
                else
                {
                    leftSide.Append(", ");
                    rightSide.Append(", ");
                }

                leftSide.Append(info.Name);
                if (info.Autoincrement && !mIgnoreAutoIncrement)
                {
                    autoIncrement = info;
                    rightSide.Append(ExpressionForAutoincrement(info));
                }
                else
                {
                    rightSide.Append(mSpecifics.ParameterInQueryPrefix);
                    rightSide.Append(info.Name);
                }
            }
            mQuery = BuildQuery(leftSide, rightSide, autoIncrement);
        }

        protected virtual string BuildQuery(StringBuilder leftSide, StringBuilder rightSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("INSERT INTO ");
            builder.Append(mTable.Name);
            builder.Append(" ( ");
            builder.Append(leftSide);
            builder.Append(") VALUES (");
            builder.Append(rightSide);
            builder.Append(" ) ");
            return builder.ToString();
        }

        protected virtual bool HasExpressionForAutoincrement => false;

        protected virtual string ExpressionForAutoincrement(TableDescriptor.ColumnInfo autoIncrement)
        {
            return null;
        }

        protected string mQuery;

        public override string Query => mQuery;
    }
}

