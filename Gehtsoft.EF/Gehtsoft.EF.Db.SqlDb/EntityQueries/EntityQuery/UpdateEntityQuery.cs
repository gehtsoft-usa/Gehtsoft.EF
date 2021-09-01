namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query to update an entity.
    ///
    /// Use <see cref="EntityConnectionExtension.GetUpdateEntityQuery(SqlDbConnection, System.Type)"/> to
    /// get an instance of this object.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class UpdateEntityQuery : ModifyEntityQuery
    {
        internal UpdateEntityQuery(SqlDbQuery query, UpdateEntityQueryBuilder builder) : base(query, builder)
        {
            builder.PrepareBinder();
            mBinder = builder.Binder;
        }
    }
}