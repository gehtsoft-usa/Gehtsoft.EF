using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query to select the entity and all dependent entities (dictionaries).
    ///
    /// Use <see cref="EntityConnectionExtension.GetSelectEntitiesQuery(SqlDbConnection, Type, SelectEntityQueryFilter[])"/> to
    /// get an instance of this query.
    ///
    /// In cause you don't need complete tree/all columns, use <see cref="SelectEntitiesQueryBase"/> to minimize DB load
    /// and traffic between the application and database.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class SelectEntitiesQuery : SelectEntitiesQueryBase
    {
        internal SelectEntityQueryBuilder mSelectBuilder1;
        protected bool mIsEntityCallback;
        protected bool mIsPostReadCallback;
        private static readonly Type entityCallbackType = typeof(IEntitySerializationCallback);

        internal SelectEntitiesQuery(SqlDbQuery query, SelectEntityQueryBuilder builder) : base(query, builder)
        {
            mSelectBuilder1 = builder;
            mIsEntityCallback = entityCallbackType.IsAssignableFrom(builder.Descriptor.EntityType);
        }

        protected SelectEntitiesQuery(Type type, SqlDbConnection connection) : this(connection.GetQuery(), new SelectEntityQueryBuilder(type, connection))
        {
        }

        /// <summary>
        /// Reads one entity.
        /// </summary>
        /// <returns></returns>
        public object ReadOne()
        {
            if (!Executed)
                Execute();

            if (mQuery.ReadNext())
            {
                object rc = mSelectBuilder1.Binder.Read(mQuery);
                if (mIsEntityCallback && rc != null)
                {
                    IEntitySerializationCallback serializationCallback = (IEntitySerializationCallback)rc;
                    serializationCallback.AfterDeserealization(this);
                }

                return rc;
            }

            return null;
        }

        /// <summary>
        /// Reads one entity asynchronously
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<object> ReadOneAsync(CancellationToken? token = null)
        {
            if (!Executed)
                await ExecuteAsync(token);

            if (await mQuery.ReadNextAsync(token))
            {
                object rc = mSelectBuilder1.Binder.Read(mQuery);
                if (mIsEntityCallback && rc != null)
                {
                    IEntitySerializationCallback serializationCallback = (IEntitySerializationCallback)rc;
                    serializationCallback.AfterDeserealization(this);
                }
                return rc;
            }
            return null;
        }

        /// <summary>
        /// Reads one entity (generic version)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ReadOne<T>() where T : class
        {
            return ReadOne() as T;
        }

        /// <summary>
        /// Reads one entity asynchronously (generic version)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<T> ReadOneAsync<T>(CancellationToken? token = null) where T : class
        {
            return await ReadOneAsync(token) as T;
        }

        /// <summary>
        /// Reads all entities
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual EntityCollection<T> ReadAll<T>() where T : class => ReadAll<EntityCollection<T>, T>();

        /// <summary>
        /// Reads all entities asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual Task<EntityCollection<T>> ReadAllAsync<T>(CancellationToken? token = null) where T : class => ReadAllAsync<EntityCollection<T>, T>(null, token);

        /// <summary>
        /// Reads all entities into collection and call the specified action for each row
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="onrow"></param>
        /// <returns></returns>
        public virtual TC ReadAll<TC, T>(Action<T, SelectEntitiesQuery> onrow = null) where TC : IList<T>, new()
            where T : class
        {
            if (!Executed)
                Execute();
            TC coll = new TC();
            T t;

            while ((t = ReadOne<T>()) != null)
            {
                coll.Add(t);
                onrow?.Invoke(t, this);
            }

            return coll;
        }

        /// <summary>
        /// Reads all entities asynchronously into collection and call the specified action for each row
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="onrow"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task<TC> ReadAllAsync<TC, T>(Action<T, SelectEntitiesQuery> onrow = null, CancellationToken? token = null) where TC : IList<T>, new()
            where T : class
        {
            if (!Executed)
                await ExecuteAsync(token);
            TC coll = new TC();
            T t;

            while ((t = await ReadOneAsync<T>(token)) != null)
            {
                coll.Add(t);
                onrow?.Invoke(t, this);
            }

            return coll;
        }

        protected internal object GetAllAsEnumerable(Type type)
        {
            if (!Executed)
                Execute();

            object collection = Activator.CreateInstance(typeof(EntityCollection<>).MakeGenericType(type));
            MethodInfo add = collection.GetType().GetTypeInfo().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            object obj;
            object[] args = new object[1];

            while ((obj = ReadOne()) != null)
            {
                args[0] = obj;
                add.Invoke(collection, args);
            }
            return collection;
        }

        protected override bool IgnoreOnDynamic(int index, FieldInfo field) => mSelectBuilder1.Binder.Rules.Any(r => r.ColumnIndex == index);

        protected override bool BindOneDynamic(ExpandoObject dynObj)
        {
            if (!mSelectBuilder1.Binder.Read(mQuery, dynObj))
                return false;
            return base.BindOneDynamic(dynObj);
        }
    }
}