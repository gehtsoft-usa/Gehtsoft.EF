using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntitiesQueryBase : ConditionEntityQueryBase
    {
        internal SelectEntityQueryBuilderBase mSelectBuilder;

        protected override bool IsReader => true;

        internal SelectEntityQueryBuilderBase SelectEntityBuilder => mSelectBuilder;

        public SelectQueryBuilder SelectBuilder => mSelectBuilder.SelectQueryBuilder;

        public EntityQueryConditionBuilder Having { get; protected set; }

        internal SelectEntitiesQueryBase(SqlDbQuery query, SelectEntityQueryBuilderBase builder) : base(query, builder)
        {
            mSelectBuilder = builder;
            Having = new EntityQueryConditionBuilder(this, mSelectBuilder.Having);
        }

        protected SelectEntitiesQueryBase(Type type, SqlDbConnection connection) : this(connection.GetQuery(), new SelectEntityQueryBuilderBase(type, connection))
        {
        }

        public bool Distinct
        {
            get { return mSelectBuilder.Distinct; }
            set { mSelectBuilder.Distinct = value; }
        }

        public int Skip
        {
            get { return mSelectBuilder.Skip; }
            set { mSelectBuilder.Skip = value; }
        }

        public int Limit
        {
            get { return mSelectBuilder.Limit; }
            set { mSelectBuilder.Limit = value; }
        }

        private readonly List<Type> mResultsetTypes = new List<Type>();

        public void AddToResultset(string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(property, alias);
            InQueryName v = GetReference(property);
            if (!v.Item.Column.ForeignKey)
                mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
            else
                mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
        }

        public void AddToResultset(Type type, string property, string alias = null) => AddToResultset(type, 0, property, alias);

        public void AddToResultset(Type type, int occurrence, string property, string alias = null)
        {
            mSelectBuilder.AddToResultset(type, occurrence, property, alias);
            InQueryName v = GetReference(type, occurrence, property);
            if (!v.Item.Column.ForeignKey)
                mResultsetTypes.Add(v.Item.Column.PropertyAccessor.PropertyType);
            else
                mResultsetTypes.Add(v.Item.Column.ForeignTable.PrimaryKey.PropertyAccessor.PropertyType);
        }

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

        public void AddToResultset(AggFn aggregation, Type type, string property, string alias = null) => AddToResultset(aggregation, type, 0, property, alias);

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

        internal void AddExpressionToResultset(string expression, bool isaggregate, DbType dbType, Type type, string alias)
        {
            mSelectBuilder.AddExpressionToResultset(expression, isaggregate, dbType, alias);
            mResultsetTypes.Add(type);
        }

        public void AddToResultset(SelectEntitiesQueryBase query, Type type, string alias = null)
        {
            query.SelectEntityBuilder.QueryBuilder.PrepareQuery();
            AddExpressionToResultset($"({query.SelectEntityBuilder.QueryBuilder.Query})", false, DbType.Object, type, alias);
            CopyParametersFrom(query);
        }

        public void AddOrderBy(string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(property, direction);

        public void AddOrderBy(Type type, string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(type, property, direction);

        public void AddOrderBy(Type type, int occurrence, string property, SortDir direction = SortDir.Asc) => mSelectBuilder.AddOrderBy(type, occurrence, property, direction);

        public void AddGroupBy(string property) => mSelectBuilder.AddGroupBy(property);

        public void AddGroupBy(Type type, string property) => mSelectBuilder.AddGroupBy(type, property);

        public void AddGroupBy(Type type, int occurrence, string property) => mSelectBuilder.AddGroupBy(type, occurrence, property);

        public void AddEntity(Type type, string connectToProperty = null, bool open = false) => mSelectBuilder.AddEntity(type, connectToProperty, open);

        public void AddEntity<T>(string connectToProperty = null, bool open = false) => AddEntity(typeof(T), connectToProperty, open);

        public void AddWholeTree() => mSelectBuilder.AddEntitiesTree();

        protected List<Tuple<string, bool>> mDynamicNames;

        protected virtual bool IgnoreOnDynamic(int index, SqlDbQuery.FieldInfo field) => false;

        protected virtual List<Tuple<string, bool>> DynamicNames
        {
            get
            {
                if (mDynamicNames == null)
                {
                    mDynamicNames = new List<Tuple<string, bool>>();
                    for (int i = 0, a = 0; i < mQuery.FieldCount; i++)
                    {
                        SqlDbQuery.FieldInfo field = mQuery.Field(i);
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

        public void AddOrderByExpr(string expression, SortDir direction = SortDir.Asc)
        {
            mSelectBuilder.AddOrderByExpr(expression, direction);
        }

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

        public IList<dynamic> ReadAllDynamic()
        {
            List<dynamic> rc = new List<dynamic>();
            dynamic one;

            while ((one = ReadOneDynamic()) != null)
                rc.Add(one);

            return rc;
        }

        public async Task<IList<dynamic>> ReadAllDynamicAsync(CancellationToken? token = null)
        {
            List<dynamic> rc = new List<dynamic>();
            dynamic one;

            while ((one = await ReadOneDynamicAsync(token)) != null)
                rc.Add(one);

            return rc;
        }

        public SelectQueryBuilderResultsetItem ResultColumn(int index) => mSelectBuilder.ResultColumn(index);

        public QueryBuilderEntity FindType(Type type, int occurrence = 1) => mSelectBuilder.FindType(type, occurrence);

        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType)
        {
            QueryBuilderEntity r = mSelectBuilder.AddEntity(type, joinType);
            r.SelectEntitiesQuery = this;
            return r;
        }

        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType, Type typeLeft, string propertyLeft, CmpOp op, Type typeRight, string propertyRight) => AddEntity(type, joinType, typeLeft, 0, propertyLeft, op, typeRight, 0, propertyRight);

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

        protected override void PrepareQuery()
        {
            Having.SetCurrentSingleEntityQueryConditionBuilder(null);
            base.PrepareQuery();
        }
        public void AddExpressionToResultset(string expression, DbType type, string alias) => mSelectBuilder.AddExpressionToResultset(expression, false, type, alias);
    }
}