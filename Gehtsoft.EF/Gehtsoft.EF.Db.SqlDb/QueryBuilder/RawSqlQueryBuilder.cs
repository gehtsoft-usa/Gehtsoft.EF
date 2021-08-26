namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder which can be set to a raw SQL query.
    /// </summary>
    public class RawSqlQueryBuilder : AQueryBuilder
    {
        protected RawSqlQueryBuilder(SqlDbLanguageSpecifics specifics, string query) : base(specifics)
        {
            mQuery = query;
        }

        /// <summary>
        /// Prepares the query.
        /// </summary>
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
