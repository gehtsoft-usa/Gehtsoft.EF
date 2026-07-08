using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// Extensions to use LINQ queries on connection.
    /// </summary>
    public static class EntityQueryLinqConnectionExtension
    {
        /// <summary>
        /// Returns queryable collection.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <param name="preloadDynamicProperties">
        /// When `true` (the default), a whole-entity query (one without a projection) loads and
        /// attaches each returned entity's dynamic property bag. A no-op for a type that does not own
        /// dynamic properties; ignored for projection (`Select`) queries. Pass `false` to opt out of
        /// the extra load (the returned collection is then a fresh, non-cached instance).
        /// </param>
        /// <returns></returns>
        public static QueryableEntity<T> GetCollectionOf<T>(this SqlDbConnection connection, bool preloadDynamicProperties = true)
        {
            if (!preloadDynamicProperties)
            {
                var provider = new QueryableEntityProvider(new ExistingConnectionFactory(connection));
                return new QueryableEntity<T>(provider);
            }

            var r = connection.Tags.GetTag<QueryableEntity<T>>(typeof(QueryableEntity<T>));
            if (r == null)
            {
                var p = connection.Tags.GetTag<QueryableEntityProvider>(typeof(QueryableEntityProvider));
                if (p == null)
                {
                    p = new QueryableEntityProvider(new ExistingConnectionFactory(connection)) { PreloadDynamicProperties = true };
                    connection.Tags.SetTag(typeof(QueryableEntityProvider), p);
                }

                r = new QueryableEntity<T>(p);
                connection.Tags.SetTag(typeof(QueryableEntity<T>), r);
            }
            return r;
        }
    }
}
