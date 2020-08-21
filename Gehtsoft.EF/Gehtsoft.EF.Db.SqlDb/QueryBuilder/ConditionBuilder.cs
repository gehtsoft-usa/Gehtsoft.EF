using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public interface IConditionBuilderInfoProvider
    {
        SqlDbLanguageSpecifics Specifics { get; }

        string GetAlias(TableDescriptor.ColumnInfo columnInfo, QueryBuilderEntity queryEntity);
    }

    public class SingleConditionBuilder
    {
        private readonly ConditionBuilder mBuilder;

        internal ConditionBuilder Builder => mBuilder;

        private readonly LogOp mLogOp;
        private string mLeftSide;
        private CmpOp? mCmpOp = null;
        private string mRightSide;

        internal string Right
        {
            get => mRightSide;
            set => mRightSide = value;
        }

        internal string Left
        {
            get => mLeftSide;
            set => mLeftSide = value;
        }

        internal SingleConditionBuilder(ConditionBuilder builder, LogOp logOp)
        {
            mBuilder = builder;
            mLogOp = logOp;
        }

        public SingleConditionBuilder Raw(string rawExpression)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (rawExpression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(rawExpression));

            if (mCmpOp == null)
                mLeftSide = rawExpression;
            else
            {
                mRightSide = rawExpression;
                Push();
            }
            return this;
        }

        public SingleConditionBuilder Property(AggFn aggFn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => Raw(mBuilder.PropertyName(aggFn, entity, columnInfo));

        public SingleConditionBuilder Property(AggFn aggFn, TableDescriptor.ColumnInfo columnInfo) => Raw(mBuilder.PropertyName(aggFn, columnInfo));

        public SingleConditionBuilder Property(QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => Raw(mBuilder.PropertyName(entity, columnInfo));

        public SingleConditionBuilder Property(TableDescriptor.ColumnInfo columnInfo) => Raw(mBuilder.PropertyName(columnInfo));

        public SingleConditionBuilder Parameter(string name) => Raw(mBuilder.Parameter(name));

        public SingleConditionBuilder Parameters(params string[] names) => Raw(mBuilder.Parameters(names));

        public SingleConditionBuilder Query(AQueryBuilder builder) => Raw(mBuilder.Query(builder));

        public SingleConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            if (op == CmpOp.IsNull || op == CmpOp.NotNull)
                Push();

            return this;
        }

        private void Push()
        {
            mBuilder.Add(mLogOp, mLeftSide, (CmpOp)mCmpOp, mRightSide);
        }
    }

    public static class SingleConditionBuilderExtension
    {
        public static SingleConditionBuilder Eq(this SingleConditionBuilder builder) => builder.Is(CmpOp.Eq);

        public static SingleConditionBuilder Neq(this SingleConditionBuilder builder) => builder.Is(CmpOp.Neq);

        public static SingleConditionBuilder Le(this SingleConditionBuilder builder) => builder.Is(CmpOp.Le);

        public static SingleConditionBuilder Ls(this SingleConditionBuilder builder) => builder.Is(CmpOp.Ls);

        public static SingleConditionBuilder Ge(this SingleConditionBuilder builder) => builder.Is(CmpOp.Ge);

        public static SingleConditionBuilder Gt(this SingleConditionBuilder builder) => builder.Is(CmpOp.Gt);

        public static SingleConditionBuilder Like(this SingleConditionBuilder builder) => builder.Is(CmpOp.Like);

        public static SingleConditionBuilder In(this SingleConditionBuilder builder) => builder.Is(CmpOp.In);

        public static SingleConditionBuilder NotIn(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        public static SingleConditionBuilder Exists(this SingleConditionBuilder builder) => builder.Is(CmpOp.Exists);

        public static SingleConditionBuilder NotExists(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotExists);

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, int value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, double value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, string value) => builder.Raw($"\"{value}\"");

        public static SingleConditionBuilder Reference(this SingleConditionBuilder builder, ConditionEntityQueryBase.InQueryName reference) => builder.Raw(reference.Alias);

        internal static SingleConditionBuilder Wrap(this SingleConditionBuilder builder, SqlFunctionId function)
        {
            if (builder.Right != null)
                throw new InvalidOperationException("Wrap may be applied on the left side argument only");

            if (builder.Left != null)
                builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Left });
            else if (function == SqlFunctionId.Count)
                builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, Array.Empty<string>());
            else
                throw new InvalidOperationException("Nothing to process with a function");
            return builder;
        }

        public static SingleConditionBuilder Sum(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Sum);

        public static SingleConditionBuilder Avg(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Avg);

        public static SingleConditionBuilder Min(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Min);

        public static SingleConditionBuilder Max(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Max);

        public static SingleConditionBuilder Count(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Count);

        public static SingleConditionBuilder Trim(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Trim);

        public static SingleConditionBuilder Upper(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Upper);

        public static SingleConditionBuilder Lower(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Lower);

        public static SingleConditionBuilder Abs(this SingleConditionBuilder builder) => builder.Wrap(SqlFunctionId.Abs);
    }

    public class ConditionBuilder : IOpBracketAcceptor
    {
        private readonly StringBuilder mWhere = new StringBuilder();
        private bool mConditionStarted = false;
        public IConditionBuilderInfoProvider InfoProvider { get; protected set; }
        public bool IsEmpty => mWhere.Length == 0;

        public ConditionBuilder(IConditionBuilderInfoProvider infoProvider)
        {
            InfoProvider = infoProvider;
        }

        public virtual void Add(LogOp logOp, string rawExpression)
        {
            if (string.IsNullOrWhiteSpace(rawExpression))
                return;

            if (!mConditionStarted)
            {
                if ((logOp & LogOp.Not) == LogOp.Not)
                    mWhere.Append(InfoProvider.Specifics.GetLogOp(LogOp.Not));
            }
            else
                mWhere.Append(InfoProvider.Specifics.GetLogOp(logOp));

            mWhere.Append(rawExpression);
            mWhere.Append(InfoProvider.Specifics.CloseLogOp(logOp));
            mConditionStarted = true;
        }

        public virtual void Add(LogOp logOp, string leftSide, CmpOp cmpOp, string rightSide) => Add(logOp, InfoProvider.Specifics.GetOp(cmpOp, leftSide, rightSide));

        public virtual void Add(string leftSide, CmpOp cmpOp, string rightSide) => Add(LogOp.And, InfoProvider.Specifics.GetOp(cmpOp, leftSide, rightSide));

        public virtual string PropertyName(QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnDescriptor) => columnDescriptor != null ? InfoProvider.GetAlias(columnDescriptor, entity) : null;

        public virtual string PropertyName(AggFn aggFn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.Specifics.GetAggFn(aggFn, InfoProvider.GetAlias(columnDescriptor, entity));

        public virtual string PropertyName(TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.GetAlias(columnDescriptor, null);

        public virtual string PropertyName(AggFn aggFn, TableDescriptor.ColumnInfo columnDescriptor) => InfoProvider.Specifics.GetAggFn(aggFn, InfoProvider.GetAlias(columnDescriptor, null));

        public virtual string Parameter(string parameterName)
        {
            if (parameterName == null)
                return null;

            string prefix = InfoProvider.Specifics.ParameterInQueryPrefix;
            if (!string.IsNullOrEmpty(prefix) && !parameterName.StartsWith(prefix))
                parameterName = prefix + parameterName;
            return parameterName;
        }

        public virtual string Parameters(string[] parameterNames)
        {
            StringBuilder parameterName = new StringBuilder();
            foreach (string s in parameterNames)
            {
                if (parameterName.Length > 0)
                    parameterName.Append(',');

                parameterName.Append(Parameter(s));
            }
            return parameterName.ToString();
        }

        public virtual string Query(AQueryBuilder queryBuilder)
        {
            if (queryBuilder.Query == null)
                queryBuilder.PrepareQuery();
            return $"({queryBuilder.Query})";
        }

        public virtual OpBracket AddGroup(LogOp logOp = LogOp.And)
        {
            if (!mConditionStarted)
            {
                if ((logOp & LogOp.Not) == LogOp.Not)
                    mWhere.Append(InfoProvider.Specifics.GetLogOp(LogOp.Not));
            }
            else
                mWhere.Append(InfoProvider.Specifics.GetLogOp(logOp));

            mConditionStarted = false;
            mWhere.Append("(");
            return new OpBracket(this, logOp);
        }

        public virtual void BracketClosed(OpBracket op)
        {
            if (mConditionStarted == false)
                throw new EfSqlException(EfExceptionCode.WhereBracketIsEmpty);
            mWhere.Append(")");
            mWhere.Append(InfoProvider.Specifics.CloseLogOp(op.LogOp));
        }

        public override string ToString() => mWhere.ToString();

        public SingleConditionBuilder Add(LogOp logOp) => new SingleConditionBuilder(this, logOp);
    }

    public static class ConditionBuilderExtension
    {
        public static SingleConditionBuilder And(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.And);

        public static SingleConditionBuilder Or(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.Or);

        public static SingleConditionBuilder Property(this ConditionBuilder builder, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(entity, columnInfo));

        public static SingleConditionBuilder Property(this ConditionBuilder builder, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(columnInfo));

        public static SingleConditionBuilder Reference(this ConditionBuilder builder, ConditionEntityQueryBase.InQueryName reference) => builder.Raw(reference.Alias);

        public static SingleConditionBuilder Exists(this ConditionBuilder builder)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            return singleBuilder.Exists();
        }

        public static SingleConditionBuilder NotExists(this ConditionBuilder builder)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            return singleBuilder.NotExists();
        }

        public static SingleConditionBuilder Raw(this ConditionBuilder builder, string rawExpression)
        {
            SingleConditionBuilder singleBuilder = new SingleConditionBuilder(builder, LogOp.And);
            singleBuilder.Raw(rawExpression);
            return singleBuilder;
        }
    }
}