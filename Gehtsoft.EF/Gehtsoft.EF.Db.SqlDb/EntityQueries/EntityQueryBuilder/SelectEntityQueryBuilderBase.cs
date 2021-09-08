using System;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class SelectEntityQueryBuilderBase : EntityQueryWithWhereBuilder
    {
        protected SelectQueryBuilder mSelectQueryBuilder;
        public SelectQueryBuilder SelectQueryBuilder => mSelectQueryBuilder;

        public EntityConditionBuilder Having { get; protected set; }

        public SelectEntityQueryBuilderBase(Type type, SqlDbConnection connection) : base(connection.GetLanguageSpecifics(), type)
        {
            mSelectQueryBuilder = connection.GetSelectQueryBuilder(mEntityDescriptor.TableDescriptor);
            SetBuilder(type, mSelectQueryBuilder);
            Having = new EntityConditionBuilder(mSelectQueryBuilder.Having, this);
        }

        internal void AddEntitiesTree()
        {
            AddSubEntities(mSelectQueryBuilder.Entities[0], mEntityDescriptor.EntityType, true, true, "");
        }

        private int CountTypes(TableDescriptor table)
        {
            int rc = 0;
            for (int i = 0; i < mQueryWithWhereBuilder.Entities.Count; i++)
                if (mQueryWithWhereBuilder.Entities[i].Table == table)
                    rc++;
            return rc;
        }

        private void AddSubEntities(QueryBuilderEntity entity, Type entityType, bool expandAll, bool addSelfRefences, string basePath)
        {
            foreach (TableDescriptor.ColumnInfo column in entity.Table)
            {
                if (column.IgnoreRead)
                    continue;

                if (column.ForeignKey)
                {
                    bool selfReference = (column.PropertyAccessor.PropertyType == entityType);
                    if (!selfReference || (selfReference && addSelfRefences))
                    {
                        QueryBuilderEntity newEntity = mSelectQueryBuilder.AddTable(column.ForeignTable, column.ForeignTable.PrimaryKey, column.Nullable ? TableJoinType.Left : TableJoinType.Inner, entity, column);
                        newEntity.EntityType = entity.EntityType;
                        string bp = basePath + column.ID + ".";
                        AddEntityItems(AllEntities.Inst[column.PropertyAccessor.PropertyType], newEntity, bp, CountTypes(newEntity.Table) - 1);

                        bool proceed = false;

                        if (expandAll && !selfReference)
                            proceed = true;
                        else if (selfReference)
                            proceed = addSelfRefences;

                        if (proceed)
                            AddSubEntities(newEntity, column.PropertyAccessor.PropertyType, expandAll, !selfReference, bp);
                    }
                }
            }
        }

        public void AddEntity(Type type, string connectToProperty = null, bool open = false)
        {
            EntityDescriptor entity = AllEntities.Inst[type];
            TableDescriptor.ColumnInfo connectingColumn = null;
            QueryBuilderEntity connectToEntity = null;
            TableDescriptor.ColumnInfo connectToColumn = null;
            TableJoinType joinType = TableJoinType.None;

            if (connectToProperty == null)
            {
                //find first match for other(fk)->this(pk)
                for (int i = 0; i < mItems.Count; i++)
                {
                    if (mItems[i].Column.ForeignKey && mItems[i].Column.PropertyAccessor.PropertyType == type)
                        connectToProperty = mItems[i].Path;
                }
            }

            if (connectToProperty == null)
            {
                //find first match from other(pk)->this(fk)
                for (int i = 0; i < mItems.Count; i++)
                {
                    if (mItems[i].Column.PrimaryKey)
                    {
                        for (int j = 0; j < entity.TableDescriptor.Count; j++)
                        {
                            if (entity.TableDescriptor[j].ForeignKey &&
                                entity.TableDescriptor[j].PropertyAccessor.PropertyType == mItems[i].Entity.EntityType)
                            {
                                connectToProperty = mItems[i].Path;
                                break;
                            }
                        }

                        if (connectToProperty != null)
                            break;
                    }
                }
            }

            if (connectToProperty == null)
                throw new EfSqlException(EfExceptionCode.IncorrectJoin);

            EntityQueryItem item = mItemIndex[connectToProperty];

            if (item == null)
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, connectToProperty);

            string pathBase = "";

            //check how the values are connected
            if (item.Column.PrimaryKey)
            {
                //other(pk) -> type(fk)
                connectToEntity = item.QueryEntity;
                connectToColumn = item.Column;

                foreach (TableDescriptor.ColumnInfo column in entity.TableDescriptor)
                {
                    if (column.ForeignKey && column.PropertyAccessor.PropertyType == item.Entity.EntityType)
                    {
                        connectingColumn = column;
                        if (open)
                            joinType = TableJoinType.Left;
                        else
                            joinType = column.Nullable ? TableJoinType.Right : TableJoinType.Inner;
                        pathBase = entity.EntityType.Name;
                        break;
                    }
                }
            }
            else if (item.Column.ForeignKey)
            {
                //other(fk) -> type(pk)
                if (item.Column.PropertyAccessor.PropertyType != type)
                    throw new EfSqlException(EfExceptionCode.IncorrectJoin);

                connectToEntity = item.QueryEntity;
                connectToColumn = item.Column;

                connectingColumn = entity.TableDescriptor.PrimaryKey;

                joinType = (item.Column.Nullable || open) ? TableJoinType.Left : TableJoinType.Inner;
                pathBase = item.Column.Name;
            }

            if (connectToEntity == null || connectToColumn == null)
                throw new EfSqlException(EfExceptionCode.IncorrectJoin);

            QueryBuilderEntity queryEntity = mSelectQueryBuilder.AddTable(entity.TableDescriptor, connectingColumn, joinType, connectToEntity, connectToColumn);
            queryEntity.EntityType = type;
            AddEntityItems(entity, queryEntity, pathBase + ".", CountTypes(queryEntity.Table) - 1);
        }

        public bool Distinct
        {
            get { return mSelectQueryBuilder.Distinct; }
            set { mSelectQueryBuilder.Distinct = value; }
        }

        public int Skip
        {
            get { return mSelectQueryBuilder.Skip; }
            set { mSelectQueryBuilder.Skip = value; }
        }

        public int Limit
        {
            get { return mSelectQueryBuilder.Limit; }
            set { mSelectQueryBuilder.Limit = value; }
        }

        public void AddToResultset(string propertyName, string alias = null)
        {
            if (!mItemIndex.TryGetValue(propertyName, out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddToResultset(item.Column, item.QueryEntity, alias);
        }

        public void AddToResultset(Type type, string propertyName, string alias = null) => AddToResultset(type, 0, propertyName, alias);

        public void AddToResultset(Type type, int occurrence, string propertyName, string alias = null)
        {
            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, occurrence, propertyName), out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddToResultset(item.Column, item.QueryEntity, alias);
        }

        public void AddToResultset(AggFn aggregate, string propertyName, string alias = null)
        {
            if (propertyName != null)
            {
                if (!mItemIndex.TryGetValue(propertyName, out EntityQueryItem item))
                    throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);
                mSelectQueryBuilder.AddToResultset(aggregate, item.Column, item.QueryEntity, alias);
            }
            else
                mSelectQueryBuilder.AddToResultset(aggregate, alias);
        }

        public void AddToResultset(AggFn aggregate, Type type, string propertyName, string alias = null) => AddToResultset(aggregate, type, 0, propertyName, alias);

        public void AddToResultset(AggFn aggregate, Type type, int occurrence, string propertyName, string alias = null)
        {
            if (type == null)
                type = mEntityDescriptor.EntityType;
            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, occurrence, propertyName), out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddToResultset(aggregate, item.Column, item.QueryEntity, alias);
        }

        public void AddExpressionToResultset(string expression, bool isaggregate, DbType dbType, string alias)
        {
            mSelectQueryBuilder.AddExpressionToResultset(expression, dbType, isaggregate, alias);
        }

        public void AddOrderBy(string propertyName, SortDir direction = SortDir.Asc)
        {
            if (!mItemIndex.TryGetValue(propertyName, out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddOrderBy(item.Column, item.QueryEntity, direction);
        }

        public void AddOrderBy(Type type, string propertyName, SortDir direction = SortDir.Asc) => AddOrderBy(type, 0, propertyName, direction);

        public void AddOrderBy(Type type, int occurrence, string propertyName, SortDir direction = SortDir.Asc)
        {
            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, occurrence, propertyName), out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddOrderBy(item.Column, item.QueryEntity, direction);
        }

        public void AddGroupBy(string propertyName)
        {
            if (!mItemIndex.TryGetValue(propertyName, out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddGroupBy(item.Column, item.QueryEntity);
        }

        public void AddGroupBy(Type type, string propertyName)
        {
            if (type == null)
                type = mEntityDescriptor.EntityType;

            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, 0, propertyName), out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddGroupBy(item.Column, item.QueryEntity);
        }

        public void AddGroupBy(Type type, int occurrence, string propertyName)
        {
            if (type == null)
                type = mEntityDescriptor.EntityType;

            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, occurrence, propertyName), out EntityQueryItem item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            mSelectQueryBuilder.AddGroupBy(item.Column, item.QueryEntity);
        }

        public void AddOrderByExpr(string expression, SortDir direction = SortDir.Asc)
        {
            mSelectQueryBuilder.AddOrderByExpr(expression, direction);
        }

        internal void AddGroupByExpr(string expression)
        {
            mSelectQueryBuilder.AddGroupByExpr(expression);
        }

        protected internal SelectQueryBuilderResultsetItem ResultColumn(int index) => mSelectQueryBuilder.ResultColumn(index);

        public QueryBuilderEntity FindType(Type type, int occurrence = 0)
        {
            occurrence++;
            EntityDescriptor entity = AllEntities.Inst[type];

            foreach (QueryBuilderEntity table in mQueryWithWhereBuilder.Entities)
            {
                if (table.Table == entity.TableDescriptor)
                {
                    occurrence--;
                    if (occurrence == 0)
                        return table;
                }
            }

            return null;
        }

        public QueryBuilderEntity AddEntity(Type type, TableJoinType joinType)
        {
            EntityDescriptor entity = AllEntities.Inst[type];
            var r = mSelectQueryBuilder.AddTable(entity.TableDescriptor, joinType);
            r.EntityType = type;
            int occurrence = CountTypes(entity.TableDescriptor) - 1;
            AddEntityItems(entity, r, $"{type.Name}{occurrence + 1}.", occurrence);
            return r;
        }
    }
}