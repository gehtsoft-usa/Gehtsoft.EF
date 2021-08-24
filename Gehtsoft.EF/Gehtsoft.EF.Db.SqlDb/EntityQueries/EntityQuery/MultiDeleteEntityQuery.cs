namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class MultiDeleteEntityQuery : ConditionEntityQueryBase
    {
        internal MultiDeleteEntityQuery(SqlDbQuery query, DeleteEntityQueryBuilder builder) : base(query, builder)
        {
        }
    }
}