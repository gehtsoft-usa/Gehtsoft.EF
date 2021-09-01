using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The class to delete an entity.
    ///
    /// Use <see cref="EntityConnectionExtension.GetDeleteEntityQuery(SqlDbConnection, System.Type)"/>
    /// to get the instance of this class.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class DeleteEntityQuery : ModifyEntityQuery
    {
        internal DeleteEntityQuery(SqlDbQuery query, DeleteEntityQueryBuilder builder) : base(query, builder)
        {
            builder.PrepareBinder();
            mBinder = builder.Binder;
        }

        [DocgenIgnore]
        public override bool IsInsert => false;
    }
}