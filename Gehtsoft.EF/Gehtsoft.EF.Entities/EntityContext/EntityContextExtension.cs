using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The extension to the context class
    /// </summary>
    public static class EntityContextExtension
    {
        /// <summary>
        /// Gets the drop query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IEntityQuery DropEntity<T>(this IEntityContext context) => context.DropEntity(typeof(T));

        /// <summary>
        /// Gets the create table query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IEntityQuery CreateEntity<T>(this IEntityContext context) => context.CreateEntity(typeof(T));

        /// <summary>
        /// Gets the insert entity query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="createKey"></param>
        /// <returns></returns>
        public static IModifyEntityQuery InsertEntity<T>(this IEntityContext context, bool createKey = true) => context.InsertEntity(typeof(T), createKey);

        /// <summary>
        /// Gets the update entity query by the entity type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IModifyEntityQuery UpdateEntity<T>(this IEntityContext context) => context.UpdateEntity(typeof(T));

        /// <summary>
        /// Gets the delete one entity query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IModifyEntityQuery DeleteEntity<T>(this IEntityContext context) => context.DeleteEntity(typeof(T));

        /// <summary>
        /// Gets the delete multiple entities query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IContextQueryWithCondition DeleteMultiple<T>(this IEntityContext context) => context.DeleteMultiple(typeof(T));

        /// <summary>
        /// Gets the select entities query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IContextSelect Select<T>(this IEntityContext context) => context.Select(typeof(T));

        /// <summary>
        /// Gets the count entity query.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <returns></returns>
        public static IContextCount Count<T>(this IEntityContext context) => context.Count(typeof(T));

        /// <summary>
        /// Gets one entity by its primary key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static T Get<T>(this IEntityContext context, object key)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                query.Execute();
                return query.ReadOne<T>();
            }
        }

        /// <summary>
        /// Gets one entity by its primary key asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<T> GetAsync<T>(this IEntityContext context, object key, CancellationToken? token = null)
        {
            using (var query = context.Select(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                await query.ExecuteAsync(token);
                return await query.ReadOneAsync<T>();
            }
        }

        /// <summary>
        /// Checks whether the entity exists by its primary key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool Exists<T>(this IEntityContext context, object key)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return query.GetCount() > 0;
            }
        }

        /// <summary>
        /// Checks whether the entity exists by its primary key asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="key"></param>
        /// <param name="_"></param>
        /// <returns></returns>
        public static async Task<bool> ExistsAsync<T>(this IEntityContext context, object key, CancellationToken? _ = null)
        {
            using (var query = context.Count(typeof(T)))
            {
                query.Where.Property(typeof(T).GetEfPrimaryKey().Name).Eq(key);
                return (await query.GetCountAsync()) > 0;
            }
        }

        /// <summary>
        /// Saves the entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entity"></param>
        /// <param name="createKey"></param>
        public static void Save<T>(this IEntityContext context, T entity, bool createKey = true)
        {
            bool exists = context.Exists<T>(entity.GetEfEntityId());
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>(createKey))
                query.Execute(entity);
        }

        /// <summary>
        /// Saves the entity asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="context"></param>
        /// <param name="entity"></param>
        /// <param name="createKey"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task SaveAsync<T>(this IEntityContext context, T entity, bool createKey = true, CancellationToken? token = null)
        {
            bool exists = await context.ExistsAsync<T>(entity.GetEfEntityId(), token);
            using (var query = exists ? context.UpdateEntity<T>() : context.InsertEntity<T>(createKey))
                await query.ExecuteAsync(entity, token);
        }
    }
}