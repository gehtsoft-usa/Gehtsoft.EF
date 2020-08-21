namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class UpdateEntityQuery : ModifyEntityQuery
    {
        internal UpdateEntityQuery(SqlDbQuery query, UpdateEntityQueryBuilder builder) : base(query, builder)
        {
            builder.PrepareBinder();
            mBinder = builder.Binder;
        }
    }
}