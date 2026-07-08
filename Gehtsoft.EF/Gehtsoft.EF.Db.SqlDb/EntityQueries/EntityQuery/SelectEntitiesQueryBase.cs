using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base query to all entity select operation.
    ///
    /// Use <see cref="EntityConnectionExtension.GetSelectEntitiesQueryBase(SqlDbConnection, Type)"/> to get an instance of this object.
    ///
    /// You can use this query type directly to fine tune the resulset or
    /// use <see cref="SelectEntitiesCountQuery"/> or
    /// <see cref="SelectEntitiesQuery"/>.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class SelectEntitiesQueryBase : ConditionEntityQueryBase
    {
        internal SelectEntityQueryBuilderBase mSelectBuilder;

        protected override bool IsReader => true;

        internal SelectEntityQueryBuilderBase SelectEntityBuilder => mSelectBuilder;

        /// <summary>
        /// Gets associated select builder.
        /// </summary>
        public SelectQueryBuilder SelectBuilder => mSelectBuilder.SelectQueryBuilder;

        /// <summary>
        /// The having condition builder.
        ///
        /// For where condition use <see cref="ConditionEntityQueryBase.Where"/> property.
        /// </summary>
        public EntityQueryConditionBuilder Having { get; protected set; }

        internal SelectEntitiesQueryBase(SqlDbQuery query, SelectEntityQueryBuilderBase builder) : base(query, builder)
        {
            mSelectBuilder = builder;
            Having = new EntityQueryConditionBuilder(this, mSelectBuilder.Having);
        }

        protected SelectEntitiesQueryBase(Type type, SqlDbConnection connection) : this(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection))
        {
        }

        /// <summary>
        /// Gets or sets flag to select only distinct rows.
        /// </summary>
        public bool Distinct
        {
            get { return mSelectBuilder.Distinct; }
            set { mSelectBuilder.Distinct = value; }
        }

        /// <summary>
        /// Gets or sets how much entities should be skipped from the beginning
        /// </summary>
        public int Skip
        {
            get { return mSelectBuilder.Skip; }
            set { mSelectBuilder.Skip = value; }
        }

        /// <summary>
        /// Gets or sets how much entities must be read.
        /// </summary>
        public int Limit
        {
            get { return mSelectBuilder.Limit; }
            set { mSelectBuilder.Limit = value; }
        }

        private readonly List<Type> mResultsetTypes = new List<Type>();

        // The dynamic-property side-table joins established in this query, keyed by
        // (entity type, property name, occurrence, value type) - see DynamicPropertyJoinKey.
        private Dictionary<DynamicPropertyJoinKey, DynamicPropertyJoin> mDynamicPropertyJoins;

        internal Dictionary<DynamicPropertyJoinKey, DynamicPropertyJoin> DynamicPropertyJoins
            => mDynamicPropertyJoins ?? (mDynamicPropertyJoins = new Dictionary<DynamicPropertyJoinKey, DynamicPropertyJoin>());

        /// <summary>
        /// Looks up a dynamic-property side-table join already established in this query (by a prior
        /// projection). Used to filter the property directly on the joined column instead of a
        /// correlated `owner IN (SELECT ...)` sub-query.
        /// </summary>
        internal bool TryGetDynamicPropertyJoin(Type entityType, string name, int occurrence, DynamicPropertyValueType type, out DynamicPropertyJoin join)
        {
            if (mDynamicPropertyJoins == null)
            {
                join = null;
                return false;
            }
            return mDynamicPropertyJoins.TryGetValue(new DynamicPropertyJoinKey(entityType, name, occurrence, type), out join);
        }

        // Resultset column index -> the declared type used to decode the stored (encoded) value at
        // read time (ticks -> DateTime, 0/1 -> bool, ...). Populated by the dynamic-property
        // projection methods; consulted by BindOneDynamic.
        private Dictionary<int, DynamicPropertyValueType> mDynamicPropertyColumns;

        /// <summary>
        /// Add all columns of the type specified into the resultset.
        /// </summary>
        /// <param name="entityType"></param>
        /// <param name="occurrence"></param>
        /// <param name="exclusion"></param>
        public void AddToResultset(Type entityType, int occurrence = 0, string[] exclusion = null)
        {
            var ei = AllEntities.Get(entityType);
            for (int i = 0; i < ei.TableDescriptor.Count; i++)
            {
                var ci = ei.TableDescriptor[i];
                if (exclusion == null || Array.Find(exclusion, s => s.Equals(ci.ID, StringComparison.OrdinalIgnoreCase)) == null)
                    AddToResultset(entityType, occurrence, ci.ID);
            }
        }

        /// <summary>
        /// Add all columns of the type specified into the resultset (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="occurrence"></param>
        /// <param name="exclusion"></param>
        public void AddToResultset<T>(int occurrence = 0, string[] exclusion = null)
            => AddToResultset(typeof(T), occurrence, exclusion);

        /// <summary>
        /// Adds the property to the resulset.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(property, alias);
            InQueryName v = GetReference(property);
            if (!v.Item.Column.ForeignKey)
                mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
            else
                mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
        }

        /// <summary>
        /// Adds the property of the first occurrence of the specified type to the resulset.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(Type type, string property, string alias = null) => AddToResultset(type, 0, property, alias);

        /// <summary>
        /// Adds the property of the specified occurrence of the specified type to the resulset.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(Type type, int occurrence, string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(type, occurrence, property, alias);
            InQueryName v = GetReference(type, occurrence, property);
            if (!v.Item.Column.ForeignKey)
                mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
            else
                mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
        }

        /// <summary>
        /// Adds a property aggregated with the specified function to the resulset.
        /// </summary>
        /// <param name="aggregation"></param>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(AggFn aggregation, string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(aggregation, property, alias);
            if (aggregation == AggFn.Count)
                mResultsetTypes.Add(typeof(int));
            else
            {
                InQueryName v = GetReference(property);
                if (!v.Item.Column.ForeignKey)
                    mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
                else
                    mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
            }
        }

        /// <summary>
        /// Adds the property of the first occurrence of the specified type aggregated by the specified function to the resulset.
        /// </summary>
        /// <param name="aggregation"></param>
        /// <param name="type"></param>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(AggFn aggregation, Type type, string property, string alias = null) => AddToResultset(aggregation, type, 0, property, alias);

        /// <summary>
        /// Adds the property of the specified occurrence of the specified type aggregated by the specified function to the resulset.
        /// </summary>
        /// <param name="aggregation"></param>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <param name="property"></param>
        /// <param name="alias"></param>
        public void AddToResultset(AggFn aggregation, Type type, int occurrence, string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(aggregation, type, occurrence, property, alias);
            if (aggregation == AggFn.Count)
                mResultsetTypes.Add(typeof(int));
            else
            {
                InQueryName v = GetReference(type, occurrence, property);
                if (!v.Item.Column.ForeignKey)
                    mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
                else
                    mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
            }
        }

        /// <summary>
        /// Adds RAW expression to the resulset.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="isaggregate"></param>
        /// <param name="dbType"></param>
        /// <param name="type"></param>
        /// <param name="alias"></param>
        internal void AddExpressionToResultset(string expression, bool isaggregate, DbType dbType, Type type, string alias)
        {
            mSelectBuilder.AddExpressionToResultset(expression, isaggregate, dbType, alias);
            mResultsetTypes.Add(type);
        }

        /// <summary>
        /// Adds a dynamic property to the resultset.
        ///
        /// The property's side table is joined (once per property/occurrence/type) and its value
        /// column is added to the resultset. Because the query has no CLR operand to infer the type
        /// from, the value type is specified explicitly - it selects the value column and how the
        /// stored value is decoded when read.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type of the property.</param>
        /// <param name="alias">The resultset column alias, or `null`.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToResultset<T>(string name, DynamicPropertyValueType type, string alias = null, int occurrence = 0)
            => AddDynamicPropertyToResultset(typeof(T), name, type, alias, occurrence);

        /// <summary>
        /// Adds a dynamic property of the specified entity type to the resultset.
        /// </summary>
        /// <param name="entityType">The entity type that owns the dynamic property.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type of the property.</param>
        /// <param name="alias">The resultset column alias, or `null`.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToResultset(Type entityType, string name, DynamicPropertyValueType type, string alias = null, int occurrence = 0)
        {
            DynamicPropertyJoin join = DynamicPropertyProjection.EnsureJoin(this, entityType, name, occurrence, type);
            AddDynamicPropertyColumn(join.ColumnAlias, false, join.ValueColumn.DbType, type, alias);
        }

        /// <summary>
        /// Adds a dynamic property aggregated with the specified function to the resultset.
        ///
        /// The aggregate runs against the stored value column; the result is decoded back to the
        /// declared type when read (e.g. `Min`/`Max` of a DateTime property yields a DateTime).
        /// `Count` is the exception - it yields the row count as an integer and is not decoded.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="aggregation">The aggregate function.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type of the property.</param>
        /// <param name="alias">The resultset column alias, or `null`.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToResultset<T>(AggFn aggregation, string name, DynamicPropertyValueType type, string alias = null, int occurrence = 0)
            => AddDynamicPropertyToResultset(aggregation, typeof(T), name, type, alias, occurrence);

        /// <summary>
        /// Adds a dynamic property of the specified entity type aggregated with the specified function to the resultset.
        /// </summary>
        /// <param name="aggregation">The aggregate function.</param>
        /// <param name="entityType">The entity type that owns the dynamic property.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type of the property.</param>
        /// <param name="alias">The resultset column alias, or `null`.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToResultset(AggFn aggregation, Type entityType, string name, DynamicPropertyValueType type, string alias = null, int occurrence = 0)
        {
            DynamicPropertyJoin join = DynamicPropertyProjection.EnsureJoin(this, entityType, name, occurrence, type);
            string expression = SelectBuilder.Specifics.GetAggFn(aggregation, join.ColumnAlias);

            if (aggregation == AggFn.Count)
                AddExpressionToResultset(expression, true, DbType.Int32, typeof(int), alias);
            else
                AddDynamicPropertyColumn(expression, true, join.ValueColumn.DbType, type, alias);
        }

        // Adds an expression that yields an encoded dynamic-property value to the resultset and
        // records the resultset index so the value is decoded (per the declared type) when read.
        private void AddDynamicPropertyColumn(string expression, bool isAggregate, DbType dbType, DynamicPropertyValueType type, string alias)
        {
            int index = ResultsetSize;
            AddExpressionToResultset(expression, isAggregate, dbType, ClrTypeOf(type), alias);
            if (mDynamicPropertyColumns == null)
                mDynamicPropertyColumns = new Dictionary<int, DynamicPropertyValueType>();
            mDynamicPropertyColumns[index] = type;
        }

        // Adds a dynamic-property expression already compiled by the LINQ layer to the resultset and
        // records its decode type. The LINQ read path (CreateType / ReadOneValue) consults the same
        // decode registry via TryGetDynamicPropertyColumn.
        internal void AddDynamicExpressionToResultset(string expression, bool isAggregate, DynamicPropertyValueType type, string alias)
            => AddDynamicPropertyColumn(expression, isAggregate, DbType.Object, type, alias);

        // Whether the resultset column at the given index is a dynamic property (and if so, the type
        // its stored value must be decoded to). Consulted by the LINQ projection read path.
        internal bool TryGetDynamicPropertyColumn(int index, out DynamicPropertyValueType type)
        {
            if (mDynamicPropertyColumns != null)
                return mDynamicPropertyColumns.TryGetValue(index, out type);
            type = default;
            return false;
        }

        private static Type ClrTypeOf(DynamicPropertyValueType type)
        {
            switch (type)
            {
                case DynamicPropertyValueType.String:
                    return typeof(string);
                case DynamicPropertyValueType.Integer:
                    return typeof(int);
                case DynamicPropertyValueType.Long:
                    return typeof(long);
                case DynamicPropertyValueType.Real:
                    return typeof(double);
                case DynamicPropertyValueType.Boolean:
                    return typeof(bool);
                case DynamicPropertyValueType.DateTime:
                    return typeof(DateTime);
                default:
                    return typeof(object);
            }
        }

        // Returns the join a dynamic property was projected under, or throws: ORDER BY / GROUP BY /
        // HAVING can only reference a property that was already added to the resultset.
        private DynamicPropertyJoin RequireDynamicPropertyJoin(Type entityType, string name, int occurrence, DynamicPropertyValueType type)
        {
            if (!TryGetDynamicPropertyJoin(entityType, name, occurrence, type, out DynamicPropertyJoin join))
                throw new InvalidOperationException($"The dynamic property '{name}' (type '{type}') must be added to the resultset with AddDynamicPropertyToResultset before it can be used in ORDER BY / GROUP BY");
            return join;
        }

        /// <summary>
        /// Adds a dynamic property to the order by.
        ///
        /// The property must already have been added to the resultset (with the same type and
        /// occurrence) via <see cref="AddDynamicPropertyToResultset{T}(string, DynamicPropertyValueType, string, int)"/>.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="direction">The sort direction.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToOrderBy<T>(string name, DynamicPropertyValueType type, SortDir direction = SortDir.Asc, int occurrence = 0)
            => AddDynamicPropertyToOrderBy(typeof(T), name, type, direction, occurrence);

        /// <summary>
        /// Adds a dynamic property of the specified entity type to the order by.
        /// </summary>
        /// <param name="entityType">The entity type that owns the dynamic property.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="direction">The sort direction.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToOrderBy(Type entityType, string name, DynamicPropertyValueType type, SortDir direction = SortDir.Asc, int occurrence = 0)
            => AddOrderByExpr(RequireDynamicPropertyJoin(entityType, name, occurrence, type).ColumnAlias, direction);

        /// <summary>
        /// Adds a dynamic property to the group by.
        ///
        /// The property must already have been added to the resultset (with the same type and
        /// occurrence) via <see cref="AddDynamicPropertyToResultset{T}(string, DynamicPropertyValueType, string, int)"/>.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToGroupBy<T>(string name, DynamicPropertyValueType type, int occurrence = 0)
            => AddDynamicPropertyToGroupBy(typeof(T), name, type, occurrence);

        /// <summary>
        /// Adds a dynamic property of the specified entity type to the group by.
        /// </summary>
        /// <param name="entityType">The entity type that owns the dynamic property.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddDynamicPropertyToGroupBy(Type entityType, string name, DynamicPropertyValueType type, int occurrence = 0)
            => AddGroupByExpr(RequireDynamicPropertyJoin(entityType, name, occurrence, type).ColumnAlias);

        /// <summary>
        /// Starts a HAVING condition on a dynamic property.
        ///
        /// The property must already have been added to the resultset (with the same type and
        /// occurrence) via <see cref="AddDynamicPropertyToResultset{T}(string, DynamicPropertyValueType, string, int)"/>.
        /// The returned builder is positioned on the joined value column; chain an aggregate wrapper
        /// and a comparison, e.g. `HavingDynamicPropertyOf&lt;T&gt;("price", Real).Sum().Gt(100.0)`.
        ///
        /// The comparison value is compared against the stored (encoded) column, so for `DateTime`
        /// and `Boolean` properties compare against the encoded form (UTC ticks, 0/1); numeric and
        /// string properties compare directly.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the dynamic property.</typeparam>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public SingleEntityQueryConditionBuilder HavingDynamicPropertyOf<T>(string name, DynamicPropertyValueType type, int occurrence = 0)
            => HavingDynamicPropertyOf(typeof(T), name, type, occurrence);

        /// <summary>
        /// Starts a HAVING condition on a dynamic property of the specified entity type.
        /// </summary>
        /// <param name="entityType">The entity type that owns the dynamic property.</param>
        /// <param name="name">The dynamic property name.</param>
        /// <param name="type">The value type the property was projected under.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public SingleEntityQueryConditionBuilder HavingDynamicPropertyOf(Type entityType, string name, DynamicPropertyValueType type, int occurrence = 0)
        {
            DynamicPropertyJoin join = RequireDynamicPropertyJoin(entityType, name, occurrence, type);
            SingleEntityQueryConditionBuilder single = new SingleEntityQueryConditionBuilder(LogOp.And, Having);
            single.Raw(join.ColumnAlias, join.ValueColumn.DbType);
            return single;
        }

        // Resolves an entity property to the query-builder column and table it lives on, so a JSON
        // value inside that column can be projected / ordered / grouped by. Unlike a dynamic property
        // (which is a side-table join), a JSON value is just an expression on the owning column.
        private void ResolveJsonColumn(Type entityType, int occurrence, string property, out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity)
        {
            InQueryName reference = GetReference(entityType, occurrence, property);
            column = reference.Item.Column;
            entity = reference.Item.QueryEntity;
        }

        // Maps the declared JSON value DbType to the CLR type the dynamic reader decodes it to.
        private static Type ClrTypeOfJson(DbType type)
        {
            switch (type)
            {
                case DbType.Boolean:
                    return typeof(bool);
                case DbType.Int16:
                    return typeof(short);
                case DbType.Int32:
                    return typeof(int);
                case DbType.Int64:
                    return typeof(long);
                case DbType.Single:
                    return typeof(float);
                case DbType.Double:
                    return typeof(double);
                case DbType.Decimal:
                case DbType.Currency:
                    return typeof(decimal);
                case DbType.DateTime:
                case DbType.Date:
                    return typeof(DateTime);
                case DbType.Binary:
                    return typeof(byte[]);
                default:
                    return typeof(string);
            }
        }

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property to the resultset.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the JSON property.</typeparam>
        /// <param name="property">The name of the JSON property.</param>
        /// <param name="jsonPath">The JSON path to the value, for example <c>"$.age"</c>.</param>
        /// <param name="type">The primitive type of the value at the path.</param>
        /// <param name="alias">The resultset column alias, or <c>null</c>.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddJsonValueToResultset<T>(string property, string jsonPath, DbType type, string alias = null, int occurrence = 0)
            => AddJsonValueToResultset(typeof(T), property, jsonPath, type, alias, occurrence);

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property of the specified entity type to the resultset.
        /// </summary>
        public void AddJsonValueToResultset(Type entityType, string property, string jsonPath, DbType type, string alias = null, int occurrence = 0)
        {
            ResolveJsonColumn(entityType, occurrence, property, out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity);
            SelectBuilder.AddJsonValueToResultset(column, entity, jsonPath, type, alias);
            mResultsetTypes.Add(ClrTypeOfJson(type));
        }

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property aggregated with the specified function to the resultset.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the JSON property.</typeparam>
        /// <param name="aggregation">The aggregate function.</param>
        /// <param name="property">The name of the JSON property.</param>
        /// <param name="jsonPath">The JSON path to the value, for example <c>"$.age"</c>.</param>
        /// <param name="type">The primitive type of the value at the path.</param>
        /// <param name="alias">The resultset column alias, or <c>null</c>.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddJsonValueToResultset<T>(AggFn aggregation, string property, string jsonPath, DbType type, string alias = null, int occurrence = 0)
            => AddJsonValueToResultset(aggregation, typeof(T), property, jsonPath, type, alias, occurrence);

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property of the specified entity type aggregated with the specified function to the resultset.
        /// </summary>
        public void AddJsonValueToResultset(AggFn aggregation, Type entityType, string property, string jsonPath, DbType type, string alias = null, int occurrence = 0)
        {
            ResolveJsonColumn(entityType, occurrence, property, out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity);
            SelectBuilder.AddJsonValueToResultset(aggregation, column, entity, jsonPath, type, alias);
            mResultsetTypes.Add(aggregation == AggFn.Count ? typeof(int) : ClrTypeOfJson(type));
        }

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property to the order by.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the JSON property.</typeparam>
        /// <param name="property">The name of the JSON property.</param>
        /// <param name="jsonPath">The JSON path to the value, for example <c>"$.age"</c>.</param>
        /// <param name="type">The primitive type of the value at the path.</param>
        /// <param name="direction">The sort direction.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddJsonValueToOrderBy<T>(string property, string jsonPath, DbType type, SortDir direction = SortDir.Asc, int occurrence = 0)
            => AddJsonValueToOrderBy(typeof(T), property, jsonPath, type, direction, occurrence);

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property of the specified entity type to the order by.
        /// </summary>
        public void AddJsonValueToOrderBy(Type entityType, string property, string jsonPath, DbType type, SortDir direction = SortDir.Asc, int occurrence = 0)
        {
            ResolveJsonColumn(entityType, occurrence, property, out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity);
            SelectBuilder.AddJsonValueToOrderBy(column, entity, jsonPath, type, direction);
        }

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property to the group by.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the JSON property.</typeparam>
        /// <param name="property">The name of the JSON property.</param>
        /// <param name="jsonPath">The JSON path to the value, for example <c>"$.age"</c>.</param>
        /// <param name="type">The primitive type of the value at the path.</param>
        /// <param name="occurrence">The occurrence of the entity in the query.</param>
        public void AddJsonValueToGroupBy<T>(string property, string jsonPath, DbType type, int occurrence = 0)
            => AddJsonValueToGroupBy(typeof(T), property, jsonPath, type, occurrence);

        /// <summary>
        /// Adds a value at a JSON path inside a JSON property of the specified entity type to the group by.
        /// </summary>
        public void AddJsonValueToGroupBy(Type entityType, string property, string jsonPath, DbType type, int occurrence = 0)
        {
            ResolveJsonColumn(entityType, occurrence, property, out TableDescriptor.ColumnInfo column, out QueryBuilderEntity entity);
            SelectBuilder.AddJsonValueToGroupBy(column, entity, jsonPath, type);
        }

        // Parses a member/array-index expression into the JSON property, path and the value type to
        // extract it as (the leaf type, unless the caller overrides it).
        private void ParseJson<T>(Expression<Func<T, object>> expression, DbType? typeOverride, out string property, out string jsonPath, out DbType type)
        {
            JsonExpressionParser.Parse(expression, out property, out jsonPath, out Type valueType);
            if (typeOverride.HasValue)
                type = typeOverride.Value;
            else if (!SelectBuilder.Specifics.TypeToDb(valueType, out type))
                throw new ArgumentException($"The JSON value type {valueType.Name} is not supported", nameof(expression));
        }

        /// <summary>
        /// Adds a value inside a JSON property addressed by a member/array-index expression such as
        /// <c>e =&gt; e.Data.Income</c> or <c>e =&gt; e.Data.ChildrenAge[0]</c> to the resultset.
        /// </summary>
        /// <typeparam name="T">The entity type that owns the JSON property.</typeparam>
        /// <param name="expression">The member/array-index expression.</param>
        /// <param name="alias">The resultset column alias, or <c>null</c>.</param>
        /// <param name="type">The value type to extract as, overriding the type inferred from the leaf.</param>
        public void AddJsonValueToResultset<T>(Expression<Func<T, object>> expression, string alias = null, DbType? type = null)
        {
            ParseJson(expression, type, out string property, out string jsonPath, out DbType dbType);
            AddJsonValueToResultset(typeof(T), property, jsonPath, dbType, alias);
        }

        /// <summary>
        /// Adds a value inside a JSON property addressed by a member/array-index expression aggregated
        /// with the specified function to the resultset.
        /// </summary>
        public void AddJsonValueToResultset<T>(AggFn aggregation, Expression<Func<T, object>> expression, string alias = null, DbType? type = null)
        {
            ParseJson(expression, type, out string property, out string jsonPath, out DbType dbType);
            AddJsonValueToResultset(aggregation, typeof(T), property, jsonPath, dbType, alias);
        }

        /// <summary>
        /// Adds a value inside a JSON property addressed by a member/array-index expression to the order by.
        /// </summary>
        public void AddJsonValueToOrderBy<T>(Expression<Func<T, object>> expression, SortDir direction = SortDir.Asc, DbType? type = null)
        {
            ParseJson(expression, type, out string property, out string jsonPath, out DbType dbType);
            AddJsonValueToOrderBy(typeof(T), property, jsonPath, dbType, direction);
        }

        /// <summary>
        /// Adds a value inside a JSON property addressed by a member/array-index expression to the group by.
        /// </summary>
        public void AddJsonValueToGroupBy<T>(Expression<Func<T, object>> expression, DbType? type = null)
        {
            ParseJson(expression, type, out string property, out string jsonPath, out DbType dbType);
            AddJsonValueToGroupBy(typeof(T), property, jsonPath, dbType);
        }

        /// <summary>
        /// Adds a query result to the resulset.
        ///
        /// The query must select one column and one row!
        /// </summary>
        /// <param name="query"></param>
        /// <param name="type"></param>
        /// <param name="alias"></param>
        public void AddToResultset(SelectEntitiesQueryBase query, Type type, string alias = null)
        {
            query.PrepareQuery();
            AddExpressionToResultset($"({query.SelectEntityBuilder.QueryBuilder.Query})", false, DbType.Object, type, alias);
            CopyParametersFrom(query);
        }

        /// <summary>
        /// Adds the property to the order by.
        /// </summary>
        /// <param name="property"></param>
        /// <param name="direction"></param>
        public void AddOrderBy(string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(property, direction);

        /// <summary>
        /// Adds the property of the first occurrence of the specified type to the order by.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="property"></param>
        /// <param name="direction"></param>
        public void AddOrderBy(Type type, string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(type, property, direction);

        /// <summary>
        /// Adds the property of the specified occurrence of the specified type to the order by.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <param name="property"></param>
        /// <param name="direction"></param>
        public void AddOrderBy(Type type, int occurrence, string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(type, occurrence, property, direction);

        /// <summary>
        /// Adds the property to the group by.
        /// </summary>
        /// <param name="property"></param>
        public void AddGroupBy(string property) => mSelectBuilder.AddGroupBy(property);

        /// <summary>
        /// Adds the property of the first occurrence of the specified type to the group by.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="property"></param>
        public void AddGroupBy(Type type, string property) => mSelectBuilder.AddGroupBy(type, property);

        /// <summary>
        /// Adds the property of the specified occurrence of the specified type to the group by.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <param name="property"></param>
        public void AddGroupBy(Type type, int occurrence, string property) => mSelectBuilder.AddGroupBy(type, occurrence, property);

        /// <summary>
        /// Adds the entity to the query and auto-connect it to the rest of entities.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="connectToProperty"></param>
        /// <param name="open"></param>
        public void AddEntity(Type type, string connectToProperty = null, bool open = false) => mSelectBuilder.AddEntity(type, connectToProperty, open);

        /// <summary>
        /// Adds the entity to the query and auto-connect it to the rest of entities. (generic version).
        /// </summary>
        /// <param name="connectToProperty"></param>
        /// <param name="open"></param>
        public void AddEntity<T>(string connectToProperty = null, bool open = false) => AddEntity(typeof(T), connectToProperty, open);

        /// <summary>
        /// Add the whole tree of entities for the current main entity of the query.
        /// </summary>
        public void AddWholeTree() => mSelectBuilder.AddEntitiesTree();

        protected List<Tuple<string, bool>> mDynamicNames;

        protected virtual bool IgnoreOnDynamic(int index, FieldInfo field) => false;

        protected virtual List<Tuple<string, bool>> DynamicNames
        {
            get
            {
                if (mDynamicNames == null)
                {
                    mDynamicNames = new List<Tuple<string, bool>>();
                    int a = 0;
                    for (int i = 0; i < mQuery.FieldCount; i++)
                    {
                        FieldInfo field = mQuery.Field(i);
                        string name = mSelectBuilder.ResultColumn(i).Alias?.Trim();
                        if (string.IsNullOrEmpty(name))
                            name = field.Name?.Trim();
                        if (string.IsNullOrEmpty(name))
                            name = $"anonymous{a++}";
                        else
                        {
                            StringBuilder newname = new StringBuilder();
                            bool first = true;
                            foreach (char c in name)
                            {
                                if (Char.IsLetter(c))
                                    newname.Append(c);
                                else if (Char.IsDigit(c))
                                {
                                    if (first)
                                        newname.Append('_');
                                    newname.Append(c);
                                }
                                else if (c == '_')
                                    newname.Append(c);
                                else
                                    newname.Append('_');

                                first = false;
                            }

                            name = newname.ToString();
                        }

                        mDynamicNames.Add(new Tuple<string, bool>(name, !IgnoreOnDynamic(i, field)));
                    }
                }

                return mDynamicNames;
            }
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        public void AddOrderByExpr(string expression, SortDir direction = SortDir.Asc)
        {
            mSelectBuilder.AddOrderByExpr(expression, direction);
        }

        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        internal void AddGroupByExpr(string expression)
        {
            mSelectBuilder.AddGroupByExpr(expression);
        }

        protected virtual bool BindOneDynamic(ExpandoObject dynObj)
        {
            List<Tuple<string, bool>> dynamicNames = DynamicNames;
            IDictionary<string, object> dict = (IDictionary<string, object>)dynObj;
            for (int i = 0; i < dynamicNames.Count; i++)
            {
                if (dynamicNames[i].Item2)
                {
                    object value;
                    if (mDynamicPropertyColumns != null && mDynamicPropertyColumns.TryGetValue(i, out DynamicPropertyValueType dynamicType))
                        value = mQuery.IsNull(i) ? null : DynamicPropertiesValueMapper.Decode(dynamicType, mQuery.GetValue(i));
                    else if (mResultsetTypes.Count > i)
                        value = mQuery.GetValue(i, mResultsetTypes[i]);
                    else
                        value = mQuery.GetValue(i);
                    dict.Add(dynamicNames[i].Item1, value);
                }
            }

            return true;
        }

        /// <summary>
        /// Read one entity to a dynamic object.
        /// </summary>
        /// <returns></returns>
        public dynamic ReadOneDynamic()
        {
            if (!Executed)
                Execute();

            if (mQuery.ReadNext())
            {
                ExpandoObject dynObj = new ExpandoObject();
                if (BindOneDynamic(dynObj))
                    return dynObj;
                else
                    return null;
            }

            return null;
        }

        /// <summary>
        /// Read one entity to a dynamic object asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<dynamic> ReadOneDynamicAsync(CancellationToken? token = null)
        {
            if (!Executed)
                await ExecuteAsync(token);

            if (await mQuery.ReadNextAsync(token))
            {
                ExpandoObject dynObj = new ExpandoObject();
                if (BindOneDynamic(dynObj))
                    return dynObj;
                else
                    return null;
            }

            return null;
        }

        /// <summary>
        /// Read all entities as a dynamic objects.
        /// </summary>
        /// <returns></returns>
        public IList<dynamic> ReadAllDynamic()
        {
            List<dynamic> rc = new List<dynamic>();
            dynamic one;

            while ((one = ReadOneDynamic()) != null)
                rc.Add(one);

            return rc;
        }

        /// <summary>
        /// Read all entities as a dynamic objects asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<IList<dynamic>> ReadAllDynamicAsync(CancellationToken? token = null)
        {
            List<dynamic> rc = new List<dynamic>();
            dynamic one;

            while ((one = await ReadOneDynamicAsync(token)) != null)
                rc.Add(one);

            return rc;
        }

        [DocgenIgnore]
        public int ResultsetSize => mSelectBuilder.ResultsetSize;

        [DocgenIgnore]
        public SelectQueryBuilderResultsetItem ResultColumn(int index) => mSelectBuilder.ResultColumn(index);

        /// <summary>
        /// Find the query builder table associated with the specified occurrence of the specified entity type
        /// </summary>
        /// <param name="type"></param>
        /// <param name="occurrence"></param>
        /// <returns></returns>
        public QueryBuilderEntity FindType(Type type, int occurrence = 0) => mSelectBuilder.FindType(type, occurrence);

        /// <summary>
        /// Adds the entity to the query without automatic connection.
        ///
        /// The condition needs to be set directly via <see cref="QueryBuilderEntity.On"/> of the returned object.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="joinType"></param>
        /// <returns></returns>
        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType)
        {
            QueryBuilderEntity r = mSelectBuilder.AddEntity(type, joinType);
            r.SelectEntitiesQuery = this;
            return r;
        }

        /// <summary>
        /// Adds entity to the query and set a connection using a one operator comparison.
        ///
        /// If more complex connection is required, use <see cref="AddEntity(Type, TableJoinType)"/> method.
        /// </summary>
        /// <param name="type">The type to be connected</param>
        /// <param name="joinType">The join type</param>
        /// <param name="typeLeft">The type on the left side of the on condition</param>
        /// <param name="propertyLeft">The property on the left side of the on condition</param>
        /// <param name="op">The comparison op</param>
        /// <param name="typeRight">The type on the right side of the on condition</param>
        /// <param name="propertyRight">The property on the right side of the on condition</param>
        /// <returns></returns>
        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType, Type typeLeft, string propertyLeft, CmpOp op, Type typeRight, string propertyRight) => AddEntity(type, joinType, typeLeft, 0, propertyLeft, op, typeRight, 0, propertyRight);

        /// <summary>
        /// Adds entity to the query and set a connection using a one operator comparison (when entity is used more than once in the query).
        ///
        /// If more complex connection is required, use <see cref="AddEntity(Type, TableJoinType)"/> method.
        /// </summary>
        /// <param name="type">The type to be connected</param>
        /// <param name="joinType">The join type</param>
        /// <param name="typeLeft">The type on the left side of the on condition</param>
        /// <param name="occurrenceLeft">The occurrence of type on the left in the query. 0 means first occurrence</param>
        /// <param name="propertyLeft">The property on the left side of the on condition</param>
        /// <param name="op">The comparison op</param>
        /// <param name="typeRight">The type on the right side of the on condition</param>
        /// <param name="occurrenceRight">The occurrence of type on the right in the query. 0 means first occurrence</param>
        /// <param name="propertyRight">The property on the right side of the on condition</param>
        /// <returns></returns>
        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType, Type typeLeft, int occurrenceLeft, string propertyLeft, CmpOp op, Type typeRight, int occurrenceRight, string propertyRight)
        {
            var r = mSelectBuilder.AddEntity(type, joinType);
            r.SelectEntitiesQuery = this;

            InQueryName referenceLeft = GetReference(typeLeft, occurrenceLeft, propertyLeft);
            if (referenceLeft == null)
                throw new ArgumentException("Property is not found", nameof(propertyLeft));
            InQueryName referenceRight = GetReference(typeRight, occurrenceRight, propertyRight);
            if (referenceRight == null)
                throw new ArgumentException("Property is not found", nameof(propertyRight));

            r.On.And().Reference(referenceLeft).Is(op).Reference(referenceRight);
            return r;
        }

        [DocgenIgnore]
        public override void PrepareQuery()
        {
            Having.SetCurrentSingleEntityQueryConditionBuilder(null);
            base.PrepareQuery();
        }

        [DocgenIgnore]
        public void AddExpressionToResultset(string expression, DbType type, string alias) => mSelectBuilder.AddExpressionToResultset(expression, false, type, alias);
    }
}