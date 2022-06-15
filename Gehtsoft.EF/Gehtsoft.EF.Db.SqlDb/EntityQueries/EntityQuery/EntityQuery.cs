using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base query for all entity queries
    ///
    /// This class is abstract class. Use <see cref="InsertEntityQuery"/>, <see cref="MultiUpdateEntityQuery"/>,
    /// <see cref="UpdateEntityQuery"/>, <see cref="DeleteEntityQuery"/>,
    /// <see cref="MultiDeleteEntityQuery"/>, <see cref="SelectEntitiesCountQuery"/>,
    /// <see cref="SelectEntitiesQueryBase"/> or
    /// <see cref="SelectEntitiesQuery"/>.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class EntityQuery : IDbQuery
    {
        protected SqlDbQuery mQuery;
        internal EntityQueryBuilder mBuilder;

        /// <summary>
        /// Underlying SQL query.
        /// </summary>
        public SqlDbQuery Query => mQuery;

        internal EntityQueryBuilder EntityQueryBuilder => mBuilder;

        /// <summary>
        /// Underlying SQL query builder.
        /// </summary>
        public AQueryBuilder Builder => mBuilder.QueryBuilder;

        /// <summary>
        /// The flag indicating whether the query is an insert query.
        /// </summary>
        public virtual bool IsInsert => false;

        /// <summary>
        /// The flag indicating whether the query can read an entity.
        /// </summary>
        public bool CanRead
        {
            get { return mQuery.CanRead; }
        }

        /// <summary>
        /// The language specific of the associated connection.
        /// </summary>
        public SqlDbLanguageSpecifics LanguageSpecifics
        {
            get { return mQuery.LanguageSpecifics; }
        }

        internal EntityQuery(SqlDbQuery query, EntityQueryBuilder builder)
        {
            mQuery = query;
            mBuilder = builder;
        }

        /// <summary>
        /// Prepares query to be used.
        ///
        /// The method is called automatically when query is executed or reused as a sub-query,
        /// however call it if you want to get the underlying RAW SQL query.
        /// </summary>
        public virtual void PrepareQuery()
        {
            if (string.IsNullOrEmpty(mQuery.CommandText))
            {
                if (mBuilder.QueryBuilder.Query == null)
                    mBuilder.QueryBuilder.PrepareQuery();
                mQuery.CommandText = mBuilder.QueryBuilder.Query;
            }
        }

        /// <summary>
        /// Execute query
        /// </summary>
        /// <returns></returns>
        public virtual int Execute()
        {
            PrepareQuery();
            return mQuery.ExecuteNoData();
        }

        /// <summary>
        /// Execute query asynchronously.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<int> ExecuteAsync(CancellationToken? token = null)
        {
            PrepareQuery();
            return await mQuery.ExecuteNoDataAsync(token);
        }

        /// <summary>
        /// Binds parameter of the specified type and direction to the query
        /// </summary>
        /// <param name="name"></param>
        /// <param name="direction"></param>
        /// <param name="value"></param>
        /// <param name="valueType"></param>
        public void BindParam(string name, ParameterDirection direction, object value, Type valueType)
        {
            mQuery.BindParam(name, direction, value, valueType);
        }

        /// <summary>
        /// Binds input parameter.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public virtual void BindParam<T>(string name, T value) => mQuery.BindParam<T>(name, value);

        /// <summary>
        /// Gets parameter value.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetParamValue(string name, Type type)
        {
            return mQuery.GetParamValue(name, type);
        }

        /// <summary>
        /// Gets parameter value (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <returns></returns>
        public T GetParamValue<T>(string name)
        {
            return mQuery.GetParamValue<T>(name);
        }

        /// <summary>
        /// Returns the number of the rows affected by a data change query (insert, update or delete).
        /// </summary>
        public int RowsAffected { get; protected set; } = 0;

        /// <summary>
        /// Execute update query.
        /// </summary>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        public int ExecuteNoData()
        {
            PrepareQuery();
            RowsAffected = mQuery.ExecuteNoData();
            return RowsAffected;
        }

        /// <summary>
        /// Execute select query.
        /// </summary>
        [ExcludeFromCodeCoverage]
        public void ExecuteReader()
        {
            PrepareQuery();
            mQuery.ExecuteReader();
        }

        /// <summary>
        /// Execute update query asynchronously
        /// <param name="token"/>
        /// </summary>
        [ExcludeFromCodeCoverage]
        public Task<int> ExecuteNoDataAsync(CancellationToken? token = null)
        {
            PrepareQuery();
            return mQuery.ExecuteNoDataAsync(token);
        }

        /// <summary>
        /// Execute select query asynchronously.
        /// <param name="token"/>
        /// </summary>
        /// <returns></returns>
        [ExcludeFromCodeCoverage]
        public Task ExecuteReaderAsync(CancellationToken? token = null)
        {
            PrepareQuery();
            return mQuery.ExecuteReaderAsync(token);
        }

        /// <summary>
        /// Binds input parameter with the `null` value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        [ExcludeFromCodeCoverage]
        public virtual void BindNull(string name, DbType type) => mQuery.BindNull(name, type);

        /// <summary>
        /// Binds output parameter of the specified type.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="type"></param>
        [ExcludeFromCodeCoverage]
        public void BindOutputParam(string name, DbType type)
        {
            mQuery.BindOutputParam(name, type);
        }

        /// <summary>
        /// Returns the number of the fields in the resultset.
        /// </summary>
        public int FieldCount => mQuery.FieldCount;

        /// <summary>
        /// Returns the field description by its name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public FieldInfo Field(string name) => mQuery.Field(name);

        /// <summary>
        /// Returns the field description by its index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public FieldInfo Field(int column) => mQuery.Field(column);

        /// <summary>
        /// Gets column value by the index (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        public T GetValue<T>(int column) => mQuery.GetValue<T>(column);

        /// <summary>
        /// Gets column value by the name (generic version).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="column"></param>
        /// <returns></returns>
        public T GetValue<T>(string column) => mQuery.GetValue<T>(column);

        /// <summary>
        /// Gets column value by the index.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetValue(int column, Type type)
        {
            return mQuery.GetValue(column, type);
        }

        /// <summary>
        /// Gets column value by the name
        /// </summary>
        /// <param name="column"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public object GetValue(string column, Type type)
        {
            return mQuery.GetValue(column, type);
        }

        /// <summary>
        /// Finds the field by its index.
        /// </summary>
        /// <param name="column"></param>
        /// <param name="ignoreCase"></param>
        /// <returns></returns>
        public int FindField(string column, bool ignoreCase = false)
        {
            return mQuery.FindField(column, ignoreCase);
        }

        /// <summary>
        /// Checks whether the column is null by column index.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public bool IsNull(int column) => mQuery.IsNull(column);

        /// <summary>
        /// Checks whether the column is null by the column name.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        public bool IsNull(string column) => mQuery.IsNull(column);

        /// <summary>
        /// Reads the next row.
        /// </summary>
        /// <returns></returns>
        public bool ReadNext() => mQuery.ReadNext();

        /// <summary>
        /// Reads the next row asynchronously.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public Task<bool> ReadNextAsync(CancellationToken? token = null) => mQuery.ReadNextAsync(token);

        [ExcludeFromCodeCoverage]
        ~EntityQuery()
        {
            Dispose(false);
        }

        [DocgenIgnore]
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [DocgenIgnore]
        protected virtual void Dispose(bool disposing)
        {
            mQuery?.Dispose();
            mQuery = null;
        }

        private TagCollection mTags;

        public TagCollection Tags => mTags ?? (mTags = new TagCollection());

        /// <summary>
        /// Gets a tag (user-defined data) associated with the query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        [Obsolete("Use Tags property")]
        public T GetTag<T>() => Tags.GetTag<T>(typeof(T));

        /// <summary>
        /// Sets a tag (user-defined data) to the query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        [DocgenIgnore]
        [ExcludeFromCodeCoverage]
        [Obsolete("Use Tags property")]
        public void SetTag<T>(T value) => Tags.SetTag(typeof(T), value);
    }
}