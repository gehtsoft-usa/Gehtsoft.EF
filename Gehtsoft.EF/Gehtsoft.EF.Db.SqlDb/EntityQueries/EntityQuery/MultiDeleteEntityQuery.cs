namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The query to delete multiple entities by the condition.
    ///
    /// Use <see cref="EntityConnectionExtension.GetMultiDeleteEntityQuery(SqlDbConnection, System.Type)"/> to get
    /// an instance of this query.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class MultiDeleteEntityQuery : ConditionEntityQueryBase
    {
        internal MultiDeleteEntityQuery(SqlDbQuery query, DeleteEntityQueryBuilder builder) : base(query, builder)
        {
        }
    }
}