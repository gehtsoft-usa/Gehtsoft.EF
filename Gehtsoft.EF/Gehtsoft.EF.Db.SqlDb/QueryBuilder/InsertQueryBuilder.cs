using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `INSERT ... VALUES` command.
    ///
    /// Use <see cref="SqlDbConnection.GetInsertQueryBuilder(TableDescriptor, bool)"/> to create an instance of this object.
    ///
    /// You can also use <see cref="UpdateQueryToTypeBinder"/> to bind entity properties to the parameters of the query.
    /// </summary>
    public class InsertQueryBuilder : AQueryBuilder
    {
        protected TableDescriptor mTable;
        protected bool mIgnoreAutoIncrement;

        [DocgenIgnore]
        protected internal InsertQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, bool ignoreAutoIncrement = false) : base(specifics)
        {
            mTable = table;
            mIgnoreAutoIncrement = ignoreAutoIncrement;
        }

        [DocgenIgnore]
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

        [DocgenIgnore]
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

        [DocgenIgnore]
        protected virtual bool HasExpressionForAutoincrement => false;

        [DocgenIgnore]
        protected virtual string ExpressionForAutoincrement(TableDescriptor.ColumnInfo autoIncrement)
        {
            return null;
        }

        [DocgenIgnore]
        protected string mQuery;

        [DocgenIgnore]
        public override string Query => mQuery;
    }
}

