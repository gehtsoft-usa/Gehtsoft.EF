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
    /// The query to update multiple entities by the condition.
    ///
    /// Use <see cref="EntityConnectionExtension.GetMultiUpdateEntityQuery(SqlDbConnection, Type)"/> to get
    /// an instance of this query.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class MultiUpdateEntityQuery : ConditionEntityQueryBase
    {
        // Owner ids are handled in chunks so "IN (...)" lists stay well under the smallest driver cap.
        private const int IdBatchSize = 50;

        internal readonly UpdateEntityQueryBuilder mUpdateBuilder;
        private readonly List<(string Name, object Value)> mSetProperties = new List<(string, object)>();
        private readonly List<string> mRemoveProperties = new List<string>();
        private bool mHasOwnerColumnUpdates;

        internal MultiUpdateEntityQuery(SqlDbQuery query, UpdateEntityQueryBuilder builder) : base(query, builder)
        {
            mUpdateBuilder = builder;
        }

        /// <summary>
        /// Add the value to set to all the records for the specified property.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        public void AddUpdateColumn<T>(string propertyName, T value)
        {
            mUpdateBuilder.AddUpdateColumn(propertyName);
            mQuery.BindParam(propertyName, value);
            mHasOwnerColumnUpdates = true;
        }

        /// <summary>
        /// Add the value to set to all the records for the specified property.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="rawExpression"></param>
        public void AddUpdateColumnByExpression(string propertyName, string rawExpression)
        {
            mUpdateBuilder.AddUpdateColumnByExpression(propertyName, rawExpression);
            mHasOwnerColumnUpdates = true;
        }

        /// <summary>
        /// Sets a dynamic property to <paramref name="value"/> for every matched entity (replacing any
        /// existing value). The value type must be one of the supported dynamic-property types.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetDynamicProperty(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Use RemoveDynamicProperty to clear a property.");
            mSetProperties.Add((name, value));
        }

        /// <summary>
        /// Removes a dynamic property from every matched entity.
        /// </summary>
        /// <param name="name"></param>
        public void RemoveDynamicProperty(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            mRemoveProperties.Add(name);
        }

        private bool HasPropertyChanges => mSetProperties.Count > 0 || mRemoveProperties.Count > 0;

        /// <summary>
        /// Updates the matching entities' columns and/or dynamic properties.
        /// </summary>
        public override int Execute()
        {
            if (!HasPropertyChanges)
                return base.Execute(); // owner columns only - one statement, any condition (incl. dynamic-property filter)
            return RunWithPropertyChanges(true, null).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Asynchronous version of <see cref="Execute"/>.
        /// </summary>
        /// <param name="token"></param>
        public override async Task<int> ExecuteAsync(CancellationToken? token = null)
        {
            if (!HasPropertyChanges)
                return await base.ExecuteAsync(token);
            return await RunWithPropertyChanges(false, token);
        }

        // Setting/removing dynamic properties touches _props for every matched owner, so - exactly like
        // MultiDelete - we materialize the matched owner ids first (a props change filtered by a
        // dynamic-property condition would otherwise self-reference _props on MySQL, and could disturb
        // the condition). Then, inside a (nested) transaction, we update the owner columns (one
        // statement, by the original condition, before the property changes so a property being set
        // cannot affect the filter) and apply the property changes by the fixed id-set, in batches.
        private async Task<int> RunWithPropertyChanges(bool sync, CancellationToken? token)
        {
            EntityDescriptor descriptor = EntityQueryBuilder.Descriptor;
            SqlDbConnection connection = mQuery.Connection;
            TableDescriptor propsTable = descriptor.DynamicPropertiesTable;
            TableDescriptor.ColumnInfo pk = descriptor.PrimaryKey;

            // Flush the condition (so its rendered text is available) without rendering the owner update
            // yet - the SET may be empty (a properties-only update), which is not a valid UPDATE.
            Where.SetCurrentSingleEntityQueryConditionBuilder(null);
            ConditionBuilder ownerWhere = mConditionQueryBuilder.Where.ConditionBuilder;

            int matched;
            using (SqlDbTransaction transaction = connection.BeginTransaction())
            {
                List<object> ids = new List<object>();
                using (SqlDbQuery idQuery = connection.GetQuery(DynamicPropertiesSaver.BuildMatchedIdsSelect(connection, descriptor, ownerWhere)))
                {
                    idQuery.CopyParametersFrom(mQuery);
                    if (sync)
                        idQuery.ExecuteReader();
                    else
                        await idQuery.ExecuteReaderAsync(token);
                    while (idQuery.ReadNext())
                        ids.Add(idQuery.GetValue<object>(0));
                }
                matched = ids.Count;

                // owner columns (if any) - one statement by the original condition. Run it through a
                // query created INSIDE the transaction so it enlists (mQuery predates the transaction
                // and would not be associated with it); copy the SET/condition parameters over.
                if (mHasOwnerColumnUpdates)
                {
                    PrepareQuery(); // render the owner UPDATE into mBuilder.QueryBuilder
                    using (SqlDbQuery ownerUpdate = connection.GetQuery(mBuilder.QueryBuilder))
                    {
                        ownerUpdate.CopyParametersFrom(mQuery);
                        if (sync) ownerUpdate.ExecuteNoData(); else await ownerUpdate.ExecuteNoDataAsync(token);
                    }
                }

                for (int start = 0; start < ids.Count; start += IdBatchSize)
                {
                    int count = Math.Min(IdBatchSize, ids.Count - start);
                    using (SqlDbQuery batch = BuildPropertyBatch(connection, propsTable, pk.DbType, ids, start, count))
                        if (sync) batch.ExecuteNoData(); else await batch.ExecuteNoDataAsync(token);
                }

                if (sync) transaction.Commit(); else await transaction.CommitAsync();
            }

            RowsAffected = matched;
            Executed = true;
            return matched;
        }

        // For one batch of owner ids: clear every touched property name (set + removed) for those
        // owners, then insert the current value of each set-property for each owner.
        private SqlDbQuery BuildPropertyBatch(SqlDbConnection connection, TableDescriptor propsTable, DbType idType, List<object> ids, int start, int count)
        {
            MultiSqlQueryBuilder multi = new MultiSqlQueryBuilder(connection.GetLanguageSpecifics());

            string[] ownerParams = new string[count];
            for (int i = 0; i < count; i++)
                ownerParams[i] = "muown_" + i.ToString(CultureInfo.InvariantCulture);

            // distinct names to clear = set names + removed names
            List<string> clearNames = new List<string>();
            foreach ((string name, object _) in mSetProperties)
                if (!clearNames.Contains(name)) clearNames.Add(name);
            foreach (string name in mRemoveProperties)
                if (!clearNames.Contains(name)) clearNames.Add(name);

            // DELETE props WHERE owner IN (@owner...) AND name = @clear_k
            for (int k = 0; k < clearNames.Count; k++)
            {
                DeleteQueryBuilder delete = connection.GetDeleteQueryBuilder(propsTable);
                delete.Where.Property(propsTable[DynamicPropertiesTableBuilder.OwnerColumnId]).Is(CmpOp.In).Parameters(ownerParams);
                delete.Where.Property(propsTable[DynamicPropertiesTableBuilder.NameColumnId]).Is(CmpOp.Eq).Parameter("muclr_" + k.ToString(CultureInfo.InvariantCulture));
                multi.Add(delete);
            }

            // one INSERT per (set-property, owner)
            List<(int Row, int OwnerIndex, int SetIndex)> insertRows = new List<(int, int, int)>();
            int row = 0;
            for (int s = 0; s < mSetProperties.Count; s++)
                for (int i = 0; i < count; i++)
                {
                    DynamicPropertiesSaver.AddBulkInsert(multi, connection, propsTable, row);
                    insertRows.Add((row, i, s));
                    row++;
                }

            multi.PrepareQuery();
            SqlDbQuery query = connection.GetQuery(multi);

            for (int i = 0; i < count; i++)
                query.BindParam(ownerParams[i], idType, ids[start + i]);
            for (int k = 0; k < clearNames.Count; k++)
                query.BindParam<string>("muclr_" + k.ToString(CultureInfo.InvariantCulture), clearNames[k]);
            foreach ((int r, int ownerIndex, int setIndex) in insertRows)
                DynamicPropertiesSaver.BindBulkInsert(query, r, ids[start + ownerIndex], mSetProperties[setIndex].Name, mSetProperties[setIndex].Value);

            return query;
        }
    }
}
