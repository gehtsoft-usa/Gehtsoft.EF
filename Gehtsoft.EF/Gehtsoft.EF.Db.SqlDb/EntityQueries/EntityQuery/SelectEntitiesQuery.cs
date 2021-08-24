using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
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

        public T ReadOne<T>() where T : class
        {
            return ReadOne() as T;
        }

        public async Task<T> ReadOneAsync<T>(CancellationToken? token = null) where T : class
        {
            return await ReadOneAsync(token) as T;
        }

        public delegate void OnRow<in T>(T row, SelectEntitiesQuery query);

        public virtual EntityCollection<T> ReadAll<T>() where T : class => ReadAll<EntityCollection<T>, T>();

        public virtual Task<EntityCollection<T>> ReadAllAsync<T>(CancellationToken? token = null) where T : class => ReadAllAsync<EntityCollection<T>, T>(null, token);

        public virtual TC ReadAll<TC, T>(OnRow<T> onrow = null) where TC : IList<T>, new()
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

        public virtual async Task<TC> ReadAllAsync<TC, T>(OnRow<T> onrow = null, CancellationToken? token = null) where TC : IList<T>, new()
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

        protected override bool IgnoreOnDynamic(int index, SqlDbQuery.FieldInfo field) => mSelectBuilder1.Binder.BindsColumn(index, mQuery);

        protected override bool BindOneDynamic(ExpandoObject dynObj)
        {
            if (!mSelectBuilder1.Binder.BindToDynamic(mQuery, dynObj))
                return false;
            return base.BindOneDynamic(dynObj);
        }
    }
}