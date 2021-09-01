using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base class for all entity modification queries.
    ///
    /// This class is an abstract class. Use <see cref="InsertEntityQuery"/>,
    /// <see cref="UpdateEntityQuery"/> or <see cref="DeleteEntityQuery"/> instead.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public abstract class ModifyEntityQuery : EntityQuery
    {
        protected UpdateQueryToTypeBinder mBinder;

#pragma warning disable S3442 // "abstract" classes should not have "public" constructors
        internal ModifyEntityQuery(SqlDbQuery query, EntityQueryBuilder builder) : base(query, builder)
#pragma warning restore S3442 // "abstract" classes should not have "public" constructors
        {
            mQuery = query;
            mBuilder = builder;
        }

        [DocgenIgnore]
        public override int Execute()
        {
            throw new EfSqlException(EfExceptionCode.InvalidOperation);
        }

        /// <summary>
        /// Execute the query for the entity specified.
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Execute(object entity)
        {
            PrepareQuery();
            if (entity is IEntitySerializationCallback callback)
                callback.BeforeSerialization(mQuery.Connection);
            mBinder.BindAndExecute(mQuery, entity, IsInsert);
        }

        /// <summary>
        /// Execute the query for the entity specified
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public virtual async Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            PrepareQuery();
            if (entity is IEntitySerializationCallback callback)
                callback.BeforeSerialization(mQuery.Connection);
            await mBinder.BindAndExecuteAsync(mQuery, entity, IsInsert, token);
        }
    }
}