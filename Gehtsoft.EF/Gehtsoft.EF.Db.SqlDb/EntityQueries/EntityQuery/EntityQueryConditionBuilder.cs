using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SingleEntityQueryConditionBuilder
    {
        private readonly LogOp mLogOp;
        private DbType? mParameterType = null;
        private CmpOp? mCmpOp;

        internal string Left { get; set; }

        internal string Right { get; set; }

        public EntityQueryConditionBuilder Builder { get; }

        public SingleEntityQueryConditionBuilder(LogOp logop, EntityQueryConditionBuilder builder)
        {
            Builder = builder;
            mLogOp = logop;
            Left = Right = null;
        }

        public string ParameterName { get; private set; }
        public string[] ParameterNames { get; private set; }

        public SingleEntityQueryConditionBuilder Raw(string raw, DbType? columnType = null)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (raw.ContainsScalar())
                    throw new ArgumentException("Query should not consists of string scalars", nameof(raw));

            if (mCmpOp == null)
            {
                if (Left != null)
                    throw new InvalidOperationException("Left side is already set");

                if (columnType != null)
                    mParameterType = columnType;

                Left = raw;
            }
            else
            {
                if (Right != null)
                    throw new InvalidOperationException("Right side is already set");

                Right = raw;
                Push();
            }

            return this;
        }

        public virtual SingleEntityQueryConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            if (op == CmpOp.IsNull || op == CmpOp.NotNull)
                Push();
            return this;
        }

        public virtual SingleEntityQueryConditionBuilder Property(string propertyPath)
        {
            if (propertyPath == null)
                return Raw(null);

            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(propertyPath, out DbType columnType);
            return Raw(raw, columnType);
        }

        public virtual SingleEntityQueryConditionBuilder PropertyOf(string name, Type type = null, int occurrence = 0)
        {
            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(type, occurrence, name, out DbType columnType);
            return Raw(raw, columnType);
        }

        public SingleEntityQueryConditionBuilder PropertyOf<T>(string name, int occurrence = 0)
        {
            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(typeof(T), occurrence, name, out DbType columnType);
            return Raw(raw, columnType);
        }

        public SingleEntityQueryConditionBuilder Reference(ConditionEntityQueryBase.InQueryName reference)
        {
            string raw = $"{reference.Item.QueryEntity.Alias}.{reference.Item.Column.Name}";
            return Raw(raw, reference.Item.Column.DbType);
        }

        public SingleEntityQueryConditionBuilder Parameter(string name)
        {
            return Raw(Builder.Parameter(name));
        }

        public SingleEntityQueryConditionBuilder Parameters(string[] name)
        {
            return Raw(Builder.Parameters(name));
        }

        public SingleEntityQueryConditionBuilder Query(AQueryBuilder builder, DbType? columnType = null)
        {
            return Raw(Builder.Query(builder), columnType);
        }

        public SingleEntityQueryConditionBuilder Query(SelectEntitiesQueryBase query)
        {
            Builder.BaseQuery.CopyParametersFrom(query);
            SelectQueryBuilderResultsetItem firstColumn = query.ResultColumn(0);
            return Raw(Builder.Query(query.Builder.QueryBuilder), firstColumn.DbType);
        }

        public SingleEntityQueryConditionBuilder Value(object value, DbType? valueDbType = null)
        {
            if (Left == null)
                throw new InvalidOperationException("Value cannot be used at the left");

            if (mParameterType == null && valueDbType != null)
                mParameterType = valueDbType;

            if (mParameterType == null)
                throw new InvalidOperationException("If parameter value is used, the parameter type must be either specified implicitly or be discoverable from the left side of the expression");

            string raw = Builder.BaseQuery.NextParam;
            if (value == null)
                Builder.BaseQuery.Query.BindNull(raw, (DbType)mParameterType);
            else
                Builder.BaseQuery.Query.BindParam(raw, (DbType)mParameterType, value);

            ParameterName = raw;

            return Raw(Builder.Parameter(raw));
        }

        public SingleEntityQueryConditionBuilder Values(params object[] values) => Values(null, values);

        public SingleEntityQueryConditionBuilder Values(DbType? valueDbType, params object[] values)
        {
            if (Left == null)
                throw new InvalidOperationException("Values cannot be used at the left");

            if (mParameterType == null && valueDbType != null)
                mParameterType = valueDbType;

            if (mParameterType == null)
                throw new InvalidOperationException("If parameter values are used, the parameter type must be either specified implicitly or be discoverable from the left side of the expression");

            string[] names = new string[values.Length];

            for (int i = 0; i < values.Length; i++)
            {
                names[i] = Builder.BaseQuery.NextParam;

                if (values[i] == null)
                    Builder.BaseQuery.Query.BindNull(names[i], (DbType)mParameterType);
                else
                    Builder.BaseQuery.Query.BindParam(names[i], (DbType)mParameterType, values[i]);
            }

            ParameterNames = names;

            return Raw(Builder.Parameters(names));
        }

        protected void Push()
        {
            Builder.Add(mLogOp, Left, mCmpOp ?? CmpOp.Eq, Right);
        }
    }

    public class EntityQueryConditionBuilder
    {
        public ConditionEntityQueryBase BaseQuery { get; }
        public EntityConditionBuilder BaseWhere { get; }

        public EntityQueryConditionBuilder(ConditionEntityQueryBase query, EntityConditionBuilder builder)
        {
            BaseQuery = query;
            BaseWhere = builder;
        }

        public SingleEntityQueryConditionBuilder Add(LogOp logOp = LogOp.And) => new SingleEntityQueryConditionBuilder(logOp, this);

        public virtual void Add(LogOp logOp, string rawExpression) => BaseWhere.Add(logOp, rawExpression);

        public virtual void Add(LogOp logOp, string left, CmpOp op, string right) => BaseWhere.Add(logOp, left, op, right);

        public virtual string PropertyName(string propertyPath) => propertyPath == null ? null : BaseWhere.PropertyName(propertyPath);

        public virtual string PropertyOfName(string name, Type type = null, int occurrence = 0) => BaseWhere.PropertyOfName(name, type, occurrence);

        public virtual string PropertyOfName<T>(string name, int occurrence = 0) => BaseWhere.PropertyOfName<T>(name, occurrence);

        public virtual string ReferenceName(ConditionEntityQueryBase.InQueryName reference) => $"{reference.Item.QueryEntity.Alias}.{reference.Item.Column.Name}";

        public virtual string Value(object parameterValue, DbType dbType)
        {
            string name = BaseQuery.NextParam;
            if (parameterValue == null)
                BaseQuery.BindNull(name, dbType);
            else
                BaseQuery.Query.BindParam(name, dbType, parameterValue);
            return Parameter(name);
        }

        public virtual string Parameter(string parameterName) => BaseWhere.Parameter(parameterName);

        public virtual string Parameters(string[] parameterNames) => BaseWhere.Parameters(parameterNames);

        public virtual string Query(AQueryBuilder queryBuilder) => BaseWhere.Query(queryBuilder);

        public virtual string Query(SelectEntitiesQueryBase query)
        {
            BaseQuery.CopyParametersFrom(query);
            return Query(query.Builder.QueryBuilder);
        }

        public virtual OpBracket AddGroup(LogOp logOp = LogOp.And) => BaseWhere.AddGroup(logOp);

        public override string ToString() => BaseWhere.ToString();
    }

    public static class EntityQueryConditionBuilderExtension
    {
        public static SingleEntityQueryConditionBuilder And(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.And, builder);

        public static SingleEntityQueryConditionBuilder Or(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.Or, builder);

        public static SingleEntityQueryConditionBuilder Property(this EntityQueryConditionBuilder builder, string propertyPath)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.Property(propertyPath);
            return rc;
        }

        public static SingleEntityQueryConditionBuilder PropertyOf(this EntityQueryConditionBuilder builder, string property, Type type = null, int occurrence = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.PropertyOf(property, type, occurrence);
            return rc;
        }

        public static SingleEntityQueryConditionBuilder PropertyOf<T>(this EntityQueryConditionBuilder builder, string property, int occurrence = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.PropertyOf(property, typeof(T), occurrence);
            return rc;
        }

        internal static SingleEntityQueryConditionBuilder Is(this EntityQueryConditionBuilder builder, CmpOp op)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.Is(op);
            return rc;
        }

        public static SingleEntityQueryConditionBuilder Exists(this EntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.Exists).Query(query);

        public static SingleEntityQueryConditionBuilder Exists(this EntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.Exists).Query(query);

        public static SingleEntityQueryConditionBuilder NotExists(this EntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotExists).Query(query);

        public static SingleEntityQueryConditionBuilder NotExists(this EntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotExists).Query(query);
    }

    public static class SingleEntityQueryConditionBuilderExtension
    {
        public static SingleEntityQueryConditionBuilder Eq(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Eq);

        public static SingleEntityQueryConditionBuilder Neq(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Neq);

        public static SingleEntityQueryConditionBuilder Le(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Le);

        public static SingleEntityQueryConditionBuilder Ls(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Ls);

        public static SingleEntityQueryConditionBuilder Ge(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Ge);

        public static SingleEntityQueryConditionBuilder Gt(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Gt);

        public static SingleEntityQueryConditionBuilder Like(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Like);

        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Exists);

        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotExists);

        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.In);

        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        public static SingleEntityQueryConditionBuilder IsNull(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.IsNull);

        public static SingleEntityQueryConditionBuilder NotNull(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotNull);

        public static SingleEntityQueryConditionBuilder Eq(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Eq).Value(value);

        public static SingleEntityQueryConditionBuilder Neq(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Neq).Value(value);

        public static SingleEntityQueryConditionBuilder Le(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Le).Value(value);

        public static SingleEntityQueryConditionBuilder Ls(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Ls).Value(value);

        public static SingleEntityQueryConditionBuilder Ge(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Ge).Value(value);

        public static SingleEntityQueryConditionBuilder Gt(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Gt).Value(value);

        public static SingleEntityQueryConditionBuilder Like(this SingleEntityQueryConditionBuilder builder, string value) => builder.Is(CmpOp.Like).Value(value);

        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.Exists).Query(query);

        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.Exists).Query(query);

        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotExists).Query(query);

        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotExists).Query(query);

        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.In).Query(query);

        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.In).Query(query);

        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotIn).Query(query);

        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotIn).Query(query);

        internal static SingleEntityQueryConditionBuilder Wrap(this SingleEntityQueryConditionBuilder builder, SqlFunctionId function)
        {
            if (builder.Right != null)
                throw new InvalidOperationException("Wrap may be applied on the left side only");
            if (builder.Left != null)
                builder.Left = builder.Builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Left });
            else if (function == SqlFunctionId.Count)
                builder.Left = builder.Builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { });
            else
                throw new InvalidOperationException("Nothing to wrap");
            return builder;
        }

        public static SingleEntityQueryConditionBuilder Sum(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Sum);

        public static SingleEntityQueryConditionBuilder Min(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Min);

        public static SingleEntityQueryConditionBuilder Max(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Max);

        public static SingleEntityQueryConditionBuilder Avg(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Avg);

        public static SingleEntityQueryConditionBuilder Count(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Count);

        public static SingleEntityQueryConditionBuilder ToString(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToString);

        public static SingleEntityQueryConditionBuilder ToInteger(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToInteger);

        public static SingleEntityQueryConditionBuilder ToDate(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToDate);

        public static SingleEntityQueryConditionBuilder ToDouble(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToDouble);

        public static SingleEntityQueryConditionBuilder ToTimestamp(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToTimestamp);

        public static SingleEntityQueryConditionBuilder ToUpper(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Upper);

        public static SingleEntityQueryConditionBuilder ToLower(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Lower);

        public static SingleEntityQueryConditionBuilder Trim(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Trim);

        public static SingleEntityQueryConditionBuilder Abs(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Abs);
    }
}