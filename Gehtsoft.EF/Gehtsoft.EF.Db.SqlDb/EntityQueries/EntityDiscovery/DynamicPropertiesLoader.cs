using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

#pragma warning disable S6966 // false positive: methods use sync/async branching pattern

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// Query-agnostic helper that populates the dynamic property bag of entities read from the
    /// database. Properties are fetched from the side table with a single batched
    /// `WHERE owner IN (...)` select per chunk (never one query per entity), then a loaded,
    /// baseline-accepted bag is attached to each entity (empty when it owns no property rows).
    ///
    /// The batched select opens its own reader, so it must run only when no other reader is live on
    /// the connection - the caller (e.g. <see cref="SelectEntitiesQuery"/>) closes the main select's
    /// reader first.
    /// </summary>
    internal static class DynamicPropertiesLoader
    {
        // Owner ids are chunked so the "IN (...)" list stays well under the smallest driver cap
        // (Oracle's 1000-element limit is the tightest).
        private const int OwnerBatchSize = 500;

        private static readonly (string Name, object Value)[] NoProperties = new (string, object)[0];

        /// <summary>Loads and attaches the dynamic properties of a single entity.</summary>
        public static void LoadOne(SqlDbConnection connection, EntityDescriptor descriptor, object entity)
            => LoadCore(connection, descriptor, new[] { entity }, true, null).GetAwaiter().GetResult();

        /// <summary>Asynchronous version of <see cref="LoadOne"/>.</summary>
        public static Task LoadOneAsync(SqlDbConnection connection, EntityDescriptor descriptor, object entity, CancellationToken? token)
            => LoadCore(connection, descriptor, new[] { entity }, false, token);

        /// <summary>Loads and attaches the dynamic properties of many entities in one batched pass.</summary>
        public static void LoadMany(SqlDbConnection connection, EntityDescriptor descriptor, IList entities)
            => LoadCore(connection, descriptor, entities, true, null).GetAwaiter().GetResult();

        /// <summary>Asynchronous version of <see cref="LoadMany"/>.</summary>
        public static Task LoadManyAsync(SqlDbConnection connection, EntityDescriptor descriptor, IList entities, CancellationToken? token)
            => LoadCore(connection, descriptor, entities, false, token);

        private static async Task LoadCore(SqlDbConnection connection, EntityDescriptor descriptor, IList entities, bool sync, CancellationToken? token)
        {
            if (entities == null || entities.Count == 0)
                return;

            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            TableDescriptor.ColumnInfo pk = descriptor.PrimaryKey;
            IPropertyAccessor pkAccessor = pk.PropertyAccessor;

            // owner pk per entity (as read from the entity), and the string key used to group the
            // returned rows back to their owner (string keying avoids int-vs-long boxing mismatches).
            object[] pks = new object[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                pks[i] = pkAccessor.GetValue(entities[i]);

            Dictionary<string, List<(string Name, object Value)>> byOwner = new Dictionary<string, List<(string Name, object Value)>>();
            for (int start = 0; start < pks.Length; start += OwnerBatchSize)
            {
                int count = Math.Min(OwnerBatchSize, pks.Length - start);
                using (SqlDbQuery query = BuildBatchSelect(connection, propsTable, pk.DbType, pks, start, count))
                {
                    if (sync)
                        query.ExecuteReader();
                    else
                        await query.ExecuteReaderAsync(token);

                    while (sync ? query.ReadNext() : await query.ReadNextAsync(token))
                    {
                        string ownerKey = OwnerKey(query.GetValue<object>(0));
                        string name = query.GetValue<string>(1);
                        DynamicPropertyValueType type = (DynamicPropertyValueType)query.GetValue<int>(2);
                        object value = DynamicPropertiesValueMapper.Decode(type, ReadStored(query, type));

                        if (!byOwner.TryGetValue(ownerKey, out List<(string Name, object Value)> list))
                        {
                            list = new List<(string Name, object Value)>();
                            byOwner[ownerKey] = list;
                        }
                        list.Add((name, value));
                    }
                }
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if (!byOwner.TryGetValue(OwnerKey(pks[i]), out List<(string Name, object Value)> list))
                    entities[i].LoadDynamicProperties(NoProperties);
                else
                    entities[i].LoadDynamicProperties(list);
            }
        }

        // SELECT owner, name, prop_type, v_str, v_int, v_real FROM <t>_props WHERE owner IN (@id...)
        private static SqlDbQuery BuildBatchSelect(SqlDbConnection connection, TableDescriptor propsTable, System.Data.DbType idType, object[] pks, int start, int count)
        {
            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = "lpid_" + i.ToString(CultureInfo.InvariantCulture);

            SelectQueryBuilder select = connection.GetSelectQueryBuilder(propsTable);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId]);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.NameColumnId]);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.PropTypeColumnId]);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.StringValueColumnId]);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.IntValueColumnId]);
            select.AddToResultset(propsTable[DynamicPropertiesTableBuilder.RealValueColumnId]);
            select.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId]).Is(CmpOp.In).Parameters(names);

            SqlDbQuery query = connection.GetQuery(select);
            for (int i = 0; i < count; i++)
                query.BindParam(names[i], idType, pks[start + i]);
            return query;
        }

        // The stored value lives in the single value column selected by the type code (the resultset
        // order is owner=0, name=1, prop_type=2, v_str=3, v_int=4, v_real=5).
        private static object ReadStored(SqlDbQuery query, DynamicPropertyValueType type)
        {
            switch (type)
            {
                case DynamicPropertyValueType.String:
                    return query.GetValue<object>(3);
                case DynamicPropertyValueType.Real:
                    return query.GetValue<object>(5);
                default:
                    return query.GetValue<object>(4);
            }
        }

        private static string OwnerKey(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
