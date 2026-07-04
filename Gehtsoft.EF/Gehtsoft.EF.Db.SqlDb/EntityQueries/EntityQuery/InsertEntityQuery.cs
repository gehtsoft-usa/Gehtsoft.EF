using System;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
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

        /// <summary>
        /// Inserts the entity and then, if it owns dynamic properties, its property rows.
        /// </summary>
        /// <param name="entity"></param>
        public override void Execute(object entity)
        {
            base.Execute(entity);
            if (EntityQueryBuilder.Descriptor.HasDynamicProperties)
                DynamicPropertiesSaver.SaveOnInsert(mQuery.Connection, EntityQueryBuilder.Descriptor, entity);
        }

        /// <summary>
        /// Asynchronous version of <see cref="Execute(object)"/>.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="token"></param>
        public override async Task ExecuteAsync(object entity, CancellationToken? token = null)
        {
            await base.ExecuteAsync(entity, token);
            if (EntityQueryBuilder.Descriptor.HasDynamicProperties)
                await DynamicPropertiesSaver.SaveOnInsertAsync(mQuery.Connection, EntityQueryBuilder.Descriptor, entity, token);
        }
    }

    /// <summary>
    /// The query to insert the result of entity select query into another entity.
    ///
    /// Use <see cref="EntityConnectionExtension.GetInsertSelectEntityQuery(SqlDbConnection, Type, SelectEntitiesQueryBase, bool, string[])"/>
    /// to get an instance of the query.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class InsertSelectEntityQuery : EntityQuery
    {
        private readonly Type mType;

        internal InsertSelectEntityQuery(SqlDbQuery query, Type type, SelectQueryBuilder selectQuery, bool ignoreAutoIncrement, string[] includeOnlyProperties) : base(query, new InsertSelectEntityQueryBuilder(type, query.Connection, selectQuery, ignoreAutoIncrement, includeOnlyProperties))
        {
            mType = type;
        }

        public override void PrepareQuery()
        {
            base.PrepareQuery();

            if (mQuery.Connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                mQuery.BindOutput("id", AllEntities.Get(mType).TableDescriptor.PrimaryKey.DbType);
        }
    }
}