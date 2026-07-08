using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
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

        /// <summary>
        /// Deletes the entity - first its dynamic property rows (if any), then the entity row.
        /// </summary>
        /// <param name="entity"></param>
        public override void Execute(object entity)
        {
            if (EntityQueryBuilder.Descriptor.HasDynamicProperties)
                using (SqlDbQuery query = BuildDeleteOwned(entity))
                    query.ExecuteNoData();
            base.Execute(entity);
        }

        /// <summary>
        /// Asynchronous version of <see cref="Execute(object)"/>.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="token"></param>
        public override async Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            if (EntityQueryBuilder.Descriptor.HasDynamicProperties)
                using (SqlDbQuery query = BuildDeleteOwned(entity))
                    await query.ExecuteNoDataAsync(token);
            await base.ExecuteAsync(entity, token);
        }

        // Builds "DELETE FROM <table>_props WHERE owner = @owner" for the entity's PK. Independent of
        // the bag - it works whether or not the properties were ever loaded.
        private SqlDbQuery BuildDeleteOwned(object entity)
        {
            EntityDescriptor descriptor = EntityQueryBuilder.Descriptor;
            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            object ownerPk = descriptor.PrimaryKey.PropertyAccessor.GetValue(entity);

            DeleteQueryBuilder delete = mQuery.Connection.GetDeleteQueryBuilder(propsTable);
            delete.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId])
                        .Is(CmpOp.Eq)
                        .Parameter(DynamicPropertiesTableBuilder.OwnerColumn);

            SqlDbQuery query = mQuery.Connection.GetQuery(delete);
            query.BindParam(DynamicPropertiesTableBuilder.OwnerColumn, ownerPk.GetType(), ownerPk);
            return query;
        }
    }
}
