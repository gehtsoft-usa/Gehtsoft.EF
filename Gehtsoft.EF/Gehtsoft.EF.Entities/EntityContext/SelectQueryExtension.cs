using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Entities.Context
{
    /// <summary>
    /// The extensions for <see cref="IContextSelect"/> query.
    /// </summary>
    public static class SelectQueryExtension
    {
        /// <summary>
        /// Reads one entity.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static T ReadOne<T>(this IContextSelect query) => (T)query.ReadOne();

        /// <summary>
        /// Reads one entity asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static async Task<T> ReadOneAsync<T>(this IContextSelect query) => (T)(await query.ReadOneAsync());

        /// <summary>
        /// Reads all entities into the entity collection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static EntityCollection<T> ReadAll<T>(this IContextSelect query)
            where T : class
            => ReadAll<EntityCollection<T>, T>(query);

        /// <summary>
        /// Reads all entities into the collection of the specified type.
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Read all entities into the entity collection asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <returns></returns>
        public static Task<EntityCollection<T>> ReadAllAsync<T>(this IContextSelect query)
            where T : class
            => ReadAllAsync<EntityCollection<T>, T>(query);

        /// <summary>
        /// Read all entities into the collection of the specified type asynchronously.
        /// </summary>
        /// <typeparam name="TC"></typeparam>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="token"></param>
        /// <returns></returns>
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
    }
}