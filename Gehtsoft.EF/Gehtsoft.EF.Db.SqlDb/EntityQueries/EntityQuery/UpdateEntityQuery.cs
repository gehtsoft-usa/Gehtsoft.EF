using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

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

        /// <summary>
        /// Updates the entity and then, if it owns dynamic properties, applies the net changes of its
        /// property bag (added / changed / removed).
        /// </summary>
        /// <param name="entity"></param>
        public override void Execute(object entity)
        {
            base.Execute(entity);
            if (EntityQueryBuilder.Descriptor.HasDynamicProperties)
                SaveDynamicProperties(entity);
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
                await SaveDynamicPropertiesAsync(entity, token);
        }

        // Applies the bag's net changes as one combined command, then accepts the changes. A no-op
        // when the entity has no bag or nothing changed. (The owner update needs no read-back, so -
        // unlike insert - there is no reader to close before this runs.)
        private void SaveDynamicProperties(object entity)
        {
            (DynamicPropertyBag bag, SqlDbQuery query) = PrepareUpdate(entity);
            if (query != null)
                using (query)
                    query.ExecuteNoData();
            bag?.AcceptChanges();
        }

        private async Task SaveDynamicPropertiesAsync(object entity, CancellationToken? token)
        {
            (DynamicPropertyBag bag, SqlDbQuery query) = PrepareUpdate(entity);
            if (query != null)
                using (query)
                    await query.ExecuteNoDataAsync(token);
            bag?.AcceptChanges();
        }

        // Returns (bag, query): the bag whose changes to accept, and the combined command applying the
        // net changes (null when there is nothing to apply). A null bag = nothing to do; a NEW bag is
        // rejected (it belongs to an insert, and on update signals a never-loaded bag).
        private (DynamicPropertyBag bag, SqlDbQuery query) PrepareUpdate(object entity)
        {
            EntityDescriptor descriptor = EntityQueryBuilder.Descriptor;
            DynamicPropertyBag bag = DynamicPropertiesSaver.GetBag(descriptor, entity);
            if (bag == null)
                return (null, null);

            DynamicPropertiesSaver.RequireExistingBag(bag);

            if (!bag.AnyModified)
                return (bag, null);

            return (bag, BuildUpdate(descriptor, entity, bag));
        }

        // Added -> INSERT, Changed -> UPDATE, Removed -> DELETE, combined in one command. Each change
        // gets a unique row index (so its suffixed parameters are unique); the owner is a single
        // shared parameter. Values/names are bound after the command is built.
        private SqlDbQuery BuildUpdate(EntityDescriptor descriptor, object entity, DynamicPropertyBag bag)
        {
            SqlDbConnection connection = mQuery.Connection;
            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            object ownerPk = descriptor.PrimaryKey.PropertyAccessor.GetValue(entity);

            MultiSqlQueryBuilder multi = new MultiSqlQueryBuilder(connection.GetLanguageSpecifics());
            List<(int Row, string Name, object Value)> valueRows = new List<(int, string, object)>();
            List<(int Row, string Name)> nameRows = new List<(int, string)>();
            int row = 0;

            foreach ((string name, object value) in bag.Added)
            {
                DynamicPropertiesSaver.AddInsert(multi, connection, propsTable, row);
                valueRows.Add((row, name, value));
                row++;
            }
            foreach ((string name, object value) in bag.Changed)
            {
                DynamicPropertiesSaver.AddUpdate(multi, connection, propsTable, row);
                valueRows.Add((row, name, value));
                row++;
            }
            foreach (string name in bag.Removed)
            {
                DynamicPropertiesSaver.AddDelete(multi, connection, propsTable, row);
                nameRows.Add((row, name));
                row++;
            }

            SqlDbQuery query = connection.GetQuery(multi);
            DynamicPropertiesSaver.BindOwner(query, ownerPk);
            foreach ((int r, string name, object value) in valueRows)
                DynamicPropertiesSaver.BindValueRow(query, r, name, value);
            foreach ((int r, string name) in nameRows)
                DynamicPropertiesSaver.BindNameRow(query, r, name);
            return query;
        }
    }
}
