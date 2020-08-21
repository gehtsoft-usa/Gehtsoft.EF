namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class InsertEntityQuery : ModifyEntityQuery
    {
        private InsertEntityQueryBuilder mInsertBuilder;


        internal InsertEntityQuery(SqlDbQuery query, InsertEntityQueryBuilder builder) : base(query, builder)
        {
            mInsertBuilder = builder;
            mBinder = builder.Binder;
        }

        public override bool IsInsert => !mInsertBuilder.IgnoreAutoIncrement;
    }
}