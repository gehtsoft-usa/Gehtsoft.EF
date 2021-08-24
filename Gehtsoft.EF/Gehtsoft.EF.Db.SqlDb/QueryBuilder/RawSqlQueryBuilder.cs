namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class RawSqlQueryBuilder : AQueryBuilder
    {
        protected RawSqlQueryBuilder(SqlDbLanguageSpecifics specifics, string query) : base(specifics)
        {
            mQuery = query;
        }

        public override void PrepareQuery()
        {
        }

        protected string mQuery;

        public override string Query => mQuery;
    }
}
