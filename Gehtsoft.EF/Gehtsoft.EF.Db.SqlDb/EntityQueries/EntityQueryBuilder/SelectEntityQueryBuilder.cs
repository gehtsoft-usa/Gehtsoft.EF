using System;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class SelectEntityQueryBuilder : SelectEntityQueryBuilderBase
    {
        private readonly SelectEntityQueryFilter[] mExclusions;

        public SelectQueryResultBinder Binder { get; }

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
            Binder = CreateBinder(mSelectQueryBuilder.Entities[0], mEntityDescriptor.EntityType);
        }

        protected SelectQueryResultBinder CreateBinder(QueryBuilderEntity entity, Type type)
        {
            SelectQueryResultBinder binder = new SelectQueryResultBinder(type);
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
                    binder.AddBinding(mSelectQueryBuilder.Resultset.Count, column.Name, column.PropertyAccessor, column.PrimaryKey);
                    mSelectQueryBuilder.AddExpressionToResultset($"{entity.Alias}.{column.Name}", column.DbType, false, $"{entity.Alias}_{column.Name}");
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
                        var binder1 = new SelectQueryResultBinder(column.PropertyAccessor.PropertyType);
                        binder1.AddBinding(mSelectQueryBuilder.Resultset.Count, column.Name, column.ForeignTable.PrimaryKey.PropertyAccessor, true);
                        mSelectQueryBuilder.AddExpressionToResultset($"{entity.Alias}.{column.Name}", column.DbType, false, $"{entity.Alias}_{column.Name}");
                        binder.AddBinding(binder1, column.PropertyAccessor);
                    }
                }
            }
            return binder;
        }
    }
}
