using System;
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
    /// The query to delete multiple entities by the condition.
    ///
    /// Use <see cref="EntityConnectionExtension.GetMultiDeleteEntityQuery(SqlDbConnection, System.Type)"/> to get
    /// an instance of this query.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class MultiDeleteEntityQuery : ConditionEntityQueryBase
    {
        // Owner ids are deleted in chunks so the "IN (...)" lists stay well under the smallest driver
        // cap. Dynamic properties are for flexible storage + occasional filtering, not mass deletes.
        private const int IdBatchSize = 50;

        internal MultiDeleteEntityQuery(SqlDbQuery query, DeleteEntityQueryBuilder builder) : base(query, builder)
        {
        }

        /// <summary>
        /// Deletes the matching entities - and, for an owner of dynamic properties, their property
        /// rows first (FK order), driven by the same condition.
        /// </summary>
        public override int Execute()
        {
            if (!EntityQueryBuilder.Descriptor.HasDynamicProperties)
                return base.Execute();
            return RunCascade(true, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronous version of <see cref="Execute"/>.
        /// </summary>
        /// <param name="token"></param>
        public override async Task<int> ExecuteAsync(CancellationToken? token = null)
        {
            if (!EntityQueryBuilder.Descriptor.HasDynamicProperties)
                return await base.ExecuteAsync(token);
            return await RunCascade(false, token);
        }

        // Two shapes, chosen by whether the WHERE reads the _props table (all fragments come from
        // driver builders - we never hand-write SQL):
        //  * no WHERE, or WHERE on regular columns -> one combined command: delete props (child) then
        //    owners (parent). The props-delete's sub-query reads the owner table, so there is no
        //    mutual dependency and a single atomic command is enough.
        //  * WHERE filters on a dynamic property (reads _props) -> materialize the matched owner ids
        //    first (the props delete would otherwise destroy the rows the condition reads), then delete
        //    both tables by that fixed id-set, inside a (nested) transaction, in batches.
        private async Task<int> RunCascade(bool sync, CancellationToken? token)
        {
            PrepareQuery(); // flush the condition + render the owner delete (parameters already bound)

            ConditionBuilder ownerWhere = mConditionQueryBuilder.Where.ConditionBuilder;

            int affected;
            if (ownerWhere.IsEmpty || !DynamicPropertiesSaver.ConditionReferencesProps(EntityQueryBuilder.Descriptor, ownerWhere))
            {
                mQuery.CommandText = BuildCombinedCascade(ownerWhere).Query;
                affected = sync ? mQuery.ExecuteNoData() : await mQuery.ExecuteNoDataAsync(token);
            }
            else
            {
                affected = await DeleteByMaterializedIds(sync, token);
            }

            RowsAffected = affected;
            Executed = true;
            return affected;
        }

        // no-WHERE / regular-column condition: one combined command, props first (FK order).
        private MultiSqlQueryBuilder BuildCombinedCascade(ConditionBuilder ownerWhere)
        {
            SqlDbConnection connection = mQuery.Connection;
            TableDescriptor propsTable = EntityQueryBuilder.Descriptor.DynamicPropertiesTable;

            DeleteQueryBuilder propsDelete = connection.GetDeleteQueryBuilder(propsTable);
            if (!ownerWhere.IsEmpty)
                propsDelete.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId])
                                 .Is(CmpOp.In)
                                 .Query(DynamicPropertiesSaver.BuildMatchedIdsSelect(mQuery.Connection, EntityQueryBuilder.Descriptor, ownerWhere));
            // else: no WHERE -> delete all property rows

            MultiSqlQueryBuilder multi = new MultiSqlQueryBuilder(connection.GetLanguageSpecifics());
            multi.Add(propsDelete);           // property rows first (FK order)
            multi.Add(mBuilder.QueryBuilder); // then the owner rows (already rendered by PrepareQuery)
            multi.PrepareQuery();
            return multi;
        }

        // Dynamic-property condition: pre-read the matched owner ids, then delete props then owners by
        // that fixed id-set, in a transaction, in batches. The condition sub-query reads _props, so the
        // owner delete can NOT re-evaluate it once props are gone - hence the materialization.
        private async Task<int> DeleteByMaterializedIds(bool sync, CancellationToken? token)
        {
            SqlDbConnection connection = mQuery.Connection;
            EntityDescriptor descriptor = EntityQueryBuilder.Descriptor;
            TableDescriptor ownerTable = descriptor.TableDescriptor;
            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            TableDescriptor.ColumnInfo pk = descriptor.PrimaryKey;
            ConditionBuilder ownerWhere = mConditionQueryBuilder.Where.ConditionBuilder;
            bool selfReferenceAllowed = connection.GetLanguageSpecifics().SelfReferenceInDeleteAllowed;

            int affected = 0;
            using (SqlDbTransaction transaction = connection.BeginTransaction()) // nests where the driver supports it
            {
                // Pre-read the matched owner ids (before the props delete destroys the condition's basis).
                List<object> ids = new List<object>();
                using (SqlDbQuery idQuery = connection.GetQuery(DynamicPropertiesSaver.BuildMatchedIdsSelect(mQuery.Connection, EntityQueryBuilder.Descriptor, ownerWhere)))
                {
                    idQuery.CopyParametersFrom(mQuery);
                    if (sync)
                        idQuery.ExecuteReader();
                    else
                        await idQuery.ExecuteReaderAsync(token);
                    while (idQuery.ReadNext())
                        ids.Add(idQuery.GetValue<object>(0));
                }

                if (ids.Count > 0)
                {
                    // 1. property rows first (FK order)
                    if (selfReferenceAllowed)
                    {
                        // one statement; the condition sub-query is legal here (props still intact,
                        // and the engine permits deleting a table referenced in its own sub-query)
                        using (SqlDbQuery propsQuery = connection.GetQuery(BuildPropsSubqueryDelete(ownerWhere)))
                        {
                            propsQuery.CopyParametersFrom(mQuery);
                            if (sync) propsQuery.ExecuteNoData(); else await propsQuery.ExecuteNoDataAsync(token);
                        }
                    }
                    else
                    {
                        // MySQL: self-reference is rejected (error 1093) -> delete by the materialized ids
                        await DeleteInBatches(propsTable, propsTable[DynamicPropertiesTableBuilder.OwnerColumnId], pk.DbType, ids, sync, token);
                    }

                    // 2. owner rows by the materialized ids
                    affected = await DeleteInBatches(ownerTable, pk, pk.DbType, ids, sync, token);
                }

                if (sync) transaction.Commit(); else await transaction.CommitAsync();
            }
            return affected;
        }

        private async Task<int> DeleteInBatches(TableDescriptor table, TableDescriptor.ColumnInfo column, DbType idType, List<object> ids, bool sync, CancellationToken? token)
        {
            int affected = 0;
            for (int start = 0; start < ids.Count; start += IdBatchSize)
            {
                int count = Math.Min(IdBatchSize, ids.Count - start);
                using (SqlDbQuery delete = BuildInDelete(table, column, idType, ids, start, count))
                    affected += sync ? delete.ExecuteNoData() : await delete.ExecuteNoDataAsync(token);
            }
            return affected;
        }

        // DELETE FROM <table> WHERE <column> IN (@id...) for one batch of materialized owner ids.
        private SqlDbQuery BuildInDelete(TableDescriptor table, TableDescriptor.ColumnInfo column, DbType idType, List<object> ids, int start, int count)
        {
            SqlDbConnection connection = mQuery.Connection;

            string[] names = new string[count];
            for (int i = 0; i < count; i++)
                names[i] = "mdid_" + i.ToString(CultureInfo.InvariantCulture);

            DeleteQueryBuilder delete = connection.GetDeleteQueryBuilder(table);
            delete.Where.Property(column).Is(CmpOp.In).Parameters(names);

            SqlDbQuery query = connection.GetQuery(delete);
            for (int i = 0; i < count; i++)
                query.BindParam(names[i], idType, ids[start + i]);
            return query;
        }

        private DeleteQueryBuilder BuildPropsSubqueryDelete(ConditionBuilder ownerWhere)
        {
            TableDescriptor propsTable = EntityQueryBuilder.Descriptor.DynamicPropertiesTable;
            DeleteQueryBuilder propsDelete = mQuery.Connection.GetDeleteQueryBuilder(propsTable);
            propsDelete.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId])
                             .Is(CmpOp.In)
                             .Query(DynamicPropertiesSaver.BuildMatchedIdsSelect(mQuery.Connection, EntityQueryBuilder.Descriptor, ownerWhere));
            return propsDelete;
        }

    }
}
