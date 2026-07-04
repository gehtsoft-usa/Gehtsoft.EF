using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Persists an entity's dynamic property bag to its EAV side table.
    /// </summary>
    internal static class DynamicPropertiesSaver
    {
        /// <summary>
        /// Inserts every current property of a freshly-inserted entity's bag - as one combined
        /// multi-statement command - then accepts the bag's changes (resetting tracking and
        /// clearing the new flag). A no-op when the entity has no dynamic properties or no bag.
        /// </summary>
        public static void SaveOnInsert(SqlDbConnection connection, EntityDescriptor descriptor, object entity)
        {
            DynamicPropertyBag bag = GetBagToSave(descriptor, entity);
            if (bag == null)
                return;

            RequireNewBag(bag);

            List<(string Name, object Value)> props = Materialize(bag);
            if (props.Count > 0)
            {
                using (SqlDbQuery query = BuildInsert(connection, descriptor, entity, props))
                    query.ExecuteNoData();
            }

            bag.AcceptChanges();
        }

        /// <summary>
        /// Asynchronous version of <see cref="SaveOnInsert"/>.
        /// </summary>
        public static async Task SaveOnInsertAsync(SqlDbConnection connection, EntityDescriptor descriptor, object entity, CancellationToken? token = null)
        {
            DynamicPropertyBag bag = GetBagToSave(descriptor, entity);
            if (bag == null)
                return;

            RequireNewBag(bag);

            List<(string Name, object Value)> props = Materialize(bag);
            if (props.Count > 0)
            {
                using (SqlDbQuery query = BuildInsert(connection, descriptor, entity, props))
                    await query.ExecuteNoDataAsync(token);
            }

            bag.AcceptChanges();
        }

        // Returns the bag to persist, or null when there is nothing to save (the entity does not
        // own dynamic properties, or it has no bag). Null is "nothing to do", not a failure.
        private static DynamicPropertyBag GetBagToSave(EntityDescriptor descriptor, object entity)
        {
            if (!descriptor.HasDynamicProperties)
                return null;
            return (entity as IDynamicPropertiesOwner)?.DynamicProperties;
        }

        private static void RequireNewBag(DynamicPropertyBag bag)
        {
            if (!bag.IsNew)
                throw new EfSqlException(EfExceptionCode.DynamicPropertiesBagIsNotNew);
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
        private static SqlDbQuery BuildInsert(SqlDbConnection connection, EntityDescriptor descriptor, object entity, List<(string Name, object Value)> props)
        {
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

            BindValueColumn(query, row, DynamicPropertiesTableBuilder.StringValueColumn, DbType.String, column, encoded);
            BindValueColumn(query, row, DynamicPropertiesTableBuilder.IntValueColumn, DbType.Int64, column, encoded);
            BindValueColumn(query, row, DynamicPropertiesTableBuilder.RealValueColumn, DbType.Double, column, encoded);
        }

        private static void BindValueColumn(SqlDbQuery query, int row, string valueColumn, DbType valueColumnType, string encodedColumn, object encoded)
        {
            string parameter = Suffixed(valueColumn, row);
            if (valueColumn == encodedColumn)
                query.BindParam(parameter, valueColumnType, encoded);
            else
                query.BindNull(parameter, valueColumnType);
        }

        private static string Suffixed(string column, int row) => column + "_" + row.ToString(CultureInfo.InvariantCulture);
    }
}
