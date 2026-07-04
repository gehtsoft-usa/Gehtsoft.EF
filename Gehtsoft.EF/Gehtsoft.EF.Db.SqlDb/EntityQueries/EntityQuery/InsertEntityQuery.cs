using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
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

        // Inserts every current property of a freshly-inserted entity's bag - as one combined
        // multi-statement command - then accepts the bag's changes (resetting tracking and clearing
        // the new flag). A no-op when the entity has no bag.
        private void SaveDynamicProperties(object entity)
        {
            (DynamicPropertyBag bag, SqlDbQuery query) = PrepareInsert(entity);
            if (query != null)
                using (query)
                    query.ExecuteNoData();
            bag?.AcceptChanges();
        }

        private async Task SaveDynamicPropertiesAsync(object entity, CancellationToken? token)
        {
            (DynamicPropertyBag bag, SqlDbQuery query) = PrepareInsert(entity);
            if (query != null)
                using (query)
                    await query.ExecuteNoDataAsync(token);
            bag?.AcceptChanges();
        }

        // Returns (bag, query): the bag whose changes to accept after execution, and the combined
        // insert command to execute (null when there is nothing to insert). Both null == nothing to do.
        private (DynamicPropertyBag bag, SqlDbQuery query) PrepareInsert(object entity)
        {
            EntityDescriptor descriptor = EntityQueryBuilder.Descriptor;
            DynamicPropertyBag bag = DynamicPropertiesSaver.GetBag(descriptor, entity);
            if (bag == null)
                return (null, null);

            DynamicPropertiesSaver.RequireNewBag(bag);

            List<(string Name, object Value)> props = Materialize(bag);
            SqlDbQuery query = props.Count > 0 ? BuildInsert(descriptor, entity, props) : null;
            return (bag, query);
        }

        private static List<(string Name, object Value)> Materialize(DynamicPropertyBag bag)
        {
            List<(string Name, object Value)> list = new List<(string Name, object Value)>();
            foreach ((string name, object value) in bag)
                list.Add((name, value));
            return list;
        }

        // Builds one combined command that inserts all property rows. The owner value is a single
        // shared parameter (same for every row); the per-row values use row-suffixed parameters.
        // Throws (e.g. NoPrimaryKeyInTable) if the entity's side table cannot be resolved.
        private SqlDbQuery BuildInsert(EntityDescriptor descriptor, object entity, List<(string Name, object Value)> props)
        {
            SqlDbConnection connection = mQuery.Connection;
            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            object ownerPk = descriptor.PrimaryKey.PropertyAccessor.GetValue(entity);

            MultiSqlQueryBuilder multi = new MultiSqlQueryBuilder(connection.GetLanguageSpecifics());
            for (int row = 0; row < props.Count; row++)
            {
                InsertQueryBuilder insert = connection.GetInsertQueryBuilder(propsTable);
                // owner left unmapped -> shared "owner" parameter; the varying columns are suffixed per row
                insert.SetParameterNames(
                    (DynamicPropertiesTableBuilder.NameColumn, Suffixed(DynamicPropertiesTableBuilder.NameColumn, row)),
                    (DynamicPropertiesTableBuilder.PropTypeColumn, Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row)),
                    (DynamicPropertiesTableBuilder.StringValueColumn, Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row)),
                    (DynamicPropertiesTableBuilder.IntValueColumn, Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row)),
                    (DynamicPropertiesTableBuilder.RealValueColumn, Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row)));
                multi.Add(insert);
            }

            SqlDbQuery query = connection.GetQuery(multi);
            query.BindParam(DynamicPropertiesTableBuilder.OwnerColumn, ownerPk.GetType(), ownerPk);
            for (int row = 0; row < props.Count; row++)
                BindRow(query, row, props[row].Name, props[row].Value);
            return query;
        }

        private static void BindRow(SqlDbQuery query, int row, string name, object value)
        {
            (DynamicPropertyValueType type, string column, object encoded) = DynamicPropertiesValueMapper.Encode(value);

            query.BindParam<string>(Suffixed(DynamicPropertiesTableBuilder.NameColumn, row), name);
            query.BindParam<int>(Suffixed(DynamicPropertiesTableBuilder.PropTypeColumn, row), (int)type);

            DynamicPropertiesSaver.BindValueColumns(query,
                Suffixed(DynamicPropertiesTableBuilder.StringValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.IntValueColumn, row),
                Suffixed(DynamicPropertiesTableBuilder.RealValueColumn, row),
                column, encoded);
        }

        private static string Suffixed(string column, int row) => column + "_" + row.ToString(CultureInfo.InvariantCulture);
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
