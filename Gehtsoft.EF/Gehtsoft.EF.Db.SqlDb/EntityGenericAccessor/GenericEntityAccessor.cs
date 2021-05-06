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
    public class GenericEntityAccessor<T, TKey> where T : class
    {
        private readonly EntityDescriptor mDescriptor;

        public SqlDbConnection Connection { get; }

        private enum KeyType
        {
            keyint,
            keystring,
            keyguid
        }

        private readonly KeyType mKeyType;

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
                    return false;
            }
            return false;
        }

        public void NewGuidKey(T value) => NewGuidKeyCore(true, value, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task NewGuidKeyAsync(T value) => NewGuidKeyAsync(value, null);

        public Task NewGuidKeyAsync(T value, CancellationToken? token) => NewGuidKeyCore(false, value, token);

        private async Task NewGuidKeyCore(bool sync, T value, CancellationToken? token)
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

        protected virtual async Task SaveCore(bool sync, T value, CancellationToken? token)
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

        public Task SaveAsync(T value) => SaveAsync(value, null);

        public virtual Task SaveAsync(T value, CancellationToken? token) => SaveCore(false, value, token);

        public virtual void Save(T value) => SaveCore(true, value, null).ConfigureAwait(false).GetAwaiter().GetResult();

        protected virtual async Task DeleteCore(bool sync, T value, CancellationToken? token)
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

        public Task DeleteAsync(T value) => DeleteAsync(value, null);

        public virtual Task DeleteAsync(T value, CancellationToken? token) => DeleteCore(false, value, token);

        public virtual void Delete(T value) => DeleteCore(true, value, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public virtual bool CanDelete(T value) => Connection.CanDelete<T>(value, null);

        public Task<bool> CanDeleteAsync(T value) => CanDeleteAsync(value, null, null);

        public virtual Task<bool> CanDeleteAsync(T value, CancellationToken? token) => Connection.CanDeleteAsync<T>(value, null, token);

        public virtual bool CanDelete(T value, Type[] exceptTypes) => Connection.CanDelete<T>(value, exceptTypes);

        public Task<bool> CanDeleteAsync(T value, Type[] exceptTypes) => CanDeleteAsync(value, exceptTypes, null);

        public virtual Task<bool> CanDeleteAsync(T value, Type[] exceptTypes, CancellationToken? token) => Connection.CanDeleteAsync<T>(value, exceptTypes, token);

        public virtual int DeleteMultiple(GenericEntityAccessorFilter filter) => DeleteMultipleCore(true, filter, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<int> DeleteMultipleAsync(GenericEntityAccessorFilter filter) => DeleteMultipleAsync(filter, null);
        public virtual Task<int> DeleteMultipleAsync(GenericEntityAccessorFilter filter, CancellationToken? token) => DeleteMultipleCore(false, filter, token);

        protected virtual async Task<int> DeleteMultipleCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
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

        protected virtual async Task<int> UpdateMultipleCore(bool sync, GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update, CancellationToken? token)
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

        public Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update) => UpdateMultipleAsync(filter, update, null);
        public virtual Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update, CancellationToken? token) => UpdateMultipleCore(false, filter, update, token);

        public virtual int UpdateMultiple(GenericEntityAccessorFilter filter, GenericEntityAccessorUpdateRecord update) => UpdateMultipleCore(true, filter, update, null).ConfigureAwait(false).GetAwaiter().GetResult();

        protected virtual async Task<int> UpdateMultipleCore(bool sync, GenericEntityAccessorFilter filter, string property, object value, CancellationToken? token)
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

        public virtual int UpdateMultiple(GenericEntityAccessorFilter filter, string property, object value) => UpdateMultipleCore(true, filter, property, value, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, string property, object value) => UpdateMultipleAsync(filter, property, value, null);
        public virtual Task<int> UpdateMultipleAsync(GenericEntityAccessorFilter filter, string property, object value, CancellationToken? token) => UpdateMultipleCore(false, filter, property, value, token);

        protected virtual async Task<T> GetCore(bool sync, TKey id, CancellationToken? token)
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

        public virtual T Get(TKey id) => GetCore(true, id, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public virtual Task<T> GetAsync(TKey id) => GetAsync(id, null);

        public virtual Task<T> GetAsync(TKey id, CancellationToken? token) => GetCore(false, id, token);

        protected virtual async Task<TC> ReadCore<TC>(bool sync, GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token) where TC : EntityCollection<T>, new()
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

        public virtual TC Read<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion = null) where TC : EntityCollection<T>, new()
            => ReadCore<TC>(true, filter, sortOrder, skip, limit, propertiesExclusion, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<TC> ReadAsync<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit) where TC : EntityCollection<T>, new()
            => ReadAsync<TC>(filter, sortOrder, skip, limit, null, null);

        public Task<TC> ReadAsync<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion) where TC : EntityCollection<T>, new()
            => ReadAsync<TC>(filter, sortOrder, skip, limit, propertiesExclusion, null);

        public virtual Task<TC> ReadAsync<TC>(GenericEntityAccessorFilter filter, IEnumerable<GenericEntitySortOrder> sortOrder, int? skip, int? limit, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token) where TC : EntityCollection<T>, new()
            => ReadCore<TC>(false, filter, sortOrder, skip, limit, propertiesExclusion, token);

        protected virtual async Task<int> CountCore(bool sync, GenericEntityAccessorFilter filter, CancellationToken? token)
        {
            using (SelectEntitiesCountQuery query = Connection.GetSelectEntitiesCountQuery<T>())
            {
                filter?.BindToQuery(query);
                if (!sync)
                    await query.ExecuteAsync(token);
                return query.RowCount;
            }
        }

        public virtual int Count(GenericEntityAccessorFilter filter) => CountCore(true, filter, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public Task<int> CountAsync(GenericEntityAccessorFilter filter) => CountAsync(filter, null);

        public virtual Task<int> CountAsync(GenericEntityAccessorFilter filter, CancellationToken? token) => CountCore(false, filter, token);

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

        protected virtual async Task<T> NextEntityCore(bool sync, T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection, SelectEntityQueryFilter[] propertiesExclusion, CancellationToken? token)
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

        public virtual T NextEntity(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, SelectEntityQueryFilter[] propertiesExclusion = null)
            => NextEntityCore(true, entity, sortOrder, filter, reverseDirection, propertiesExclusion, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public virtual Task<T> NextEntityAsync(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, SelectEntityQueryFilter[] propertiesExclusion = null, CancellationToken? token = null)
            => NextEntityCore(false, entity, sortOrder, filter, reverseDirection, propertiesExclusion, token);

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

        protected virtual async Task<TKey> NextKeyCore(bool sync, T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection, CancellationToken? token)
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

        public virtual TKey NextKey(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false)
            => NextKeyCore(true, entity, sortOrder, filter, reverseDirection, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public virtual Task<TKey> NextKeyAsync(T entity, IEnumerable<GenericEntitySortOrder> sortOrder, GenericEntityAccessorFilter filter, bool reverseDirection = false, CancellationToken? token = null)
            => NextKeyCore(false, entity, sortOrder, filter, reverseDirection, token);
    }
}
