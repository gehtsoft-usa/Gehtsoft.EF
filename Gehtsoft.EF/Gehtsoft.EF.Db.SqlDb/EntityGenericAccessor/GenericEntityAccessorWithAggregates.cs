using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    /// <summary>
    /// The action for widow aggregates 
    /// </summary>
    public enum OnWidowNextContentAction
    {
        /// <summary>
        /// Ignore
        /// </summary>
        Ignore,
        /// <summary>
        /// Add to database
        /// </summary>
        Insert,
        /// <summary>
        /// Raise exception
        /// </summary>
        Exception,
    }

    internal class ColumnReferences
    {
        private readonly ConcurrentDictionary<Tuple<Type, Type>, TableDescriptor.ColumnInfo> gReferringColumn = new ConcurrentDictionary<Tuple<Type, Type>, TableDescriptor.ColumnInfo>();

        private static ColumnReferences gInst;

        public static ColumnReferences Inst => gInst ?? (gInst = new ColumnReferences());

        public TableDescriptor.ColumnInfo this[Tuple<Type, Type> key]
        {
            get
            {
                if (!gReferringColumn.TryGetValue(key, out TableDescriptor.ColumnInfo value))
                    throw new ArgumentOutOfRangeException(nameof(key), "The key is not found");
                return value;
            }
            set
            {
                gReferringColumn.AddOrUpdate(key, value, (k, v) => value);
            }
        }

        public bool ContainsKey(Tuple<Type, Type> key) => gReferringColumn.ContainsKey(key);
    }

    /// <summary>
    /// The CRUD accessor for an entity with aggregated.
    ///
    /// Aggregate is an entity related to the main entity with one to many. An example of
    /// aggregates would be order details for an order.
    /// 
    /// The accessor modifies some operations, e.g. when an aggregating entity is deleted,
    /// all aggregates are deleted automatically.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    public class GenericEntityAccessorWithAggregates<T, TKey> : GenericEntityAccessor<T, TKey> where T : class
    {
        private readonly TableDescriptor.ColumnInfo mPK;
        private readonly Type[] mAggregates;

        /// <summary>
        /// Constructor for one aggregated type.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="aggregate"></param>
        public GenericEntityAccessorWithAggregates(SqlDbConnection connection, Type aggregate) : this(connection, new Type[] { aggregate })
        {
        }

        /// <summary>
        /// Constructor for multiple aggregated types.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="aggregates"></param>
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

                if (!ColumnReferences.Inst.ContainsKey(key))
                {
                    foreach (TableDescriptor.ColumnInfo column in descriptor1.TableDescriptor)
                    {
                        if (column.ForeignKey && column.ForeignTable.Name == descriptor.TableDescriptor.Name)
                        {
                            ColumnReferences.Inst[key] = column;
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets aggregates of the aggregating entity specified.
        /// </summary>
        /// <typeparam name="TAC">The type of the aggregates collection.</typeparam>
        /// <typeparam name="TA">The type of the aggregate</typeparam>
        /// <param name="entity"></param>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <param name="propertiesExclusion"></param>
        /// <returns></returns>
        public virtual TAC GetAggregates<TAC, TA>(T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TAC : IList<TA>, new()
            where TA : class, new()
        {
            return GetAggregatesCore<TAC, TA>(true, entity, filter, sortOrder, skip, limit, propertiesExclusion).SyncResult();
        }

        /// <summary>
        /// Gets aggregates of the aggregating entity specified (async version).
        /// </summary>
        /// <typeparam name="TAC"></typeparam>
        /// <typeparam name="TA"></typeparam>
        /// <param name="entity"></param>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <param name="propertiesExclusion"></param>
        /// <returns></returns>
        public virtual Task<TAC> GetAggregatesAsync<TAC, TA>(T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TAC : IList<TA>, new()
            where TA : class, new()
        {
            return GetAggregatesCore<TAC, TA>(false, entity, filter, sortOrder, skip, limit, propertiesExclusion).AsTask();
        }

        protected virtual async ValueTask<TAC> GetAggregatesCore<TAC, TA>(bool sync, T entity, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null)
            where TAC : IList<TA>, new()
            where TA : class, new()
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));

            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<TA>(propertiesExclusion))
            {
                query.Where.Property(ColumnReferences.Inst[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(entity));
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

        /// <summary>
        /// Gets count of the aggregates.
        /// </summary>
        /// <typeparam name="TA"></typeparam>
        /// <param name="entity"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public virtual int GetAggregatesCount<TA>(T entity, GenericEntityAccessorFilter filter)
            where TA : class, new()
        {
            return GetAggregatesCountCore<TA>(true, entity, filter, null).SyncResult();
        }

        /// <summary>
        /// Gets count of the aggregates (async version).
        /// </summary>
        /// <typeparam name="TA"></typeparam>
        /// <param name="entity"></param>
        /// <param name="filter"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> GetAggregatesCountAsync<TA>(T entity, GenericEntityAccessorFilter filter, CancellationToken? token = null)
            where TA : class, new()
        {
            return GetAggregatesCountCore<TA>(false, entity, filter, token).AsTask();
        }

        protected virtual async ValueTask<int> GetAggregatesCountCore<TA>(bool sync, T entity, GenericEntityAccessorFilter filter, CancellationToken? token)
            where TA : class, new()
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));

            using (SelectEntitiesCountQuery query = Connection.GetSelectEntitiesCountQuery<TA>())
            {
                query.Where.Property(ColumnReferences.Inst[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(entity));
                filter?.BindToQuery(query);
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount;
            }
        }

        protected override async ValueTask DeleteCore(bool sync, T value, CancellationToken? token)
        {
            foreach (Type type in mAggregates)
            {
                using (MultiDeleteEntityQuery query = Connection.GetMultiDeleteEntityQuery(type))
                {
                    Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), type);
                    query.Where.Property(ColumnReferences.Inst[key].ID).Is(CmpOp.Eq).Value(mPK.PropertyAccessor.GetValue(value));
                    if (sync)
                        query.Execute();
                    else
                        await query.ExecuteAsync(token);
                }
            }

            if (sync)
                base.DeleteCore(true, value, token).ConfigureAwait(false).GetAwaiter().GetResult();
            else
                await base.DeleteCore(false, value, token);
        }

        [DocgenIgnore]
        public override bool CanDelete(T value) => Connection.CanDelete<T>(value, mAggregates);

        [DocgenIgnore]
        public override Task<bool> CanDeleteAsync(T value, CancellationToken? token = null) => Connection.CanDeleteAsync<T>(value, mAggregates, token);

        protected override async ValueTask<int> DeleteMultipleCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
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
                        query.Where.Property(ColumnReferences.Inst[key].ID).Is(CmpOp.In).Query(subquery);
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

        /// <summary>
        /// Saves the collection of aggregates
        /// </summary>
        /// <typeparam name="TA">The type of aggregated entity</typeparam>
        /// <param name="entity">The aggregating entity</param>
        /// <param name="originalAggregates">The current state of the aggregates in the DB</param>
        /// <param name="newAggregates">The new state of the aggregates</param>
        /// <param name="areDataEqual">The function to check whether two aggregates are the same. The function is used to identify the aggregates that are changed.</param>
        /// <param name="areIDEqual">The function to check whether primary key of two aggregates are the same. The function is used to compare the old and new state of the aggregates.</param>
        /// <param name="isDefined">The function to check whether the aggregate record is defined. Undefined records are ignore. </param>
        /// <param name="isNew">The function to check whether the aggregate record is a new one.</param>
        /// <param name="widowAction">The action to perform on the aggregates which aren't new and do not exist in the current state.</param>
        /// <returns></returns>
        public virtual int SaveAggregates<TA>(T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction = OnWidowNextContentAction.Ignore) where TA : class
        {
            return SaveAggregatesCore(true, entity, originalAggregates, newAggregates, areDataEqual, areIDEqual, isDefined, isNew, widowAction, null)
                .SyncResult();
        }

        /// <summary>
        /// Saves the collection of aggregates (async version).
        /// 
        /// See <see cref="SaveAggregates{TA}(T, IEnumerable{TA}, IEnumerable{TA}, Func{TA, TA, bool}, Func{TA, TA, bool}, Func{TA, bool}, Func{TA, bool}, OnWidowNextContentAction)"/>
        /// for details.
        /// </summary>
        /// <typeparam name="TA"></typeparam>
        /// <param name="entity"></param>
        /// <param name="originalAggregates"></param>
        /// <param name="newAggregates"></param>
        /// <param name="areDataEqual"></param>
        /// <param name="areIDEqual"></param>
        /// <param name="isDefined"></param>
        /// <param name="isNew"></param>
        /// <param name="widowAction"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> SaveAggregatesAsync<TA>(T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction = OnWidowNextContentAction.Ignore, CancellationToken? token = null) where TA : class
        {
            return SaveAggregatesCore(false, entity, originalAggregates, newAggregates, areDataEqual, areIDEqual, isDefined, isNew, widowAction, token).AsTask();
        }

        protected virtual async ValueTask<int> SaveAggregatesCore<TA>(bool sync, T entity, IEnumerable<TA> originalAggregates, IEnumerable<TA> newAggregates, Func<TA, TA, bool> areDataEqual, Func<TA, TA, bool> areIDEqual, Func<TA, bool> isDefined, Func<TA, bool> isNew, OnWidowNextContentAction widowAction, CancellationToken? token) where TA : class
        {
            Tuple<Type, Type> key = new Tuple<Type, Type>(typeof(T), typeof(TA));
            TableDescriptor.ColumnInfo refColumn = ColumnReferences.Inst[key];

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
                        throw new InvalidOperationException("An attempt to update the aggregate that does not exists");
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