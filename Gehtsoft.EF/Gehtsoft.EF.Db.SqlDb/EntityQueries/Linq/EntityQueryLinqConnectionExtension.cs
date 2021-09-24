using System.Linq;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq
{
    /// <summary>
    /// Extensions to use LINQ queries on connection.
    /// </summary>
    public static class EntityQueryLinqConnectionExtension
    {
        /// <summary>
        /// Returns queryable collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static QueryableEntity<T> GetCollectionOf<T>(this SqlDbConnection connection)
        {
            var r = connection.Tags.GetTag<QueryableEntity<T>>(typeof(QueryableEntity<T>));
            if (r == null)
            {
                var p = connection.Tags.GetTag<QueryableEntityProvider>(typeof(QueryableEntityProvider));
                if (p == null)
                {
                    p = new QueryableEntityProvider(new ExistingConnectionFactory(connection));
                    connection.Tags.SetTag(typeof(QueryableEntityProvider), p);
                }

                r = new QueryableEntity<T>(p);
                connection.Tags.SetTag(typeof(QueryableEntity<T>), r);
            }
            return r;
        }
    }
}
