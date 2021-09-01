using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query to insert an entity to the DB.
    ///
    /// Use <see cref="EntityConnectionExtension.GetInsertEntityQuery(SqlDbConnection, System.Type, bool)"/>
    /// to get an instance of this object.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class InsertEntityQuery : ModifyEntityQuery
    {
        private readonly InsertEntityQueryBuilder mInsertBuilder;

        internal InsertEntityQuery(SqlDbQuery query, InsertEntityQueryBuilder builder) : base(query, builder)
        {
            mInsertBuilder = builder;
            mBinder = builder.Binder;
        }

        [DocgenIgnore]
        public override bool IsInsert => !mInsertBuilder.IgnoreAutoIncrement;
    }
}