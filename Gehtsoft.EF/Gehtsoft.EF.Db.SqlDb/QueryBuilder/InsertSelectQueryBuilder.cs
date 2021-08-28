using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `INSERT ... SELECT` command.
    ///
    /// Use <see cref="SqlDbConnection.GetInsertSelectQueryBuilder(TableDescriptor, SelectQueryBuilder, bool)"/> to create an instance of this object.
    /// </summary>
    public class InsertSelectQueryBuilder : AQueryBuilder
    {
        protected TableDescriptor mTable;
        protected SelectQueryBuilder mSelect;
        protected bool mIgnoreAutoIncrement;
        protected HashSet<string> mInclude;

        protected internal InsertSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false) : base(specifics)
        {
            mTable = table;
            mIgnoreAutoIncrement = ignoreAutoIncrement;
            mSelect = selectQuery;
            mInclude = null;
        }

        /// <summary>
        /// Sets the columns to be inserted by the statement.
        ///
        /// If this method isn't called, all columns will be added.
        /// </summary>
        /// <param name="columns"></param>
        public void IncludeOnly(params string[] columns)
        {
            if (mInclude == null)
                mInclude = new HashSet<string>();
            foreach (string s in columns)
                if (!mInclude.Contains(s))
                    mInclude.Add(s);
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            StringBuilder leftSide = new StringBuilder();
            TableDescriptor.ColumnInfo autoIncrement = null;
            foreach (TableDescriptor.ColumnInfo info in mTable)
            {
                if (!info.Autoincrement || mIgnoreAutoIncrement)
                {
                    if (mInclude == null || mInclude.Contains(info.Name))
                    {
                        if (leftSide.Length > 0)
                            leftSide.Append(", ");
                        leftSide.Append(info.Name);
                    }
                }

                if (info.Autoincrement)
                    autoIncrement = info;
            }
            if (!mIgnoreAutoIncrement && HasExpressionForAutoincrement && autoIncrement != null)
            {
                if (leftSide.Length > 0)
                    leftSide.Append(", ");
                leftSide.Append(autoIncrement.Name);
                mSelect.AddExpressionToResultset(ExpressionForAutoincrement(autoIncrement), System.Data.DbType.Int32);
            }
            mQuery = BuildQuery(leftSide, autoIncrement);
        }

        [DocgenIgnore]
        protected virtual string BuildQuery(StringBuilder leftSide, TableDescriptor.ColumnInfo autoIncrement)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("INSERT INTO ");
            builder.Append(mTable.Name);
            builder.Append(" ( ");
            builder.Append(leftSide);
            builder.Append(") ");
            if (string.IsNullOrEmpty(mSelect.Query))
                mSelect.PrepareQuery();
            builder.Append(mSelect.Query);
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

