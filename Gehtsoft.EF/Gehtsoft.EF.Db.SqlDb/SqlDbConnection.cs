using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using Gehtsoft.EF.Utils;
using Gehtsoft.Tools.TypeUtils;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The delegate to the function that creates a connection.
    /// </summary>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    public delegate SqlDbConnection SqlDbConnectionFactory(string connectionString);

    /// <summary>
    /// The delegate to the function that creates a connection asynchronously.
    /// </summary>
    /// <param name="connectionString"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public delegate Task<SqlDbConnection> SqlDbConnectionFactoryAsync(string connectionString, CancellationToken? token);

    /// <summary>
    /// The connection to the SQL database.
    /// </summary>
    public abstract partial class SqlDbConnection : IEntityContext
    {
        protected DbConnection mConnection;

        /// <summary>
        /// Gets underlying ADO.NET connection.
        /// </summary>
        public DbConnection Connection => mConnection;

        protected MutexSlim SyncRoot { get; private set; } = new MutexSlim();

        /// <summary>
        /// Locks the connection object.
        /// </summary>
        /// <returns></returns>
        public IDisposable Lock() => SyncRoot.Lock();

        /// <summary>
        /// Locks the connection object asynchronously.
        /// </summary>
        /// <returns></returns>
        public Task<IDisposable> LockAsync() => SyncRoot.LockAsync();

        /// <summary>
        /// Returns the name of the associated driver.
        ///
        /// Check <see cref="UniversalSqlDbFactory"/> for supported drivers.
        /// </summary>
        public abstract string ConnectionType { get; }

        /// <summary>
        /// The resiliency policy associated with the connection.
        ///
        /// The resiliency policy will be used for all executions.
        ///
        /// Use <see cref="ResiliencyPolicyDictionary"/> to set the resiliency policy.
        /// </summary>
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

        /// <summary>
        /// Disposes the object.
        /// </summary>
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

        /// <summary>
        /// Begins the transaction.
        /// </summary>
        /// <returns></returns>
        public virtual SqlDbTransaction BeginTransaction()
        {
            return new SqlDbTransaction(mConnection.BeginTransaction());
        }

        /// <summary>
        /// Begins the transaction of the specified isolation level.
        /// </summary>
        /// <param name="level"></param>
        /// <returns></returns>
        public virtual SqlDbTransaction BeginTransaction(IsolationLevel level)
        {
            return new SqlDbTransaction(mConnection.BeginTransaction(level));
        }

        /// <summary>
        /// Creates a query object.
        /// </summary>
        /// <returns></returns>
        public SqlDbQuery GetQuery() => ConstructQuery();

        /// <summary>
        /// Creates a query object with the specified command text.
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        public SqlDbQuery GetQuery(string queryText) => GetQuery(queryText, false);

        /// <summary>
        /// Creates a query suppressing the SQL injection protection.
        /// </summary>
        /// <param name="queryText"></param>
        /// <param name="suppressScalarProtection"></param>
        /// <returns></returns>
        public SqlDbQuery GetQuery(string queryText, bool suppressScalarProtection)
        {
            if (!suppressScalarProtection)
                CheckForScalars(queryText);

            return ConstructQuery(queryText);
        }

        protected internal void CheckForScalars(string queryText)
        {
            if (SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries)
                if (queryText.ContainsScalar(true))
                    throw new ArgumentException("The query cannot contains string scalars", nameof(queryText));
        }

        /// <summary>
        /// Creates a query object with the specified command text defined by a query builder.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public SqlDbQuery GetQuery(QueryBuilder.AQueryBuilder builder)
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

        /// <summary>
        /// Returns the object with dialect rules specific for the curent driver.
        /// </summary>
        /// <returns></returns>
        public abstract SqlDbLanguageSpecifics GetLanguageSpecifics();

        /// <summary>
        /// Returns a query builder to create a table.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public virtual CreateTableBuilder GetCreateTableBuilder(TableDescriptor descriptor)
        {
            return new CreateTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        /// <summary>
        /// Returns a query builder to create an index.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual CreateIndexBuilder GetCreateIndexBuilder(TableDescriptor descriptor, CompositeIndex index)
        {
            return new CreateIndexBuilder(GetLanguageSpecifics(), descriptor, index);
        }

        /// <summary>
        /// Returns a query builder to drop a table.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public virtual DropTableBuilder GetDropTableBuilder(TableDescriptor descriptor)
        {
            return new DropTableBuilder(GetLanguageSpecifics(), descriptor);
        }

        /// <summary>
        /// Returns a query builder to insert a row.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <returns></returns>
        public virtual InsertQueryBuilder GetInsertQueryBuilder(TableDescriptor descriptor, bool ignoreAutoIncrement = false)
        {
            return new InsertQueryBuilder(GetLanguageSpecifics(), descriptor, ignoreAutoIncrement);
        }

        /// <summary>
        /// Returns a query builder to insert a select resultset into the table.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="selectQuery"></param>
        /// <param name="ignoreAutoIncrement"></param>
        /// <returns></returns>
        public virtual InsertSelectQueryBuilder GetInsertSelectQueryBuilder(TableDescriptor descriptor, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement = false)
        {
            return new InsertSelectQueryBuilder(GetLanguageSpecifics(), descriptor, selectQuery, ignoreAutoIncrement);
        }

        /// <summary>
        /// Returns a query builder to update the table.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public virtual UpdateQueryBuilder GetUpdateQueryBuilder(TableDescriptor descriptor)
        {
            return new UpdateQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        /// <summary>
        /// Returns a query builder to delete rows from the table.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public virtual DeleteQueryBuilder GetDeleteQueryBuilder(TableDescriptor descriptor)
        {
            return new DeleteQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        /// <summary>
        /// Returns a query builder for hierarchical (self-connected) table select.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="parentReferenceColumn"></param>
        /// <param name="rootParameter"></param>
        /// <returns></returns>
        public abstract HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null);

        /// <summary>
        /// Returns a query builder for an ordinary select.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public virtual SelectQueryBuilder GetSelectQueryBuilder(TableDescriptor descriptor)
        {
            return new SelectQueryBuilder(GetLanguageSpecifics(), descriptor);
        }

        /// <summary>
        /// Returns the schema (list of tables).
        /// </summary>
        /// <returns></returns>
        public virtual TableDescriptor[] Schema() => SchemaCore(true, null).ConfigureAwait(false).GetAwaiter().GetResult();

        /// <summary>
        /// Checks whether the object exists.
        /// </summary>
        /// <param name="tableName">The name of the object (table or view).</param>
        /// <param name="objectName">The optional name of the sub-object, e.g. index</param>
        /// <param name="objectType">The type of the object. The type could be `"table"`, `"index"` or `"view"`.</param>
        /// <returns></returns>
        public bool DoesObjectExist(string tableName, string objectName, string objectType)
        {
            CheckForScalars(tableName);
            if (!string.IsNullOrEmpty(objectType))
                CheckForScalars(objectName);

            return DoesObjectExistCore(tableName, objectName, objectType, false).Result;
        }

        /// <summary>
        /// Checks whether the object exists.
        /// </summary>
        /// <param name="tableName">The name of the object (table or view).</param>
        /// <param name="objectName">The optional name of the sub-object, e.g. index</param>
        /// <param name="objectType">The type of the object. The type could be `"table"`, `"index"` or `"view"`.</param>
        /// <returns></returns>
        public Task<bool> DoesObjectExistAsync(string tableName, string objectName, string objectType)
        {
            CheckForScalars(tableName);
            if (!string.IsNullOrEmpty(objectType))
                CheckForScalars(objectName);

            return DoesObjectExistCore(tableName, objectName, objectType, true).AsTask();
        }

        protected abstract ValueTask<bool> DoesObjectExistCore(string tableName, string objectName, string objectType, bool executeAsync);

        /// <summary>
        /// Returns the schema asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<TableDescriptor[]> SchemaAsync(CancellationToken? token = null) => SchemaCore(false, token);

        protected abstract Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token);

        /// <summary>
        /// Gets query builder to modify the table.
        /// </summary>
        /// <returns></returns>
        public abstract AlterTableQueryBuilder GetAlterTableQueryBuilder();

        /// <summary>
        /// Gets builder to create a view.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="selectBuilder"></param>
        /// <returns></returns>
        public virtual CreateViewBuilder GetCreateViewBuilder(string name, SelectQueryBuilder selectBuilder)
        {
            return new CreateViewBuilder(GetLanguageSpecifics(), name, selectBuilder);
        }

        /// <summary>
        /// Gets query builder to drop a view.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual DropViewBuilder GetDropViewBuilder(string name)
        {
            return new DropViewBuilder(GetLanguageSpecifics(), name);
        }

        /// <summary>
        /// Gets query builder to drop an index.
        /// </summary>
        /// <param name="descriptor"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public virtual DropIndexBuilder GetDropIndexBuilder(TableDescriptor descriptor, string name)
        {
            return new DropIndexBuilder(GetLanguageSpecifics(), descriptor.Name, name);
        }

        private TagCollection mTags = null;
        public TagCollection Tags => mTags ?? (mTags = new TagCollection());

        /// <summary>
        /// Gets a tag to the connection.
        ///
        /// A tag is any user-defined value. You can use tags to keep connection-related data.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [DocgenIgnore]
        [Obsolete("Use Tags property instead")]
        public object GetTag(object key) => mTags.GetTag(key, typeof(object));

        /// <summary>
        /// Gets a tag to the connection (generic method).
        ///
        /// A tag is any user-defined value. You can use tags to keep connection-related data.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        [DocgenIgnore]
        [Obsolete("Use Tags property instead")]
        public T GetTag<T>(object key, T defaultValue = default) => mTags.GetTag<T>(key, defaultValue);

        /// <summary>
        /// Sets a tag to the connection.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        [DocgenIgnore]
        [Obsolete("Use Tags property instead")]
        public void SetTag(object key, object value) => mTags.SetTag(key, value);

        internal class ExistingTable : IEntityTable
        {
            public string Name { get; set; }
            public Type EntityType { get; set; }
        }

        IEntityTable[] IEntityContext.ExistingTables()
        {
            var tables = Schema();
            var r = new ExistingTable[tables.Length];
            var entities = AllEntities.Inst.All();
            for (int i = 0; i < tables.Length; i++)
            {
                r[i] = new ExistingTable()
                {
                    Name = tables[i].Name,
                    EntityType = Array.Find(entities, e => e.TableDescriptor.Name.Equals(tables[i].Name, StringComparison.OrdinalIgnoreCase))?.EntityType
                };
            }
            return r;
        }

        /// <summary>
        /// Gets query builder for union query.
        /// </summary>
        /// <param name="firstQuery">The first query in union</param>
        /// <returns></returns>
        public virtual UnionQueryBuilder GetUnionQueryBuilder(SelectQueryBuilder firstQuery)
        {
            var u = new UnionQueryBuilder(GetLanguageSpecifics());
            u.AddQuery(firstQuery, false);
            return u;
        }
    }
}