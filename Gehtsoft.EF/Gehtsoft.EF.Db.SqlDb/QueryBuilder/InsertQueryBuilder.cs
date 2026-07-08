using System.Collections.Generic;
using System.Linq;
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

        protected HashSet<string> mInclude;
        protected Dictionary<string, string> mParameterNames;

        /// <summary>
        /// Whether the statement reads the generated autoincrement value back (default `true` - the
        /// historical behavior). Set to `false` to insert into an autoincrement table *without* the
        /// read-back: the database still generates the id, but the statement omits the driver's
        /// read-back tail (e.g. `; SELECT LAST_INSERT_ID();`, `RETURNING id INTO :id`). Used when the
        /// generated id is not needed and the read-back would break a combined multi-statement command.
        /// </summary>
        public bool ReturnAutoincrement { get; set; } = true;

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

        /// <summary>
        /// Overrides the parameter name used for specific columns.
        ///
        /// By default a column's parameter has the same name as the column. Pass
        /// `(column, parameter)` pairs to use a different parameter name for those columns - e.g.
        /// to combine several inserts in one command with unique (or intentionally shared)
        /// parameter names. Columns not listed keep the default (column-name) parameter.
        /// </summary>
        /// <param name="names">The `(column, parameter)` name pairs.</param>
        public void SetParameterNames(params (string Column, string Parameter)[] names)
        {
            if (names == null || names.Length == 0)
                return;
            if (mParameterNames == null)
                mParameterNames = new Dictionary<string, string>();
            foreach ((string column, string parameter) in names)
                mParameterNames[column] = parameter;
        }

        protected string ParameterNameFor(string column)
            => mParameterNames != null && mParameterNames.TryGetValue(column, out string parameter) ? parameter : column;

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

                if (mInclude != null && !mInclude.Contains(info.Name))
                    continue;

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
                    rightSide.Append(ParameterNameFor(info.Name));
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
            if (leftSide.Length == 0)
            {
                // no columns to insert (e.g. an entity whose only column is the autoincrement id)
                builder.Append(' ').Append(EmptyInsertClause);
            }
            else
            {
                builder.Append(" ( ");
                builder.Append(leftSide);
                builder.Append(") VALUES (");
                builder.Append(rightSide);
                builder.Append(" ) ");
            }
            return builder.ToString();
        }

        /// <summary>
        /// The clause used to insert a row with no explicit column values (all defaults /
        /// autoincrement). The SQL-standard form works on most engines; override where it doesn't.
        /// </summary>
        [DocgenIgnore]
        protected virtual string EmptyInsertClause => "DEFAULT VALUES";

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

