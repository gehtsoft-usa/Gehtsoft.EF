using System;
using System.Data;
using System.Linq.Expressions;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal interface IEntityInfoProvider
    {
        string Alias(string path, out DbType columnType);
        string Alias(Type type, int occurrence, string propertyName, out DbType columnType);
    }

    internal class SingleEntityConditionBuilder
    {
        private readonly EntityConditionBuilder mBuilder;
        private readonly LogOp mLogOp;
        private string mLeftSide, mRightSide;
        private CmpOp? mCmpOp = null;

        public SingleEntityConditionBuilder(LogOp op, EntityConditionBuilder builder)
        {
            mBuilder = builder;
            mLogOp = op;
        }

        public SingleEntityConditionBuilder Raw(string raw)
        {
            if (mCmpOp == null)
                mLeftSide = raw;
            else
            {
                mRightSide = raw;
                Push();
            }

            return this;
        }

        public SingleEntityConditionBuilder Raw(AggFn fn, string raw) => Raw(mBuilder.ConditionBuilder.InfoProvider.Specifics.GetAggFn(fn, raw));

        public SingleEntityConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            if (op == CmpOp.IsNull || op == CmpOp.NotNull)
                Push();

            return this;
        }

        private void Push() => mBuilder.Add(mLogOp, mLeftSide, mCmpOp ?? CmpOp.Eq, mRightSide);

        public virtual SingleEntityConditionBuilder Property(string propertyPath) => Raw(mBuilder.PropertyName(propertyPath));

        public virtual SingleEntityConditionBuilder PropertyOf(string name, Type type = null, int occurrence = 0) => Raw(mBuilder.PropertyOfName(name, type, occurrence));

        public virtual SingleEntityConditionBuilder PropertyOf<T>(string name, int occurrence = 0) => Raw(mBuilder.PropertyOfName(name, typeof(T), occurrence));

        public virtual SingleEntityConditionBuilder Property(AggFn fn, string propertyPath) => Raw(fn, mBuilder.PropertyName(propertyPath));

        public virtual SingleEntityConditionBuilder PropertyOf(AggFn fn, string name, Type type = null, int occurrence = 0) => Raw(fn, mBuilder.PropertyOfName(name, type, occurrence));

        public virtual SingleEntityConditionBuilder PropertyOf<T>(AggFn fn, string name, int occurrence = 0) => Raw(fn, mBuilder.PropertyOfName(name, typeof(T), occurrence));

        public virtual SingleEntityConditionBuilder Parameter(string parameterName) => Raw(mBuilder.Parameter(parameterName));

        public virtual SingleEntityConditionBuilder Parameters(string[] parameterNames) => Raw(mBuilder.Parameters(parameterNames));

        public virtual SingleEntityConditionBuilder Query(AQueryBuilder queryBuilder) => Raw(mBuilder.Query(queryBuilder));
    }

    internal class EntityConditionBuilder
    {
        public ConditionBuilder ConditionBuilder { get; }
        public IEntityInfoProvider EntityInfoProvider { get; }

        internal EntityConditionBuilder(ConditionBuilder builder, IEntityInfoProvider entityInfoProvider)
        {
            ConditionBuilder = builder;
            EntityInfoProvider = entityInfoProvider;
        }

        public SingleEntityConditionBuilder Add(LogOp op) => new SingleEntityConditionBuilder(op, this);

        public virtual void Add(LogOp logOp, string rawExpression) => ConditionBuilder.Add(logOp, rawExpression);

        public virtual void Add(LogOp logOp, string left, CmpOp op, string right) => ConditionBuilder.Add(logOp, left, op, right);

        public virtual string PropertyName(string propertyPath) => propertyPath == null ? null : EntityInfoProvider.Alias(propertyPath, out DbType _);

        public virtual string PropertyOfName(string name, Type type = null, int occurrence = 0) => EntityInfoProvider.Alias(type, occurrence, name, out DbType _);

        public virtual string PropertyOfName<T>(string name, int occurrence = 0) => EntityInfoProvider.Alias(typeof(T), occurrence, name, out DbType _);

        public virtual string Parameter(string parameterName) => ConditionBuilder.ParameterName(parameterName);

        public virtual string Parameters(string[] parameterNames) => ConditionBuilder.ParameterList(parameterNames);

        public virtual string Query(AQueryBuilder queryBuilder) => ConditionBuilder.Query(queryBuilder);

        public virtual OpBracket AddGroup(LogOp logOp = LogOp.And) => ConditionBuilder.AddGroup(logOp);

        public override string ToString()
        {
            return ConditionBuilder.ToString();
        }
    }
}