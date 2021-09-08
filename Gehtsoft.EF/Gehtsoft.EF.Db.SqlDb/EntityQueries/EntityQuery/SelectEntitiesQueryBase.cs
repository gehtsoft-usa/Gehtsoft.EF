using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
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
        /// Adds a query result to the resulset.
        ///
        /// The query must select one column and one row!
        /// </summary>
        /// <param name="query"></param>
        /// <param name="type"></param>
        /// <param name="alias"></param>
        public void AddToResultset(SelectEntitiesQueryBase query, Type type, string alias = null)
        {
            query.SelectEntityBuilder.QueryBuilder.PrepareQuery();
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
                    for (int i = 0, a = 0; i < mQuery.FieldCount; i++)
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
        public void AddOrderByExpr(string expression, SortDir direction = SortDir.Asc)
        {
            mSelectBuilder.AddOrderByExpr(expression, direction);
        }

        [DocgenIgnore]
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
                    if (mResultsetTypes.Count > i)
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