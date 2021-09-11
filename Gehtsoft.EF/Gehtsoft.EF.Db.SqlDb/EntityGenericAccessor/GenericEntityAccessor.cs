using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityGenericAccessor
{
    /// <summary>
    /// The CRUD accessor for an entity
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The primary key of entity type</typeparam>
    public class GenericEntityAccessor<T, TKey> where T : class
    {
        private readonly EntityDescriptor mDescriptor;

        /// <summary>
        /// Associated connection
        /// </summary>
        public SqlDbConnection Connection { get; }

        private enum KeyType
        {
            keyint,
            keystring,
            keyguid
        }

        private readonly KeyType mKeyType;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection"></param>
        public GenericEntityAccessor(SqlDbConnection connection)
        {
            Connection = connection;
            mDescriptor = AllEntities.Inst[typeof(T)];
            if (mDescriptor == null)
                throw new InvalidOperationException("The type is on an entity");
            if (typeof(TKey) == typeof(int))
                mKeyType = KeyType.keyint;
            else if (typeof(TKey) == typeof(string))
                mKeyType = KeyType.keystring;
            else if (typeof(TKey) == typeof(Guid))
                mKeyType = KeyType.keyguid;
            else
                throw new InvalidOperationException("The key type is not supported by generic accessor");
        }

        protected virtual bool IsNew(T value)
        {
            object key = mDescriptor.TableDescriptor.PrimaryKey.PropertyAccessor.GetValue(value);
            if (key == null)
                return true;
            if (mKeyType == KeyType.keyint && key is int ikey)
            {
                if (ikey < 1)
                    return true;
            }
            else if (mKeyType == KeyType.keyguid && key is Guid guid)
            {
                if (guid == Guid.Empty)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Generates new GUID primary key for the value
        ///
        /// The method makes 100 attempts to generate a new GUID key.
        ///
        /// The entity must have GUID primary key.
        /// </summary>
        /// <param name="value"></param>
        public void NewGuidKey(T value) => NewGuidKeyCore(true, value, null).SyncOp();

        /// <summary>
        /// Generates new GUID primary key for the value (async version).
        ///
        /// The method makes 100 attempts to generate a new GUID key.
        ///
        /// The entity must have GUID primary key.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="token"></param>
        public Task NewGuidKeyAsync(T value, CancellationToken? token = null) => NewGuidKeyCore(false, value, token).AsTask();

        private async ValueTask NewGuidKeyCore(bool sync, T value, CancellationToken? token)
        {
            int attempt = 0;
            while (attempt++ < 100)
            {
                using (SelectEntitiesCountQuery query = Connection.GetSelectEntitiesCountQuery<T>())
                {
                    Guid guid = Guid.NewGuid();
                    query.Where.Property(mDescriptor.TableDescriptor.PrimaryKey.PropertyAccessor.Name).Is(CmpOp.Eq).Value(guid);

                    if (sync)
                        query.Execute();
                    else
                        await query.ExecuteAsync(token);

                    if (query.RowCount == 0)
                    {
                        mDescriptor.TableDescriptor.PrimaryKey.PropertyAccessor.SetValue(value, guid);
                        return;
                    }
                }
            }
            throw new OperationCanceledException("A new key cannot be created after 100 attempts");
        }

        protected virtual async ValueTask SaveCore(bool sync, T value, CancellationToken? token)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            using (ModifyEntityQuery query = IsNew(value) ? Connection.GetInsertEntityQuery<T>() : Connection.GetUpdateEntityQuery<T>())
            {
                if (mKeyType == KeyType.keyguid && IsNew(value))
                {
                    if (sync)
                        NewGuidKey(value);
                    else
                        await NewGuidKeyAsync(value, token);
                }

                if (sync)
                    query.Execute(value);
                else
                    await query.ExecuteAsync(value, token);
            }
        }

        /// <summary>
        /// Saves entity (async version).
        ///
        /// The entity considered new and is inserted into DB when
        /// * The entity has an `int` primary key and its value is `0`
        /// * The entity has a `GUID` primary key and its value is `Guid.Empty`
        ///
        /// Entities with non-auto generated primary key aren't supported.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task SaveAsync(T value, CancellationToken? token = null) => SaveCore(false, value, token).AsTask();

        /// <summary>
        /// Saves entity.
        ///
        /// The entity considered new and is inserted into DB when
        /// * The entity has an `int` primary key and its value is `0`
        /// * The entity has a `GUID` primary key and its value is `Guid.Empty`
        ///
        /// Entities with non-auto generated primary key aren't supported.
        /// </summary>
        /// <param name="value"></param>
        public virtual void Save(T value) => SaveCore(true, value, null).SyncOp();

        protected virtual async ValueTask DeleteCore(bool sync, T value, CancellationToken? token)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            using (ModifyEntityQuery query = Connection.GetDeleteEntityQuery<T>())
            {
                if (sync)
                    query.Execute(value);
                else
                    await query.ExecuteAsync(value, token);
            }
        }

        /// <summary>
        /// Deletes the entity (async version)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task DeleteAsync(T value, CancellationToken? token = null) => DeleteCore(false, value, token).AsTask();

        /// <summary>
        /// Delete the enitity.
        /// </summary>
        /// <param name="value"></param>
        public virtual void Delete(T value) => DeleteCore(true, value, null).SyncOp();

        /// <summary>
        /// Checks whether the entity can be deleted
        ///
        /// The method checks whether the entity isn't used in other entities via foreign key references.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual bool CanDelete(T value) => Connection.CanDelete<T>(value, null);

        /// <summary>
        /// Checks whether the entity can be deleted (async version)
        ///
        /// The method checks whether the entity isn't used in other entities via foreign key references.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<bool> CanDeleteAsync(T value, CancellationToken? token = null) => Connection.CanDeleteAsync<T>(value, null, token);

        /// <summary>
        /// Checks whether the entity can be deleted with exemption of some entity types.
        ///
        /// The method checks whether the entity isn't used in other entities via foreign key references.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="exceptTypes"></param>
        /// <returns></returns>
        public virtual bool CanDelete(T value, Type[] exceptTypes) => Connection.CanDelete<T>(value, exceptTypes);

        /// <summary>
        /// Checks whether the entity can be deleted with exemption of some entity types (async version).
        ///
        /// The method checks whether the entity isn't used in other entities via foreign key references.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="exceptTypes"></param>
        /// <param name="token"></param>
        public virtual Task<bool> CanDeleteAsync(T value, Type[] exceptTypes, CancellationToken? token = null) => Connection.CanDeleteAsync<T>(value, exceptTypes, token);

        /// <summary>
        /// Deletes multiple records using the filter.
        ///
        /// The method fails if filter is `null` to avoid unintentional deletion of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public virtual int DeleteMultiple(GenericEntityAccessorFilter filter) => DeleteMultipleCore(true, filter, null).SyncResult();

        /// <summary>
        /// Deletes multiple records using the filter (async version).
        ///
        /// The method fails if filter is `null` to avoid unintentional deletion of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> DeleteMultipleAsync(GenericEntityAccessorFilter filter, CancellationToken? token = null) => DeleteMultipleCore(false, filter, token).AsTask();

        protected virtual async ValueTask<int> DeleteMultipleCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            using (MultiDeleteEntityQuery query = Connection.GetMultiDeleteEntityQuery<T>())
            {
                filter.BindToQuery(query);
                if (sync)
                    query.Execute();
                else
                    await query.ExecuteAsync(token);
                return query.RowsAffected;
            }
        }

        protected virtual async ValueTask<int> UpdateMultipleCore(bool sync, GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update, CancellationToken? token)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            if (update == null)
                throw new ArgumentNullException(nameof(update));

            using (MultiUpdateEntityQuery query = Connection.GetMultiUpdateEntityQuery<T>())
            {
                filter.BindToQuery(query);
                update.BindToQuery(query);
                if (sync)
                    query.Execute();
                else
                    await query.ExecuteAsync(token);
                return query.RowsAffected;
            }
        }

        /// <summary>
        /// Updates multiple entities using update record (async version).
        ///
        /// The method fails if filter is `null` to avoid unintentional changing of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="update"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update, CancellationToken? token = null)
            => UpdateMultipleCore(false, filter, update, token).AsTask();

        /// <summary>
        /// Updates multiple entities using update record (async version).
        ///
        /// The method fails if filter is `null` to avoid unintentional changing of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="update"></param>
        /// <returns></returns>
        public virtual int UpdateMultiple(GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update)
            => UpdateMultipleCore(true, filter, update, null).SyncResult();

        protected virtual async ValueTask<int> UpdateMultipleCore(bool sync, GenericEntityAccessorFilter filter, string property, object value, CancellationToken? token)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            using (MultiUpdateEntityQuery query = Connection.GetMultiUpdateEntityQuery<T>())
            {
                filter.BindToQuery(query);
                query.AddUpdateColumn(property, value);
                if (sync)
                    query.Execute();
                else
                    await query.ExecuteAsync(token);
                return query.RowsAffected;
            }
        }

        /// <summary>
        /// Updates one property of multiple entities (async version).
        ///
        /// The method fails if filter is `null` to avoid unintentional changing of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public virtual int UpdateMultiple(GenericEntityAccessorFilter filter, string property, object value)
            => UpdateMultipleCore(true, filter, property, value, null).SyncResult();

        /// <summary>
        /// Updates one property of multiple entities (async version).
        ///
        /// The method fails if filter is `null` to avoid unintentional changing of all records.
        ///
        /// To delete all pass a filter with no filters defined.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, string property, object value, CancellationToken? token = null)
            => UpdateMultipleCore(false, filter, property, value, token).AsTask();

        protected virtual async ValueTask<T> GetCore(bool sync, TKey id, CancellationToken? token)
        {
            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<T>())
            {
                query.Where.Property(mDescriptor.TableDescriptor.PrimaryKey.PropertyAccessor.Name).Is(CmpOp.Eq).Value(id);
                if (sync)
                    return query.ReadOne<T>();
                else
                    return await query.ReadOneAsync<T>(token);
            }
        }

        /// <summary>
        /// Reads one entity by the key.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual T Get(TKey id) => GetCore(true, id, null).SyncResult();

        /// <summary>
        /// Reads one entity by the key (async version).
        /// </summary>
        /// <param name="id"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<T> GetAsync(TKey id, CancellationToken? token = null) => GetCore(false, id, token).AsTask();

        protected virtual async ValueTask<TC> ReadCore<TC>(bool sync, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token)
            where TC : IList<T>, new()
        {
            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<T>(propertiesExclusion))
            {
                if (filter != null)
                    filter.BindToQuery(query);

                if (limit != null)
                    query.Limit = (int)limit;

                if (skip != null)
                    query.Skip = (int)skip;

                if (sortOrder != null)
                {
                    foreach (GenericEntitySortOrder order in sortOrder)
                        query.AddOrderBy(order.Path, order.Direction);
                }
                if (sync)
                    return query.ReadAll<TC, T>();
                else
                    return await query.ReadAllAsync<TC, T>(null, token);
            }
        }

        /// <summary>
        /// Reads multiple entities excluding some properties.
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <param name="propertiesExclusion"></param>
        /// <returns></returns>
        public virtual TC Read<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion)
            where TC : IList<T>, new()
            => ReadCore<TC>(true, filter, sortOrder, skip, limit, propertiesExclusion, null).SyncResult();

        /// <summary>
        /// Reads multiple entities excluding some properties (async version).
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <param name="propertiesExclusion"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<TC> ReadAsync<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token = null)
            where TC : IList<T>, new()
            => ReadCore<TC>(false, filter, sortOrder, skip, limit, propertiesExclusion, token).AsTask();

        /// <summary>
        /// Reads multiple entities.
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <returns></returns>
        public virtual TC Read<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit)
            where TC : IList<T>, new()
            => ReadCore<TC>(true, filter, sortOrder, skip, limit, null, null).SyncResult();

        /// <summary>
        /// Reads multiple entities (async version).
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <param name="filter"></param>
        /// <param name="sortOrder"></param>
        /// <param name="skip"></param>
        /// <param name="limit"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<TC> ReadAsync<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, CancellationToken? token = null)
            where TC : IList<T>, new()
            => ReadCore<TC>(false, filter, sortOrder, skip, limit, null, token).AsTask();

        protected virtual async ValueTask<int> CountCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
        {
            using (SelectEntitiesCountQuery query = Connection.GetSelectEntitiesCountQuery<T>())
            {
                filter?.BindToQuery(query);
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount;
            }
        }

        /// <summary>
        /// Counts entities
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public virtual int Count(GenericEntityAccessorFilter filter) => CountCore(true, filter, null).SyncResult();

        /// <summary>
        /// Counts entities (async version).
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<int> CountAsync(GenericEntityAccessorFilter filter, CancellationToken? token = null) => CountCore(false, filter, token).AsTask();

        private void PrepareNextEntity(SelectEntitiesQuery query, T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false)
        {
            filter?.BindToQuery(query);

            if (entity != null)
            {
                GenericEntitySortOrder[] sortOrderArr = sortOrder.ToArray();

                if (sortOrderArr.Length == 0)
                    throw new ArgumentException("Sort order is required for the operation", nameof(sortOrder));
                else if (sortOrderArr.Length == 1)
                {
                    query.Where.Property(sortOrderArr[0].Path)
                        .Is(sortOrderArr[0].GetDirection(reverseDirection) == SortDir.Asc ? CmpOp.Gt : CmpOp.Ls)
                        .Value(EntityPathAccessor.ReadData(entity, sortOrderArr[0].Path));
                }
                else
                {
                    using (var group1 = query.Where.AddGroup())
                    {
                        for (int i = sortOrderArr.Length; i >= 1; i--)
                        {
                            using (var group2 = query.Where.AddGroup(LogOp.Or))
                            {
                                for (int j = 0; j < i; j++)
                                {
                                    CmpOp op;
                                    if (j == (i - 1))
                                    {
                                        if (sortOrderArr[j].GetDirection(reverseDirection) == SortDir.Asc)
                                            op = CmpOp.Gt;
                                        else
                                            op = CmpOp.Ls;
                                    }
                                    else
                                        op = CmpOp.Eq;

                                    query.Where.Property(sortOrderArr[j].Path).Is(op).Value(EntityPathAccessor.ReadData(entity, sortOrderArr[j].Path));
                                }
                            }
                        }
                    }
                }
            }
            foreach (GenericEntitySortOrder order in sortOrder)
                query.AddOrderBy(order.Path, order.GetDirection(reverseDirection));

            query.Limit = 1;
        }

        protected virtual async ValueTask<T> NextEntityCore(bool sync, T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token)
        {
            if (sortOrder == null)
                throw new ArgumentException("Sort order is required for the operation", nameof(sortOrder));

            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<T>(propertiesExclusion))
            {
                PrepareNextEntity(query, entity, sortOrder, filter, reverseDirection);
                if (sync)
                    return query.ReadOne<T>();
                else
                    return await query.ReadOneAsync<T>(token);
            }
        }

        /// <summary>
        /// Gets next or previous entity in the sort order specified
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="sortOrder"></param>
        /// <param name="filter"></param>
        /// <param name="reverseDirection"></param>
        /// <param name="propertiesExclusion"></param>
        /// <returns></returns>
        public virtual T NextEntity(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, SelectEntityQueryFilter[] propertiesExclusion = null)
            => NextEntityCore(true, entity, sortOrder, filter, reverseDirection, propertiesExclusion, null).SyncResult();

        /// <summary>
        /// Gets next or previous entity in the sort order specified (async version).
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="sortOrder"></param>
        /// <param name="filter"></param>
        /// <param name="reverseDirection"></param>
        /// <param name="propertiesExclusion"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<T> NextEntityAsync(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, SelectEntityQueryFilter[] propertiesExclusion = null, CancellationToken? token = null)
            => NextEntityCore(false, entity, sortOrder, filter, reverseDirection, propertiesExclusion, token).AsTask();

        private SelectEntityQueryFilter[] gIDOnly = null;

        protected SelectEntityQueryFilter[] IDOnly
        {
            get
            {
                if (gIDOnly == null)
                {
                    gIDOnly = new SelectEntityQueryFilter[mDescriptor.TableDescriptor.Count - 1];
                    int idx = 0;
                    foreach (TableDescriptor.ColumnInfo column in mDescriptor.TableDescriptor)
                    {
                        if (column.PrimaryKey)
                            continue;
                        gIDOnly[idx++] = new SelectEntityQueryFilter() { Property = column.ID };
                    }
                }
                return gIDOnly;
            }
        }

        protected virtual async ValueTask<TKey> NextKeyCore(bool sync, T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection, CancellationToken? token)
        {
            if (sortOrder == null)
                throw new ArgumentException("Sort order is required for the operation", nameof(sortOrder));

            using (SelectEntitiesQuery query = Connection.GetSelectEntitiesQuery<T>(IDOnly))
            {
                PrepareNextEntity(query, entity, sortOrder, filter, reverseDirection);
                bool rc;
                if (sync)
                {
                    query.Execute();
                    rc = query.ReadNext();
                }
                else
                {
                    await query.ExecuteAsync(token);
                    rc = await query.ReadNextAsync(token);
                }

                if (!rc)
                    return default;
                else
                    return query.GetValue<TKey>(0);
            }
        }

        /// <summary>
        /// Gets the key of next or previous entity in the sort order specified
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="sortOrder"></param>
        /// <param name="filter"></param>
        /// <param name="reverseDirection"></param>
        /// <returns></returns>
        public virtual TKey NextKey(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false)
            => NextKeyCore(true, entity, sortOrder, filter, reverseDirection, null).SyncResult();

        /// <summary>
        /// Gets the key of next or previous entity in the sort order specified (async version)
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="sortOrder"></param>
        /// <param name="filter"></param>
        /// <param name="reverseDirection"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<TKey> NextKeyAsync(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, CancellationToken? token = null)
            => NextKeyCore(false, entity, sortOrder, filter, reverseDirection, token).AsTask();
    }
}
