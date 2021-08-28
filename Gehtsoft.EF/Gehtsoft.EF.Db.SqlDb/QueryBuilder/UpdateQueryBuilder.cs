using System;
using System.Text;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The builder for `UPDATE` query.
    ///
    /// Use <see cref="SqlDbConnection.GetUpdateQueryBuilder(TableDescriptor)"/> to get an instance of the builder.
    ///
    /// You can also use <see cref="UpdateQueryToTypeBinder"/> to bind entity properties to the parameters of the query.
    /// </summary>
    public class UpdateQueryBuilder : SingleTableQueryWithWhereBuilder
    {
        private readonly StringBuilder mFieldSet = new StringBuilder();

        protected internal UpdateQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        /// <summary>
        /// Returns the flag indicating that there is not fields defined yet.
        /// </summary>
        public bool IsFieldsetEmpty => mFieldSet.Length == 0;

        /// <summary>
        /// Adds a column to be updated by the query. The value is set via parameter.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="parameterName">The parameter name. If the value is not set, the parameter name will be the same as the name of the column</param>
        public void AddUpdateColumn(TableDescriptor.ColumnInfo column, string parameterName = null)
        {
            if (parameterName != null)
                if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                    if (parameterName.ContainsScalar())
                        throw new ArgumentException("The query must not contain string scalars", nameof(parameterName));
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            mFieldSet
                .Append(column.Name)
                .Append('=')
                .Append(mSpecifics.ParameterInQueryPrefix)
                .Append(parameterName ?? column.Name);
        }

        /// <summary>
        /// Adds a column to be updated by the query. The value is set via raw SQL expression.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="rawExpression"></param>
        /// <param name="parameterDelimiter"></param>
        public void AddUpdateColumnExpression(TableDescriptor.ColumnInfo column, string rawExpression, string parameterDelimiter = "@")
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (rawExpression.ContainsScalar())
                    throw new ArgumentException("The query must not contain string scalars", nameof(rawExpression));
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            if (parameterDelimiter != null && parameterDelimiter != mSpecifics.ParameterInQueryPrefix)
                rawExpression = rawExpression.Replace(parameterDelimiter, mSpecifics.ParameterInQueryPrefix);
            mFieldSet.Append(column.Name).Append('=').Append(rawExpression);
        }

        /// <summary>
        /// Adds a column to be updated by the query. The value is set via SQL `SELECT`.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="builder"></param>
        public void AddUpdateColumnSubquery(TableDescriptor.ColumnInfo column, AQueryBuilder builder)
        {
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            builder.PrepareQuery();
            mFieldSet.Append(column.Name).Append("=(").Append(builder.Query).Append(')');
        }

        /// <summary>
        /// Adds all columns of the entity to be updated.
        ///
        /// All columns except the primary key will be added to be updated using parameter values.
        /// The parameter names will be equal to the name of corresponding columns.
        /// </summary>
        public void AddUpdateAllColumns()
        {
            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                if (!column.PrimaryKey)
                    AddUpdateColumn(column);
            }
        }

        /// <summary>
        /// Sets the condition of the query to update the value by ID.
        ///
        /// The parameter name to set the ID will be the same as the primary key column name.
        /// </summary>
        public void UpdateById()
        {
            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("UPDATE ");
            builder.Append(mDescriptor.Name);
            builder.Append(" SET ");
            builder.Append(mFieldSet);
            if (!Where.IsEmpty)
            {
                builder.Append(" WHERE ");
                builder.Append(Where);
            }
            mQuery = builder.ToString();
        }
    }
}