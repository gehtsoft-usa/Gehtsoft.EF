using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class SelectEntityQueryBuilder : SelectEntityQueryBuilderBase
    {
        private SelectQueryTypeBinder mBinder;
        private SelectEntityQueryFilter[] mExclusions;

        public SelectQueryTypeBinder Binder => mBinder;

        public SelectEntityQueryBuilder(Type type, SqlDbConnection connection, SelectEntityQueryFilter[] exclusions = null) : base(type, connection)
        {
            mExclusions = exclusions;
            if (mExclusions != null)
            {
                foreach (SelectEntityQueryFilter filter in mExclusions)
                    if (filter.EntityType == null)
                        filter.EntityType = type;
            }
            AddEntitiesTree();
            mBinder = CreateBinder(mSelectQueryBuilder.Entities[0], mEntityDescriptor.EntityType);

        }

        protected SelectQueryTypeBinder CreateBinder(QueryBuilderEntity entity, Type type)
        {
            SelectQueryTypeBinder binder = new SelectQueryTypeBinder(type);
            foreach (TableDescriptor.ColumnInfo column in entity.Table)
            {
                if (mExclusions != null)
                {
                    bool found = false;
                    foreach (SelectEntityQueryFilter exclusion in mExclusions)
                    {
                        if (exclusion.EntityType == type && exclusion.Property == column.ID)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (found)
                        continue;
                }

                if (!column.ForeignKey)
                {
                    binder.AddBinding(mSelectQueryBuilder.Resultset.Count, column.PropertyAccessor, column.PrimaryKey);
                    mSelectQueryBuilder.AddExpressionToResultset($"{entity.Alias}.{column.Name}", column.DbType, false);
                }
                else
                {
                    bool found = false;
                    //find referenced entity
                    foreach (QueryBuilderEntity entity1 in mSelectQueryBuilder.Entities)
                    {
                        if (entity1.ConnectedToTable == entity && entity1.ConnectedToField == column)
                        {
                            binder.AddBinding(CreateBinder(entity1, column.PropertyAccessor.PropertyType), column.PropertyAccessor);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        SelectQueryTypeBinder binder1 = new SelectQueryTypeBinder(column.PropertyAccessor.PropertyType);
                        binder1.AddBinding(mSelectQueryBuilder.Resultset.Count, column.ForeignTable.PrimaryKey.PropertyAccessor, true);
                        mSelectQueryBuilder.AddExpressionToResultset($"{entity.Alias}.{column.Name}", column.DbType, false);
                        binder.AddBinding(binder1, column.PropertyAccessor);
                    }
                }
            }
            return binder;
        }
    }
}
