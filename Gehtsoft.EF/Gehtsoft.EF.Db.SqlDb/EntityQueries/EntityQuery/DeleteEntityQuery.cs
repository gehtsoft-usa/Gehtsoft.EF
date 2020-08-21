namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class DeleteEntityQuery : ModifyEntityQuery
    {
        internal DeleteEntityQuery(SqlDbQuery query, DeleteEntityQueryBuilder builder) : base(query, builder)
        {
            builder.PrepareBinder();
            mBinder = builder.Binder;
        }

        public override bool IsInsert => false;
    }
}