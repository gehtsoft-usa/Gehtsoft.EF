namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class MultiUpdateEntityQuery : ConditionEntityQueryBase
    {
        internal readonly UpdateEntityQueryBuilder mUpdateBuilder;

        internal MultiUpdateEntityQuery(SqlDbQuery query, UpdateEntityQueryBuilder builder) : base(query, builder)
        {
            mUpdateBuilder = builder;
        }

        public void AddUpdateColumn<T>(string propertyName, T value)
        {
            mUpdateBuilder.AddUpdateColumn(propertyName);
            mQuery.BindParam(propertyName, value);
        }
    }
}