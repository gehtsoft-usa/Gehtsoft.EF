using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The condition builder for a single condition of entity query.
    ///
    /// Use <see cref="EntityQueryConditionBuilder.Add(LogOp)"/> to create a signle condition.
    ///
    /// Use <see cref="SingleEntityQueryConditionBuilderExtension" /> to created easier to read
    /// conditions.
    /// </summary>
    public class SingleEntityQueryConditionBuilder
    {
        private readonly LogOp mLogOp;
        private DbType? mParameterType = null;
        private CmpOp? mCmpOp;

        internal string Left { get; set; }

        internal string Right { get; set; }

        [DocgenIgnore]
        public EntityQueryConditionBuilder Builder { get; }

        [DocgenIgnore]
        public SingleEntityQueryConditionBuilder(LogOp logop, EntityQueryConditionBuilder builder)
        {
            Builder = builder;
            Builder.SetCurrentSingleEntityQueryConditionBuilder(this);
            mLogOp = logop;
            Left = Right = null;
        }

        [DocgenIgnore]
        public string ParameterName { get; private set; }

        [DocgenIgnore]
        public string[] ParameterNames { get; private set; }

        [DocgenIgnore]
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
            }

            return this;
        }

        /// <summary>
        /// Sets the comparison operation.
        /// </summary>
        /// <param name="op"></param>
        /// <returns></returns>
        public virtual SingleEntityQueryConditionBuilder Is(CmpOp op)
        {
            mCmpOp = op;
            return this;
        }

        /// <summary>
        /// Sets left or right part of the expression to the property.
        /// </summary>
        /// <param name="propertyPath"></param>
        /// <returns></returns>
        public virtual SingleEntityQueryConditionBuilder Property(string propertyPath)
        {
            if (propertyPath == null)
                return Raw(null);

            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(propertyPath, out DbType columnType);
            return Raw(raw, columnType);
        }

        /// <summary>
        /// Sets the left or right part of the expression to the property of the specified occurrence of the specified type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <returns></returns>
        public virtual SingleEntityQueryConditionBuilder PropertyOf(string name, Type type = null, int occurrence = 0)
        {
            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(type, occurrence, name, out DbType columnType);
            return Raw(raw, columnType);
        }

        /// <summary>
        /// Sets the left or right part of the expression to the property of the specified occurrence of the specified type (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="occurrence"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder PropertyOf<T>(string name, int occurrence = 0)
        {
            string raw = Builder.BaseQuery.Where.BaseWhere.EntityInfoProvider.Alias(typeof(T), occurrence, name, out DbType columnType);
            return Raw(raw, columnType);
        }

        /// <summary>
        /// Sets the left or right part of the expression to the reference to a column of another entity.
        ///
        /// This method is typically used to add references between the main query and a subquery.
        ///
        /// Use [clink=Gehtsoft.EF.Db.SqlDb.EntityQueries.ConditionEntityQueryBase.GetReference]ConditionEntityQueryBase.GetReference()[/clink]
        /// method to get a reference.
        /// </summary>
        /// <param name="reference"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Reference(ConditionEntityQueryBase.InQueryName reference)
        {
            string raw = $"{reference.Item.QueryEntity.Alias}.{reference.Item.Column.Name}";
            return Raw(raw, reference.Item.Column.DbType);
        }

        /// <summary>
        /// Sets the left or right part of the expression the parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Parameter(string name)
        {
            return Raw(Builder.Parameter(name));
        }

        /// <summary>
        /// Sets the left or right part of the expression to the parameter list.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Parameters(string[] name)
        {
            return Raw(Builder.Parameters(name));
        }

        /// <summary>
        /// Sets the left or right part of the expression to the query (via query builder).
        ///
        /// Query may be used with any condition, but unless the condition is `Exists` or `In`
        /// the query must return one value in one row.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="columnType"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Query(AQueryBuilder builder, DbType? columnType = null)
        {
            return Raw(Builder.Query(builder), columnType);
        }

        /// <summary>
        /// Sets the left or right part of the expression to the query (via entity query).
        /// Query may be used with any condition, but unless the condition is `Exists` or `In`
        /// the query must return one value in one row.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Query(SelectEntitiesQueryBase query)
        {
            query.Where.SetCurrentSingleEntityQueryConditionBuilder(null);
            Builder.BaseQuery.CopyParametersFrom(query);
            SelectQueryBuilderResultsetItem firstColumn = query.ResultColumn(0);
            return Raw(Builder.Query(query.EntityQueryBuilder.QueryBuilder), firstColumn.DbType);
        }

        /// <summary>
        /// Sets the right part of the expression to the value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="valueDbType"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Sets the right part of the expression to the values (using the default type)
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Values(params object[] values) => Values(null, values);

        /// <summary>
        /// Sets the right part of the expression to the values (using the specified DB type)
        /// </summary>
        /// <param name="valueDbType"></param>
        /// <param name="values"></param>
        /// <returns></returns>
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

        internal protected void Push()
        {
            if (mCmpOp == null)
                Builder.Add(mLogOp, Left);
            else
                Builder.Add(mLogOp, Left, (CmpOp)mCmpOp, Right);
        }
    }

    /// <summary>
    /// The condition builder for WHERE and HAVING clauses of entity queries.
    ///
    /// Each individual condition in the expression is build using <see cref="SingleEntityQueryConditionBuilder"/>.
    ///
    /// Use <see cref="EntityQueryConditionBuilderExtension"/> to create an easier to read conditions.
    /// </summary>
    public class EntityQueryConditionBuilder
    {
        [DocgenIgnore]
        public ConditionEntityQueryBase BaseQuery { get; }
        internal EntityConditionBuilder BaseWhere { get; }

        internal EntityQueryConditionBuilder(ConditionEntityQueryBase query, EntityConditionBuilder builder)
        {
            BaseQuery = query;
            BaseWhere = builder;
        }

        private SingleEntityQueryConditionBuilder mCurrentBuilder = null;

        internal void SetCurrentSingleEntityQueryConditionBuilder(SingleEntityQueryConditionBuilder currentBuilder)
        {
            if (mCurrentBuilder != null)
            {
                var c = mCurrentBuilder;
                mCurrentBuilder = null;
                c.Push();
            }
            mCurrentBuilder = currentBuilder;
        }

        /// <summary>
        /// Adds one condition and connects it to the rest of the expression using the specified logical operator.
        ///
        /// Note: for the first condition only `Not` is accounted. `And` and `Or` are ignored.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public SingleEntityQueryConditionBuilder Add(LogOp logOp = LogOp.And) => new SingleEntityQueryConditionBuilder(logOp, this);

        /// <summary>
        /// Adds a raw SQL expression.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="rawExpression"></param>
        public virtual void Add(LogOp logOp, string rawExpression)
        {
            SetCurrentSingleEntityQueryConditionBuilder(null);
            BaseWhere.Add(logOp, rawExpression);
        }

        [DocgenIgnore]
        public virtual void Add(LogOp logOp, string left, CmpOp op, string right)
        {
            SetCurrentSingleEntityQueryConditionBuilder(null);
            BaseWhere.Add(logOp, left, op, right);
        }

        [DocgenIgnore]
        public virtual string PropertyName(string propertyPath) => propertyPath == null ? null : BaseWhere.PropertyName(propertyPath);

        [DocgenIgnore]
        public virtual string PropertyOfName(string name, Type type = null, int occurrence = 0) => BaseWhere.PropertyOfName(name, type, occurrence);

        [DocgenIgnore]
        public virtual string PropertyOfName<T>(string name, int occurrence = 0) => BaseWhere.PropertyOfName<T>(name, occurrence);

        [DocgenIgnore]
        public virtual string ReferenceName(ConditionEntityQueryBase.InQueryName reference) => $"{reference.Item.QueryEntity.Alias}.{reference.Item.Column.Name}";

        [DocgenIgnore]
        public virtual string Value(object parameterValue, DbType dbType)
        {
            string name = BaseQuery.NextParam;
            if (parameterValue == null)
                BaseQuery.BindNull(name, dbType);
            else
                BaseQuery.Query.BindParam(name, dbType, parameterValue);
            return Parameter(name);
        }

        [DocgenIgnore]
        public virtual string Parameter(string parameterName) => BaseWhere.Parameter(parameterName);

        [DocgenIgnore]
        public virtual string Parameters(string[] parameterNames) => BaseWhere.Parameters(parameterNames);

        [DocgenIgnore]
        public virtual string Query(AQueryBuilder queryBuilder) => BaseWhere.Query(queryBuilder);

        [DocgenIgnore]
        public virtual string Query(SelectEntitiesQueryBase query)
        {
            BaseQuery.CopyParametersFrom(query);
            return Query(query.EntityQueryBuilder.QueryBuilder);
        }

        /// <summary>
        /// Adds a group of conditions and connects it to the already defined condition using the logical operator specified.
        ///
        /// The method returns a disposable object. The condition is considered closed when the returned object is disposed.
        /// </summary>
        /// <param name="logOp"></param>
        /// <returns></returns>
        public virtual OpBracket AddGroup(LogOp logOp = LogOp.And)
        {
            SetCurrentSingleEntityQueryConditionBuilder(null);
            var g = BaseWhere.AddGroup(logOp);
            g.OnClose += (s, e) => this.SetCurrentSingleEntityQueryConditionBuilder(null);
            return g;
        }

        /// <summary>
        /// Adds a group of conditions and connects it to the already defined condition using the logical operator specified.
        ///
        /// The condition is defined within the action specified.
        /// </summary>
        /// <param name="logOp"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public virtual EntityQueryConditionBuilder AddGroup(LogOp logOp, Action<EntityQueryConditionBuilder> group)
        {
            SetCurrentSingleEntityQueryConditionBuilder(null);
            using (var g = BaseWhere.AddGroup(logOp))
            {
                g.OnClose += (s, e) => this.SetCurrentSingleEntityQueryConditionBuilder(null);
                group(this);
            }
            return this;
        }

        [DocgenIgnore]
        public override string ToString()
        {
            SetCurrentSingleEntityQueryConditionBuilder(null);
            return BaseWhere.ToString();
        }
    }

    /// <summary>
    /// The extension methods for the entity condition builder.
    /// </summary>
    public static class EntityQueryConditionBuilderExtension
    {
        /// <summary>
        /// Starts a new single condition and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder And(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.And, builder);

        /// <summary>
        /// Starts a new single negative condition and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder AndNot(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.And | LogOp.Not, builder);

        /// <summary>
        /// Starts a new single condition and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Or(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.Or, builder);

        /// <summary>
        /// Starts a new single negative condition and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder OrNot(this EntityQueryConditionBuilder builder) => new SingleEntityQueryConditionBuilder(LogOp.Or | LogOp.Not, builder);

        /// <summary>
        /// Starts condition that compares a property and connects it to the other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="propertyPath"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Property(this EntityQueryConditionBuilder builder, string propertyPath)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.Property(propertyPath);
            return rc;
        }

        /// <summary>
        /// Starts condition that compares a property of the specified type and connects it to the other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="property"></param>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder PropertyOf(this EntityQueryConditionBuilder builder, string property, Type type = null, int occurrence = 0)
        {
            var rc = new SingleEntityQueryConditionBuilder(LogOp.And, builder);
            rc.PropertyOf(property, type, occurrence);
            return rc;
        }

        /// <summary>
        /// Starts condition that compares a property of the specified type and connects it to the other conditions using logical and (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <param name="property"></param>
        /// <param name="occurrence"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Starts checks whether the query returns a non-empty resultset connects it to the other conditions using logical and. The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Exists(this EntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.Exists).Query(query);

        /// <summary>
        /// Starts checks whether the query returns a non-empty resultset connects it to the other conditions using logical and. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Exists(this EntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.Exists).Query(query);

        /// <summary>
        /// Starts checks whether the query returns an empty resultset connects it to the other conditions using logical and. The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotExists(this EntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotExists).Query(query);

        /// <summary>
        /// Starts checks whether the query returns an empty resultset connects it to the other conditions using logical and. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotExists(this EntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotExists).Query(query);

        /// <summary>
        /// Starts a new group of conditions, define it using the actions specified and connect it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder And(this EntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.AddGroup(LogOp.And, group);

        /// <summary>
        /// Starts a new group of conditions, define it using the actions specified and connect it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder Or(this EntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.AddGroup(LogOp.Or, group);

        /// <summary>
        /// Starts a new group of conditions, define it using the actions specified, negate the condition and connect it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder AndNot(this EntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.AddGroup(LogOp.And | LogOp.Not, group);

        /// <summary>
        /// Starts a new group of conditions, define it using the actions specified, negate the condition and connect it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder OrNot(this EntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.AddGroup(LogOp.Or | LogOp.Not, group);
    }

    /// <summary>
    /// The extensions methods for a single condition in the entity condition builder.
    /// </summary>
    public static class SingleEntityQueryConditionBuilderExtension
    {
        /// <summary>
        /// Sets the condition to "equals to".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Eq(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Eq);

        /// <summary>
        /// Sets the condition to "not equals to".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Neq(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Neq);

        /// <summary>
        /// Sets the condition to "less than or equals to".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Le(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Le);

        /// <summary>
        /// Sets the condition to "less than".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Ls(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Ls);

        /// <summary>
        /// Sets the condition to "greater than or equals to".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Ge(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Ge);

        /// <summary>
        /// Sets the condition to "greater than".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Gt(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Gt);

        /// <summary>
        /// Sets the condition to "like".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Like(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Like);

        /// <summary>
        /// Sets the condition to "exists (returns non-empty resulset)".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.Exists);

        /// <summary>
        /// Sets the condition to "not exists (returns empty resultset)".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotExists);

        /// <summary>
        /// Sets the condition to "in".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.In);

        /// <summary>
        /// Sets the condition to "not in".
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotIn);

        /// <summary>
        /// Sets the condition to "is null".
        ///
        /// This condition does not expect the right part.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder IsNull(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.IsNull);

        /// <summary>
        /// Sets the condition to "is not null".
        ///
        /// This condition does not expect the right part.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotNull(this SingleEntityQueryConditionBuilder builder) => builder.Is(CmpOp.NotNull);

        /// <summary>
        /// Sets the condition to "equals to" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Eq(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Eq).Value(value);

        /// <summary>
        /// Sets the condition to "not equals to" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Neq(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Neq).Value(value);

        /// <summary>
        /// Sets the condition to "less than or equals to" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Le(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Le).Value(value);

        /// <summary>
        /// Sets the condition to "less than" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Ls(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Ls).Value(value);

        /// <summary>
        /// Sets the condition to "greater than or equals to" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Ge(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Ge).Value(value);

        /// <summary>
        /// Sets the condition to "greater than" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Gt(this SingleEntityQueryConditionBuilder builder, object value) => builder.Is(CmpOp.Gt).Value(value);

        /// <summary>
        /// Sets the condition to "is like" the value specified.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Like(this SingleEntityQueryConditionBuilder builder, string value) => builder.Is(CmpOp.Like).Value(value);

        /// <summary>
        /// Sets the condition to "exists". The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.Exists).Query(query);

        /// <summary>
        /// Sets the condition to "exists" the value specified. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Exists(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.Exists).Query(query);

        /// <summary>
        /// Sets the condition to "not exists". The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotExists).Query(query);

        /// <summary>
        /// Sets the condition to "not exists" the value specified. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotExists(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotExists).Query(query);

        /// <summary>
        /// Sets the condition to "in" in a sub-query. The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.In).Query(query);

        /// <summary>
        /// Sets the condition to "in" in a sub-query. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder In(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.In).Query(query);

        /// <summary>
        /// Sets the condition to "not in" in a sub-query. The query is set by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder, AQueryBuilder query) => builder.Is(CmpOp.NotIn).Query(query);

        /// <summary>
        /// Sets the condition to "not in" in a sub-query. The query is set by an entity query.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder NotIn(this SingleEntityQueryConditionBuilder builder, SelectEntitiesQueryBase query) => builder.Is(CmpOp.NotIn).Query(query);

        internal static SingleEntityQueryConditionBuilder Wrap(this SingleEntityQueryConditionBuilder builder, SqlFunctionId function)
        {
            if (builder.Right != null)
                builder.Right = builder.Builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Right });
            else if (builder.Left != null)
                builder.Left = builder.Builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { builder.Left });
            else if (function == SqlFunctionId.Count)
                builder.Left = builder.Builder.BaseQuery.Where.BaseWhere.ConditionBuilder.InfoProvider.Specifics.GetSqlFunction(function, new string[] { });
            else
                throw new InvalidOperationException("Nothing to wrap");
            return builder;
        }

        /// <summary>
        /// Wraps the left or right part by Sum function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Sum(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Sum);

        /// <summary>
        /// Wraps the left or right part by Min function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Min(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Min);

        /// <summary>
        /// Wraps the left or right part by Max function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Max(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Max);

        /// <summary>
        /// Wraps the left or right part by Avg function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Avg(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Avg);

        /// <summary>
        /// Wraps the left or right part by Count function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Count(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Count);

        /// <summary>
        /// Wraps the left or right part by ToString function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToString(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToString);

        /// <summary>
        /// Wraps the left or right part by ToInteger function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToInteger(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToInteger);

        /// <summary>
        /// Wraps the left or right part by ToDate function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToDate(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToDate);

        /// <summary>
        /// Wraps the left or right part by ToDouble function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToDouble(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToDouble);

        /// <summary>
        /// Wraps the left or right part by ToTimestamp function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToTimestamp(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.ToTimestamp);

        /// <summary>
        /// Wraps the left or right part by ToUpper function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToUpper(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Upper);

        /// <summary>
        /// Wraps the left or right part by ToLower function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder ToLower(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Lower);

        /// <summary>
        /// Wraps the left or right part by Trim function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Trim(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Trim);

        /// <summary>
        /// Wraps the left or right part by Abs function.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Abs(this SingleEntityQueryConditionBuilder builder) => builder.Wrap(SqlFunctionId.Abs);

        /// <summary>
        /// Adds another single condition and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder And(this SingleEntityQueryConditionBuilder builder)
            => builder.Builder.Add(LogOp.And);

        /// <summary>
        /// Adds another single condition and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder Or(this SingleEntityQueryConditionBuilder builder)
            => builder.Builder.Add(LogOp.Or);

        /// <summary>
        /// Adds another negative single condition and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder AndNot(this SingleEntityQueryConditionBuilder builder)
            => builder.Builder.Add(LogOp.And | LogOp.Not);

        /// <summary>
        /// Adds another negative single condition and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static SingleEntityQueryConditionBuilder OrNot(this SingleEntityQueryConditionBuilder builder)
            => builder.Builder.Add(LogOp.Or | LogOp.Not);

        /// <summary>
        /// Adds a group of the conditions defined by the actions and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder And(this SingleEntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.Builder.AddGroup(LogOp.And, group);

        /// <summary>
        /// Adds a group of the conditions defined by the actions and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder Or(this SingleEntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.Builder.AddGroup(LogOp.Or, group);

        /// <summary>
        /// Adds a group of the conditions defined by the actions, negates it and connects it to other conditions using logical and.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder AndNot(this SingleEntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.Builder.AddGroup(LogOp.And | LogOp.Not, group);

        /// <summary>
        /// Adds a group of the conditions defined by the actions, negates it and connects it to other conditions using logical or.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static EntityQueryConditionBuilder OrNot(this SingleEntityQueryConditionBuilder builder, Action<EntityQueryConditionBuilder> group)
            => builder.Builder.AddGroup(LogOp.Or | LogOp.Not, group);
    }
}