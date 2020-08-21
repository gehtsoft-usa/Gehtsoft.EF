namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public abstract class AQueryBuilder
    {
        protected SqlDbLanguageSpecifics mSpecifics;

        protected AQueryBuilder(SqlDbLanguageSpecifics specifics)
        {
            mSpecifics = specifics;
        }

        public abstract void PrepareQuery();

        public abstract string Query { get; }
    }

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
