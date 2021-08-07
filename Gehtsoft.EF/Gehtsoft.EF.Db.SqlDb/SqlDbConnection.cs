using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Db.SqlDb
{
    public delegate SqlDbConnection SqlDbConnectionFactory(string connectionString);

    public delegate Task<SqlDbConnection> SqlDbConnectionFactoryAsync(string connectionString, CancellationToken? token);

    public abstract partial class SqlDbConnection : IEntityContext
    {
        protected DbConnection mConnection;

        public DbConnection Connection => mConnection;

        protected MutexSlim SyncRoot { get; private set; } = new MutexSlim();

        public IDisposable Lock() => SyncRoot.Lock();

        public Task<IDisposable> LockAsync() => SyncRoot.LockAsync();

        public abstract string ConnectionType { get; }

        public IResiliencyPolicy ResiliencyPolicy { get; }

        protected SqlDbConnection(DbConnection connection)
        {
            mConnection = connection;
            ResiliencyPolicy = ResiliencyPolicyDictionary.Instance.GetPolicy(connection.ConnectionString);
        }

        ~SqlDbConnection()
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
            if (mConnection != null)
            {
                mConnection.Dispose();
                mConnection = null;
            }
            if (SyncRoot != null)
            {
                SyncRoot.Dispose();
                SyncRoot = null;
            }
        }

        public virtual SqlDbTransaction BeginTransaction()
        {
            return new SqlDbTransaction(mConnection.BeginTransaction());
        }

        public virtual SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            return new SqlDbTransaction(mConnection.BeginTransaction(level));
        }

        public virtual SqlDbQuery GetQuery() => ConstructQuery();

        public virtual SqlDbQuery GetQuery(string queryText)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (queryText.ContainsScalar(true))
                    throw new ArgumentException("The query cannot contains string scalars", nameof(queryText));
            return ConstructQuery(queryText);
        }

        public virtual SqlDbQuery GetQuery(QueryBuilder.AQueryBuilder builder)
        {
            if (builder.Query == null)
                builder.PrepareQuery();
            return ConstructQuery(builder.Query);
        }

        protected virtual SqlDbQuery ConstructQuery()
        {
            return new SqlDbQuery(this, mConnection.CreateCommand(), GetLanguageSpecifics());
        }

        protected virtual SqlDbQuery ConstructQuery(string queryText)
        {
            var query = ConstructQuery();
            query.CommandText = queryText;
            return query;
        }

        public abstract SqlDbLanguageSpecifics GetLanguageSpecifics();

        public virtual CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new CreateTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        public virtual CreateIndexBuilder GetCreateIndexBuilder(TableDescriptor descriptor, CompositeIndex index)
        {
            return new CreateIndexBuilder(GetLanguageSpecifics(), descriptor, index);
        }

        public virtual DropTableBuilder GetDropTableBuilder(TableDescriptor descriptor)
        {
            return new DropTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        public InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor) => GetInsertQueryBuilder(descriptor, false);

        public virtual InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement)
        {
            return new InsertQueryBuilder(GetLanguageSpecifics(), descriptor, ignoreAutoIncrement);
        }

        public InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery) => GetInsertSelectQueryBuilder(descriptor, selectQuery, false);

        public virtual InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement)
        {
            return new InsertSelectQueryBuilder(GetLanguageSpecifics(), descriptor, selectQuery, ignoreAutoIncrement);
        }

        public virtual UpdateQueryBuilder GetUpdateQueryBuilder(TableDescriptor descriptor)
        {
            return new UpdateQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        public virtual DeleteQueryBuilder GetDeleteQueryBuilder(TableDescriptor descriptor)
        {
            return new DeleteQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        public abstract HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null);

        public virtual SelectQueryBuilder GetSelectQueryBuilder(TableDescriptor descriptor)
        {
            return new SelectQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        public virtual ParameterGroupQueryBuilder GetParameterGroupBuilder()
        {
            return new ParameterGroupQueryBuilder(GetLanguageSpecifics());
        }

        public virtual TableDescriptor[] Schema() => SchemaCore(true, null).ConfigureAwait(false).GetAwaiter().GetResult();

        public virtual Task<TableDescriptor[]> SchemaAsync(CancellationToken? token = null) => SchemaCore(false, token);

        protected abstract Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token);

        public virtual AlterTableQueryBuilder GetAlterTableQueryBuilder()
        {
            throw new EfSqlException(EfExceptionCode.FeatureNotSupported);
        }

        public virtual CreateViewBuilder GetCreateViewBuilder(string name, SelectQueryBuilder selectBuilder)
        {
            return new CreateViewBuilder(GetLanguageSpecifics(), name, selectBuilder);
        }

        public virtual DropViewBuilder GetDropViewBuilder(string name)
        {
            return new DropViewBuilder(GetLanguageSpecifics(), name);
        }

        public virtual DropIndexBuilder GetDropIndexBuilder(TableDescriptor descriptor, string name)
        {
            return new DropIndexBuilder(GetLanguageSpecifics(), descriptor.Name, name);
        }

        private Dictionary<object, object> mTags = null;

        public object GetTag(object key)
        {
            if (mTags == null)
                return null;
            if (!mTags.TryGetValue(key, out object value))
                return null;
            return value;
        }

        public T GetTag<T>(object key, T defaultValue = default)
        {
            if (mTags == null)
                return defaultValue;
            if (!mTags.TryGetValue(key, out object value))
                return defaultValue;
            if (value is T t)
                return t;
            return defaultValue;
        }

        public void SetTag(object key, object value)
        {
            if (mTags == null)
                mTags = new Dictionary<object, object>();
            if (value == null)
                mTags.Remove(key);
            else
                mTags.Add(key, value);
        }

        class ExistingTable : IEntityTable
        {
            public string Name { get; set; }
            public Type EntityType { get; set; }
        }

        public IEntityTable[] ExistingTables()
        {
            var tables = Schema();
            var r = new ExistingTable[tables.Length];
            var entities = AllEntities.Inst.All();
            for (int i = 0; i < tables.Length; i++)
            {
                r[i] = new ExistingTable()
                {
                    Name = tables[i].Name,
                    EntityType = entities.FirstOrDefault(e => e.TableDescriptor.Name.Equals(tables[i].Name, StringComparison.OrdinalIgnoreCase))?.EntityType
                };
            }
            return r;
        }

    }
}