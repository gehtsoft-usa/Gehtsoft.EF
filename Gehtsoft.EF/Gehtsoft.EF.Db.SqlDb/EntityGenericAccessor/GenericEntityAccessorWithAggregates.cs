using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    public enum OnWidowNextContentAction
    {
        Ignore,
        Insert,
        Exception,
    }

    public class GenericEntityAccessorWithAggregates<T, TKey> : GenericEntityAccessor<T, TKey> where T : class
    {
        private static readonly Dictionary<Tuple<Type, Type>, TableDescriptor.ColumnInfo> gReferringColumn = new Dictionary<Tuple<Type, Type>, TableDescriptor.ColumnInfo>();
        private readonly TableDescriptor.ColumnInfo mPK;
        private readonly Type[] mAggregates;

        public GenericEntityAccessorWithAggregates(SqlDbConnection connection, Type aggregate) : this(connection, new Type[] {aggregate})
        {

        }

        public GenericEntityAccessorWithAggregates(SqlDbConnection connection, IEnumerable<Type> aggregates) : base(connection)
        {
            EntityDescriptor descriptor = AllEntities.Inst[typeof(T)];

            foreach (TableDescriptor.ColumnInfo column in descriptor.TableDescriptor)
            {
                if (column.PrimaryKey)
                {
                    mPK = column;
                    break;
                }
            }

            mAggregates = aggregates.ToArray();

            foreach (Type type in mAggregates)
            {
                EntityDescriptor descriptor1 = AllEntities.Inst[type];
                Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), type);

                if (!gReferringColumn.ContainsKey(key))
                {
                    foreach (TableDescriptor.ColumnInfo column in descriptor1.TableDescriptor)
                    {
                        if (column.ForeignKey && column.ForeignTable.Name == descriptor.TableDescriptor.Name)
                        {
                            gReferringColumn[key] = column;
                            break;
                        }
                    }
                }
            }
        }

        public virtual TAC GetAggregates<TAC, TA>(T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TA : class, new()
            where TAC : EntityCollection<TA>, new()
        {
            return GetAggregatesCore<TAC, TA>(true, entity, filter, sortOrder, skip, limit, propertiesExclusion)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public virtual Task<TAC> GetAggregatesAsync<TAC, TA>(T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TA : class, new()
            where TAC : EntityCollection<TA>, new()
        {
            return GetAggregatesCore<TAC, TA>(false, entity, filter, sortOrder, skip, limit, propertiesExclusion);
        }

        protected virtual async Task<TAC> GetAggregatesCore<TAC, TA>(bool sync, T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TA : class, new()
            where TAC : EntityCollection<TA>, new()
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));

            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<TA>(propertiesExclusion))
            {
                query.Where.Property(gReferringColumn[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(entity));
                filter?.BindToQuery(query);
                if (sortOrder != null)
                {
                    foreach (GenericEntitySortOrder order in sortOrder)
                        query.AddOrderBy(order.Path, order.Direction);
                }

                if (skip != null)
                    query.Skip = (int)skip;
                if (limit != null)
                    query.Skip = (int)limit;
                if (sync)
                    return query.ReadAll<TAC, TA>();
                else
                    return await query.ReadAllAsync<TAC, TA>();
            }
        }
        public virtual int GetAggregatesCount<TA>(T entity, GenericEntityAccessorFilter filter)
            where TA : class, new()
        {
            return GetAggregatesCountCore<TA>(false, entity, filter, null).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public virtual Task<int> GetAggregatesCountAsync<TA>(T entity, GenericEntityAccessorFilter filter)
            where TA : class, new() => GetAggregatesCountAsync<TA>(entity, filter, null);

        public virtual Task<int> GetAggregatesCountAsync<TA>(T entity, GenericEntityAccessorFilter filter, CancellationToken? token)
            where TA : class, new()
        {
            return GetAggregatesCountCore<TA>(false, entity, filter, token);
        }
        protected virtual async Task<int> GetAggregatesCountCore<TA>(bool sync, T entity, GenericEntityAccessorFilter filter, CancellationToken? token)
            where TA : class, new()
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));

            using (SelectEntitiesCountQuery query = Connection.GetSelectEntitiesCountQuery<TA>())
            {
                query.Where.Property(gReferringColumn[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(entity));
                filter?.BindToQuery(query);
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount;
            }
        }

        protected override async Task DeleteCore(bool sync, T entity, CancellationToken? token)
        {
            foreach (Type type in mAggregates)
            {
                using (MultiDeleteEntityQuery query = Connection.GetMultiDeleteEntityQuery(type))
                {
                    Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), type);
                    query.Where.Property(gReferringColumn[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(entity));
                    if (sync)
                        query.Execute();
                    else
                        await query.ExecuteAsync(token);
                }
            }

            if (sync)
                base.DeleteCore(true, entity, token).ConfigureAwait(false).GetAwaiter().GetResult();
            else
                await base.DeleteCore(false, entity, token);

        }

        public override bool CanDelete(T value) => Connection.CanDelete<T>(value, mAggregates);

        public override Task<bool> CanDeleteAsync(T value, CancellationToken? token) => Connection.CanDeleteAsync<T>(value, mAggregates, token);

        protected override async Task<int> DeleteMultipleCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));


            using (SelectEntitiesQuery subquery = Connection.GetSelectEntitiesQuery<T>(IDOnly))
            {
                subquery.WhereParamPrefix = "autoparamsq";
                filter.BindToQuery(subquery);

                foreach (Type type in mAggregates)
                {
                    using (MultiDeleteEntityQuery query = Connection.GetMultiDeleteEntityQuery(type))
                    {
                        Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), type);
                        query.Where.Property(gReferringColumn[key].ID).Is(CmpOp.In).Query(subquery);
                        if (sync)
                            query.Execute();
                        else
                            await query.ExecuteAsync(token);
                    }
                }
            }

            if (sync)
                return base.DeleteMultipleCore(true, filter, token).ConfigureAwait(false).GetAwaiter().GetResult();
            else
                return await base.DeleteMultipleCore(false, filter, token);
        }

        private static TA FindInArray<TA>(IEnumerable<TA> array, TA findA, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined) where TA : class
        {
            foreach (TA a in array)
            {
                if (a == null || !isDefined(a))
                    continue;
                if (areIDEqual(a, findA))
                    return a;
            }

            return null;
        }

        public virtual int SaveAggregates<TA>(T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction = OnWidowNextContentAction.Ignore) where TA : class
        {
            return SaveAggregatesCore(true, entity, originalAggregates, newAggregates, areDataEqual, areIDEqual, isDefined, isNew, widowAction, null)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public virtual Task<int> SaveAggregatesAsync<TA>(T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction = OnWidowNextContentAction.Ignore, CancellationToken? token = null) where TA : class
        {
            return SaveAggregatesCore(false, entity, originalAggregates, newAggregates, areDataEqual, areIDEqual, isDefined, isNew, widowAction, token);
        }

        protected virtual async Task<int> SaveAggregatesCore<TA>(bool sync, T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction, CancellationToken? token) where TA : class
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));
            TableDescriptor.ColumnInfo refColumn = gReferringColumn[key];

            ModifyEntityQuery insertQuery = Connection.GetInsertEntityQuery<TA>();
            ModifyEntityQuery saveQuery = Connection.GetUpdateEntityQuery<TA>();
            ModifyEntityQuery deleteQuery = Connection.GetDeleteEntityQuery<TA>();

            int count = 0;

            try
            {
                foreach (TA orgContent in originalAggregates)
                {
                    if (FindInArray<TA>(newAggregates, orgContent, areIDEqual, isDefined) == null)
                    {
                        count++;
                        if (sync)
                            deleteQuery.Execute(orgContent);
                        else
                            await deleteQuery.ExecuteAsync(orgContent, token);
                    }
                }

                foreach (TA content in newAggregates)
                {
                    if (content == null || !isDefined(content))
                        continue;

                    if (isNew(content))
                    {
                        refColumn.PropertyAccessor.SetValue(content, entity);
                        if (sync)
                            insertQuery.Execute(content);
                        else
                            await insertQuery.ExecuteAsync(content, token);
                        count++;
                        continue;
                    }

                    TA orgContent = FindInArray<TA>(originalAggregates, content, areIDEqual, isDefined);

                    if (orgContent == null)
                    {
                        switch (widowAction)
                        {
                            case OnWidowNextContentAction.Ignore:
                                continue;
                            case OnWidowNextContentAction.Insert:
                                refColumn.PropertyAccessor.SetValue(content, entity);
                                if (sync)
                                    insertQuery.Execute(content);
                                else
                                    await insertQuery.ExecuteAsync(content, token);
                                count++;
                                continue;
                        }
                        throw new Exception("An attempt to update the aggregate that does not exists");
                    }

                    if (areDataEqual(content, orgContent))
                        continue;

                    refColumn.PropertyAccessor.SetValue(content, entity);
                    if (sync)
                        saveQuery.Execute(content);
                    else
                        await saveQuery.ExecuteAsync(content, token);
                    count++;
                }
            }
            finally
            {
                insertQuery.Dispose();
                saveQuery.Dispose();
                deleteQuery.Dispose();
            }

            return count;
        }
    }
}