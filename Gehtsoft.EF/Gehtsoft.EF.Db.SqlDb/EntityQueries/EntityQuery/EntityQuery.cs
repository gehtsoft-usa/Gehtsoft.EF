using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class EntityQuery : IDbQuery
    {
        protected SqlDbQuery mQuery;
        protected EntityQueryBuilder mBuilder;

        public SqlDbQuery Query => mQuery;
        public EntityQueryBuilder Builder => mBuilder;
        public SqlDbConnection Connection => mQuery.Connection;

        public virtual bool IsInsert => false;

        public bool CanRead
        {
            get { return mQuery.CanRead; }
        }

        public SqlDbLanguageSpecifics LanguageSpecifics
        {
            get { return mQuery.LanguageSpecifics; }
        }

        internal EntityQuery(SqlDbQuery query, EntityQueryBuilder builder)
        {
            mQuery = query;
            mBuilder = builder;
        }

        protected virtual void PrepareQuery()
        {
            if (string.IsNullOrEmpty(mQuery.CommandText))
            {
                if (mBuilder.QueryBuilder.Query == null)
                    mBuilder.QueryBuilder.PrepareQuery();
                mQuery.CommandText = mBuilder.QueryBuilder.Query;
            }
        }

        public virtual int Execute()
        {
            PrepareQuery();
            return mQuery.ExecuteNoData();
        }

        public Task<int> ExecuteAsync() => ExecuteAsync(null);

        public virtual async Task<int> ExecuteAsync(CancellationToken? token)
        {
            PrepareQuery();
            return await mQuery.ExecuteNoDataAsync(token);
        }

        public void BindParam(string name, ParameterDirection direction, object value, Type valueType)
        {
            mQuery.BindParam(name, direction, value, valueType);
        }

        public virtual void BindParam<T>(string name, T value) => mQuery.BindParam<T>(name, value);

        public object GetParamValue(string name, Type type)
        {
            return mQuery.GetParamValue(name, type);
        }

        public T GetParamValue<T>(string name)
        {
            return mQuery.GetParamValue<T>(name);
        }

        public int RowsAffected { get; protected set; } = 0;

        public int ExecuteNoData()
        {
            RowsAffected = mQuery.ExecuteNoData();
            return RowsAffected;
        }

        public void ExecuteReader()
        {
            mQuery.ExecuteReader();
        }

        public Task<int> ExecuteNoDataAsync() => ExecuteNoDataAsync(null);

        public Task<int> ExecuteNoDataAsync(CancellationToken? token)
        {
            return mQuery.ExecuteNoDataAsync(token);
        }

        public Task ExecuteReaderAsync() => ExecuteReaderAsync(null);

        public Task ExecuteReaderAsync(CancellationToken? token)
        {
            return mQuery.ExecuteReaderAsync(token);
        }

        public virtual void BindNull(string name, DbType type) => mQuery.BindNull(name, type);

        public void BindOutputParam(string name, DbType type)
        {
            mQuery.BindOutputParam(name, type);
        }

        public int FieldCount => mQuery.FieldCount;

        public SqlDbQuery.FieldInfo Field(string name) => mQuery.Field(name);

        public SqlDbQuery.FieldInfo Field(int column) => mQuery.Field(column);

        public T GetValue<T>(int column) => mQuery.GetValue<T>(column);

        public T GetValue<T>(string column) => mQuery.GetValue<T>(column);

        public object GetValue(int column, Type type)
        {
            return mQuery.GetValue(column, type);
        }

        public object GetValue(string column, Type type)
        {
            return mQuery.GetValue(column, type);
        }

        public int FindField(string column)
        {
            return mQuery.FindField(column);
        }

        public bool IsNull(int column) => mQuery.IsNull(column);

        public bool IsNull(string column) => mQuery.IsNull(column);

        public bool ReadNext() => mQuery.ReadNext();

        public Task<bool> ReadNextAsync() => ReadNextAsync(null);

        public Task<bool> ReadNextAsync(CancellationToken? token) => mQuery.ReadNextAsync(token);

        ~EntityQuery()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            mQuery?.Dispose();
            mQuery = null;
        }

        private Dictionary<Type, object> mTags;

        public T GetTag<T>()
        {
            if (mTags == null)
                return default;
            if (!mTags.TryGetValue(typeof(T), out object value))
                return default;
            return (T)value;
        }

        public void SetTag<T>(T value)
        {
            if (mTags == null)
                mTags = new Dictionary<Type, object>();
            mTags[typeof(T)] = value;
        }
    }
}