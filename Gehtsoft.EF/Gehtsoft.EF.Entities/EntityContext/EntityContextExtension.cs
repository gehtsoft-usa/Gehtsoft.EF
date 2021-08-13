using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    public static class EntityContextExtension
    {
        public static IEntityQuery DropEntity<T>(this IEntityContext context) => context.DropEntity(typeof(T));

        public static IEntityQuery CreateEntity<T>(this IEntityContext context) => context.CreateEntity(typeof(T));

        public static IModifyEntityQuery InsertEntity<T>(this IEntityContext context, bool createKey = true) => context.InsertEntity(typeof(T), createKey);

        public static IModifyEntityQuery UpdateEntity<T>(this IEntityContext context) => context.UpdateEntity(typeof(T));

        public static IModifyEntityQuery DeleteEntity<T>(this IEntityContext context) => context.DeleteEntity(typeof(T));

        public static IContextQueryWithCondition DeleteMultiple<T>(this IEntityContext context) => context.DeleteMultiple(typeof(T));

        public static IContextSelect Select<T>(this IEntityContext context) => context.Select(typeof(T));

        public static IContextCount Count<T>(this IEntityContext context) => context.Count(typeof(T));

        public static T ReadOne<T>(this IContextSelect query) => (T)query.ReadOne();

        public static async Task<T> ReadOneAsync<T>(this IContextSelect query) => (T)(await query.ReadOneAsync());

        public static EntityCollection<T> ReadAll<T>(this IContextSelect query)
            where T : class
            => ReadAll<EntityCollection<T>, T>(query);

        public static TC ReadAll<TC, T>(this IContextSelect query)
            where TC : ICollection<T>, new()
            where T : class
        {
            TC r = new TC();
            T t;
            while (true)
            {
                t = (T)query.ReadOne();
                if (t == null)
                    break;
                r.Add(t);
            }
            return r;
        }

        public static Task<EntityCollection<T>> ReadAllAsync<T>(this IContextSelect query)
            where T : class
            => ReadAllAsync<EntityCollection<T>, T>(query);

        public static async Task<TC> ReadAllAsync<TC, T>(this IContextSelect query, CancellationToken? token = null)
            where TC : ICollection<T>, new()
            where T : class
        {
            TC r = new TC();
            T t;
            while (true)
            {
                t = (T)(await query.ReadOneAsync(token));
                if (t == null)
                    break;
                r.Add(t);
            }
            return r;
        }

        public static T Get<T>(this IEntityContext context, object key)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                query.Execute();
                return query.ReadOne<T>();
            }
        }

        public static async Task<T> GetAsync<T>(this IEntityContext context, object key, CancellationToken? token = null)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                await query.ExecuteAsync(token);
                return await query.ReadOneAsync<T>();
            }
        }

        public static bool Exists<T>(this IEntityContext context, object key)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return query.GetCount() > 0;
            }
        }

        public static async Task<bool> ExistsAsync<T>(this IEntityContext context, object key, CancellationToken? _ = null)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return (await query.GetCountAsync()) > 0;
            }
        }

        public static void Save<T>(this IEntityContext context, T entity, bool createKey = true)
        {
            bool exists = context.Exists<T>(entity.GetEfEntityId());
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>(createKey))
                query.Execute(entity);
        }

        public static async Task SaveAsync<T>(this IEntityContext context, T entity, bool createKey = true, CancellationToken? token = null)
        {
            bool exists = await context.ExistsAsync<T>(entity.GetEfEntityId(), token);
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>(createKey))
                await query.ExecuteAsync(entity, token);
        }
    }
}