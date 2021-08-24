using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public abstract class ModifyEntityQuery : EntityQuery
    {
        protected UpdateQueryToTypeBinder mBinder;

        internal ModifyEntityQuery(SqlDbQuery query, EntityQueryBuilder builder) : base(query, builder)
        {
            mQuery = query;
            mBuilder = builder;
        }

        public override int Execute()
        {
            throw new EfSqlException(EfExceptionCode.InvalidOperation);
        }

        public virtual void Execute(object entity)
        {
            PrepareQuery();
            if (entity is IEntitySerializationCallback callback)
                callback.BeforeSerialization(mQuery.Connection);
            mBinder.BindAndExecute(mQuery, entity, IsInsert);
        }

#pragma warning disable S4019 // Base class methods should not be hidden
        // by design. base method has no meaning
        public Task ExecuteAsync(object entity) => ExecuteAsync(entity, null);
#pragma warning restore S4019 // Base class methods should not be hidden

        public virtual async Task ExecuteAsync(object entity, CancellationToken? token)
        {
            PrepareQuery();
            if (entity is IEntitySerializationCallback callback)
                callback.BeforeSerialization(mQuery.Connection);
            await mBinder.BindAndExecuteAsync(mQuery, entity, IsInsert, token);
        }
    }
}