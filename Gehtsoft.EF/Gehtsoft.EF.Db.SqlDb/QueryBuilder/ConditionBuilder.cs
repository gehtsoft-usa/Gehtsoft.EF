using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
        internal ConditionBuilder Builder { get; }

        private readonly LogOp mLogOp;
        private CmpOp? mCmpOp = null;

        internal string Right { get; set; }

        internal string Left { get; set; }

        internal bool HasOp => mCmpOp != null;

        internal SingleConditionBuilder(ConditionBuilder builder, LogOp logOp)
        {
            Builder = builder;
            mLogOp = logOp;
            builder.SetCurrentSignleConditionBuilder(this);
        }

        public SingleConditionBuilder Raw(string rawExpression)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (rawExpression.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(rawExpression));

            if (mCmpOp == null)
                Left = rawExpression;
            else
                Right = rawExpression;
            return this;
        }

        public SingleConditionBuilder Property(AggFn aggFn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(aggFn, entity, columnInfo));

        public SingleConditionBuilder Property(AggFn aggFn, TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(aggFn, columnInfo));

        public SingleConditionBuilder Property(QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(entity, columnInfo));

        public SingleConditionBuilder Property(TableDescriptor.ColumnInfo columnInfo) => Raw(Builder.PropertyName(columnInfo));

        public SingleConditionBuilder Parameter(string name) => Raw(Builder.ParameterName(name));

        public SingleConditionBuilder Parameters(params string[] names) => Raw(Builder.ParameterList(names));

        public SingleConditionBuilder Query(AQueryBuilder builder) => Raw(Builder.Query(builder));

        public SingleConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            return this;
        }

        public SingleConditionBuilder Add(LogOp op) => Builder.Add(op);

        internal void Push()
        {
            if (mCmpOp == null)
                Builder.Add(mLogOp, Left);
            else
                Builder.Add(mLogOp, Left, (CmpOp)mCmpOp, Right);
        }
    }

    public interface IInQueryFieldReference
    {
        string Alias { get; }
    }

    internal class InQueryFieldReference : IInQueryFieldReference
    {
        public string Alias { get; }
        public InQueryFieldReference(string alias)
        {
            Alias = alias;
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

        public static SingleConditionBuilder IsNull(this SingleConditionBuilder builder) => builder.Is(CmpOp.IsNull);
        public static SingleConditionBuilder NotNull(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotNull);

        public static SingleConditionBuilder NotIn(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        public static SingleConditionBuilder Exists(this SingleConditionBuilder builder) => builder.Is(CmpOp.Exists);

        public static SingleConditionBuilder NotExists(this SingleConditionBuilder builder) => builder.Is(CmpOp.NotExists);

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, int value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, double value) => builder.Raw(value.ToString(CultureInfo.InvariantCulture));

        public static SingleConditionBuilder Value(this SingleConditionBuilder builder, string value) => builder.Raw($"\"{value}\"");

        public static SingleConditionBuilder Reference(this SingleConditionBuilder builder, IInQueryFieldReference reference) => builder.Raw(reference.Alias);

        internal static SingleConditionBuilder Wrap(this SingleConditionBuilder builder, SqlFunctionId function)
        {
            bool done = false;
            if (builder.HasOp)
            {
                if (builder.Right != null)
                {
                    builder.Right = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Right });
                    done = true;
                }
                else if (function == SqlFunctionId.Count)
                {
                    builder.Right = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, Array.Empty<string>());
                    done = true;
                }
            }
            else
            {
                if (builder.Left != null)
                {
                    builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Left });
                    done = true;
                }
                else if (function == SqlFunctionId.Count)
                {
                    builder.Left = builder.Builder.InfoProvider.Specifics.GetSqlFunction(function, Array.Empty<string>());
                    done = true;
                }
            }
            if (!done)
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

        public static SingleConditionBuilder And(this SingleConditionBuilder builder) => builder.Add(LogOp.And);

        public static SingleConditionBuilder Or(this SingleConditionBuilder builder) => builder.Add(LogOp.Or);

        public static SingleConditionBuilder AndNot(this SingleConditionBuilder builder) => builder.Add(LogOp.And | LogOp.Not);

        public static SingleConditionBuilder OrNot(this SingleConditionBuilder builder) => builder.Add(LogOp.Or | LogOp.Not);

        public static ConditionBuilder And(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.And(action);
            return builder.Builder;
        }

        public static ConditionBuilder Or(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.Or(action);
            return builder.Builder;
        }

        public static ConditionBuilder AndNot(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.AndNot(action);
            return builder.Builder;
        }

        public static ConditionBuilder OrNot(this SingleConditionBuilder builder, Action<ConditionBuilder> action)
        {
            builder.Builder.OrNot(action);
            return builder.Builder;
        }
    }

    public class ConditionBuilder : IOpBracketAcceptor
    {
        private readonly StringBuilder mWhere = new StringBuilder();

        private bool mConditionStarted = false;

        public IConditionBuilderInfoProvider InfoProvider { get; protected set; }

        public bool IsEmpty
        {
            get
            {
                SetCurrentSignleConditionBuilder(null);
                return mWhere.Length == 0;
            }
        }

        public ConditionBuilder(IConditionBuilderInfoProvider infoProvider)
        {
            InfoProvider = infoProvider;
        }

        private SingleConditionBuilder mCurrentSingleConditionBuilder = null;

        internal void SetCurrentSignleConditionBuilder(SingleConditionBuilder singleConditionBuilder)
        {
            if (mCurrentSingleConditionBuilder != null)
            {
                var c = mCurrentSingleConditionBuilder;
                mCurrentSingleConditionBuilder = null;
                c.Push();
            }
            mCurrentSingleConditionBuilder = singleConditionBuilder;
        }

        public virtual void Add(LogOp logOp, string rawExpression)
        {
            SetCurrentSignleConditionBuilder(null);

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

        public virtual string ParameterName(string parameterName)
        {
            if (parameterName == null)
                return null;

            string prefix = InfoProvider.Specifics.ParameterInQueryPrefix;
            if (!string.IsNullOrEmpty(prefix) && !parameterName.StartsWith(prefix))
                parameterName = prefix + parameterName;
            return parameterName;
        }

        public virtual string ParameterList(string[] parameterNames)
        {
            StringBuilder parameterName = new StringBuilder();
            foreach (string s in parameterNames)
            {
                if (parameterName.Length > 0)
                    parameterName.Append(',');

                parameterName.Append(ParameterName(s));
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
            SetCurrentSignleConditionBuilder(null);
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
            SetCurrentSignleConditionBuilder(null);
            if (!mConditionStarted)
                throw new EfSqlException(EfExceptionCode.WhereBracketIsEmpty);
            mWhere.Append(")");
            mWhere.Append(InfoProvider.Specifics.CloseLogOp(op.LogOp));
        }

        public override string ToString()
        {
            SetCurrentSignleConditionBuilder(null);
            return mWhere.ToString();
        }
        public SingleConditionBuilder Add(LogOp logOp) => new SingleConditionBuilder(this, logOp);

        public void Add(LogOp logOp, Action<ConditionBuilder> builder)
        {
            using (var bracket = AddGroup(logOp))
                builder.Invoke(this);
        }
    }

    public static class ConditionBuilderExtension
    {
        public static SingleConditionBuilder And(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.And);

        public static SingleConditionBuilder Or(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.Or);

        public static SingleConditionBuilder Not(this ConditionBuilder builder) => AndNot(builder);

        public static SingleConditionBuilder OrNot(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.Or | LogOp.Not);

        public static SingleConditionBuilder AndNot(this ConditionBuilder builder) => new SingleConditionBuilder(builder, LogOp.And | LogOp.Not);

        public static void And(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.And, action);

        public static void Or(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.Or, action);

        public static void AndNot(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.And | LogOp.Not, action);

        public static void OrNot(this ConditionBuilder builder, Action<ConditionBuilder> action) => builder.Add(LogOp.Or | LogOp.Not, action);

        public static SingleConditionBuilder Property(this ConditionBuilder builder, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(entity, columnInfo));

        public static SingleConditionBuilder Property(this ConditionBuilder builder, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(columnInfo));

        public static SingleConditionBuilder Property(this ConditionBuilder builder, AggFn fn, QueryBuilderEntity entity, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(fn, entity, columnInfo));

        public static SingleConditionBuilder Property(this ConditionBuilder builder, AggFn fn, TableDescriptor.ColumnInfo columnInfo) => builder.Raw(builder.PropertyName(fn, columnInfo));

        public static SingleConditionBuilder Parameter(this ConditionBuilder builder, string parameter)
        {
            var b = new SingleConditionBuilder(builder, LogOp.And);
            return b.Parameter(parameter);
        }

        public static SingleConditionBuilder Reference(this ConditionBuilder builder, IInQueryFieldReference reference) => builder.Raw(reference.Alias);

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