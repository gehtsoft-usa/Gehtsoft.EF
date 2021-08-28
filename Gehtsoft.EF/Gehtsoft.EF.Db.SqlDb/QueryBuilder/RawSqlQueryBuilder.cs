using System.Runtime.CompilerServices;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// Connection class extension for the raw query builder.
    /// </summary>
    public static class RawSqlQueryBuilderConnectionExtension
    {
        public static RawSqlQueryBuilder GetRawSqlQueryBuilder(this SqlDbConnection connection, string query)
            => new RawSqlQueryBuilder(connection.GetLanguageSpecifics(), query);
    }

    /// <summary>
    /// The query builder which can be set to a raw SQL query.
    /// </summary>
    public class RawSqlQueryBuilder : AQueryBuilder
    {
        [DocgenIgnore]
        internal protected RawSqlQueryBuilder(SqlDbLanguageSpecifics specifics, string query) : base(specifics)
        {
            mQuery = query;
        }

        /// <summary>
        /// Prepares the query.
        /// </summary>
        [DocgenIgnore]
        public override void PrepareQuery()
        {
        }

        protected string mQuery;

        /// <summary>
        /// Returns the query in SQL.
        ///
        /// You must call <see cref="PrepareQuery"/> before getting the query.
        /// </summary>
        public override string Query => mQuery;
    }
}
