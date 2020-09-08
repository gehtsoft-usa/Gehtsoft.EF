using System;
using System.Text;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class UpdateQueryBuilder : SingleTableQueryWithWhereBuilder
    {
        private StringBuilder mFieldSet = new StringBuilder();

        public UpdateQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        public bool IsFieldsetEmpty => mFieldSet.Length == 0;

        public void AddUpdateColumn(TableDescriptor.ColumnInfo column, string parameterName = null)
        {
            if (parameterName != null)
                if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                    if (parameterName.ContainsScalar())
                        throw new ArgumentException("The query must not contain string scalars", nameof(parameterName));
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            mFieldSet.Append($"{column.Name}={mSpecifics.ParameterInQueryPrefix}{parameterName ?? column.Name}");
        }

        public void AddUpdateColumnExpression(TableDescriptor.ColumnInfo column, string rawExpression, string parameterDelimiter = "@")
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (rawExpression.ContainsScalar())
                    throw new ArgumentException("The query must not contain string scalars", nameof(rawExpression));
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            if (parameterDelimiter != null && parameterDelimiter != mSpecifics.ParameterInQueryPrefix)
                rawExpression = rawExpression.Replace(parameterDelimiter, mSpecifics.ParameterInQueryPrefix);
            mFieldSet.Append($"{column.Name}={rawExpression}");
        }

        public void AddUpdateColumnSubquery(TableDescriptor.ColumnInfo column, AQueryBuilder builder)
        {
            if (mFieldSet.Length > 0)
                mFieldSet.Append(", ");
            builder.PrepareQuery();
            mFieldSet.Append($"{column.Name}=({builder.Query})");
        }

        public void AddUpdateAllColumns()
        {
            foreach (TableDescriptor.ColumnInfo column in mDescriptor)
            {
                if (!column.PrimaryKey)
                    AddUpdateColumn(column);
            }
        }

        public void UpdateById()
        {
            Where.Property(mDescriptor.PrimaryKey).Is(CmpOp.Eq).Parameter(mDescriptor.PrimaryKey.Name);
        }

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