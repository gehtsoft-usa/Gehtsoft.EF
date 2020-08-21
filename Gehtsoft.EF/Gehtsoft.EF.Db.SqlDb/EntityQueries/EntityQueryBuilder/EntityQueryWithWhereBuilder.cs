using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class EntityQueryWithWhereBuilder : EntityQueryBuilder, IEntityInfoProvider
    {
        protected QueryWithWhereBuilder mQueryWithWhereBuilder;

        protected internal class EntityQueryItem
        {
            public string Path { get; set; }
            public EntityDescriptor Entity { get; set; }
            public QueryBuilderEntity QueryEntity { get; set; }
            public TableDescriptor.ColumnInfo Column { get; set; }
            public int ResultsetColumn { get; set; } = -1;
        }

        protected List<EntityQueryItem> mItems = new List<EntityQueryItem>();
        protected Dictionary<string, EntityQueryItem> mItemIndex = new Dictionary<string, EntityQueryItem>();
        protected Dictionary<Tuple<Type, int, string>, EntityQueryItem> mTypesIndex = new Dictionary<Tuple<Type, int, string>, EntityQueryItem>();

        public EntityConditionBuilder Where { get; protected set; }

        protected EntityQueryWithWhereBuilder(SqlDbLanguageSpecifics languageSpecifics, Type type) : base(languageSpecifics, type)
        {

        }

        protected EntityQueryWithWhereBuilder(SqlDbLanguageSpecifics languageSpecifics, Type type, QueryWithWhereBuilder builder) : base(languageSpecifics, type, builder)
        {
            SetBuilder(type, builder);
        }

        protected void SetBuilder(Type type, QueryWithWhereBuilder builder)
        {
            mQueryBuilder = mQueryWithWhereBuilder = builder;
            Where = new EntityConditionBuilder(builder.Where, this);
            AddEntityItems(mEntityDescriptor, mQueryWithWhereBuilder.Entities[0], "", 0);
        }

        protected void AddEntityItems(EntityDescriptor entity, QueryBuilderEntity queryEntity, string basePath, int typeOccurrence)
        {
            foreach (TableDescriptor.ColumnInfo column in entity.TableDescriptor)
            {
                if (column.IgnoreRead)
                    continue;

                EntityQueryItem item = new EntityQueryItem()
                {
                    Path = $"{basePath}{column.ID}",
                    Entity = entity,
                    QueryEntity = queryEntity,
                    Column = column
                };

                mItems.Add(item);
                mItemIndex[item.Path] = item;
                Tuple<Type, int, string> t = new Tuple<Type, int, string>(entity.EntityType, typeOccurrence, column.ID);
                if (!mTypesIndex.ContainsKey(t))
                    mTypesIndex[t] = item;
            }
        }

        internal EntityQueryItem FindItem(string propertyName, Type type, int typeOccurrence = 0)
        {
            EntityQueryItem item = null;

            if (type == null)
                mItemIndex.TryGetValue(propertyName, out item);
            else
                mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, typeOccurrence, propertyName), out item);

            if (item == null)
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);

            return item;
        }


        internal EntityQueryItem FindPath(string path)
        {
            EntityQueryItem item = null;
            mItemIndex.TryGetValue(path, out item);
            return item;
        }

        internal string GetAlias(EntityQueryItem queryItem) => mQueryWithWhereBuilder.GetAlias(queryItem.Column, queryItem.QueryEntity);

        public string Alias(string path, out DbType columnType)
        {
            EntityQueryItem item = null;
            columnType = DbType.Object;
            if (!mItemIndex.TryGetValue(path, out item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, path);
            columnType = item.Column.DbType;
            return GetAlias(item);
        }
        
        public string Alias(Type type, int occurrence, string propertyName, out DbType columnType)
        {
            EntityQueryItem item = null;
            columnType = DbType.Object;
            if (type == null)
                type = mEntityDescriptor.EntityType;
            if (!mTypesIndex.TryGetValue(new Tuple<Type, int, string>(type, occurrence, propertyName), out item))
                throw new EfSqlException(EfExceptionCode.ColumnNotFound, propertyName);
            columnType = item.Column.DbType;
            return GetAlias(item);
        }
    }
}