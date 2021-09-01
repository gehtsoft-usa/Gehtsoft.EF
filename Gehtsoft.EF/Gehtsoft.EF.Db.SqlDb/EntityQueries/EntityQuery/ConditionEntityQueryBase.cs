using System;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    /// <summary>
    /// The base class for all queries with a condition.
    ///
    /// This is a base abstract base class. Use <see cref="MultiUpdateEntityQuery"/>, <see cref="MultiDeleteEntityQuery"/>,
    /// <see cref="SelectEntitiesQueryBase"/>, <see cref="SelectEntitiesCountQuery"/> or <see cref="SelectEntitiesQuery"/>
    /// instead.
    ///
    /// The object instance must be disposed after use. Some databases requires the query to be disposed before the next query may be executed.
    /// </summary>
    public class ConditionEntityQueryBase : EntityQuery
    {
        private int mAutoParam = 1;

        private static int mQueryID = 1;

        private static int NextQueryID => mQueryID = (mQueryID + 1) & 0xff_ffff;

        /// <summary>
        /// The prefix for the parameters.
        /// </summary>
        public string WhereParamPrefix { get; set; } = $"auto{NextQueryID}_";

        protected internal string NextParam => $"{WhereParamPrefix}{mAutoParam++}";

        internal readonly EntityQueryWithWhereBuilder mConditionQueryBuilder;

        internal EntityQueryWithWhereBuilder ConditionQueryBuilder => mConditionQueryBuilder;

        /// <summary>
        /// The where condition.
        /// </summary>
        public EntityQueryConditionBuilder Where { get; protected set; }

        protected virtual bool IsReader => false;

        internal ConditionEntityQueryBase(SqlDbQuery query, EntityQueryWithWhereBuilder builder) : base(query, builder)
        {
            mConditionQueryBuilder = builder;
            Where = new EntityQueryConditionBuilder(this, builder.Where);
        }

        protected bool Executed { get; set; } = false;

        [DocgenIgnore]
        public override int Execute()
        {
            PrepareQuery();
            if (IsReader)
                mQuery.ExecuteReader();
            else
                RowsAffected = mQuery.ExecuteNoData();
            Executed = true;
            return RowsAffected;
        }

        [DocgenIgnore]
        public override async Task<int> ExecuteAsync(CancellationToken? token = null)
        {
            PrepareQuery();
            if (IsReader)
                await mQuery.ExecuteReaderAsync(token);
            else
                RowsAffected = await mQuery.ExecuteNoDataAsync(token);
            Executed = true;
            return RowsAffected;
        }

        internal EntityQueryWithWhereBuilder.EntityQueryItem GetItem(string path)
        {
            return mConditionQueryBuilder.FindPath(path);
        }

        protected override void PrepareQuery()
        {
            Where.SetCurrentSingleEntityQueryConditionBuilder(null);
            base.PrepareQuery();
        }

        internal EntityQueryWithWhereBuilder.EntityQueryItem GetItem(Type type, string property, int occurrence = 0)
        {
            return mConditionQueryBuilder.FindItem(property, type, occurrence);
        }

        /// <summary>
        /// The reference to an entity property in the query
        /// </summary>
        public class InQueryName : IInQueryFieldReference
        {
            internal EntityQueryWithWhereBuilder.EntityQueryItem Item { get; }

            /// <summary>
            /// The path to the property for them query root
            /// </summary>
            public string Path => Item.Path;

            private readonly string mAlias;

            /// <summary>
            /// Alias of the field in the query.
            /// </summary>
            public string Alias => mAlias ?? $"{Item.QueryEntity.Alias}.{Item.Column.Name}";

            internal InQueryName(EntityQueryWithWhereBuilder.EntityQueryItem item, string alias = null)
            {
                mAlias = alias;
                Item = item;
            }
        }

        /// <summary>
        /// Gets the reference by the property path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public InQueryName GetReference(string path) => GetReference(mConditionQueryBuilder.FindPath(path));

        /// <summary>
        /// Gets the reference of the specified property of the first occurrence of the specified type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public InQueryName GetReference(Type type, string property) => GetReference(type, 0, property);

        /// <summary>
        /// Gets the reference of the specified property of the specified occurrence of the specified type.
        ///
        /// Use this method when the entity is included into the query more than once.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="occurence"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public InQueryName GetReference(Type type, int occurence, string property) => GetReference(mConditionQueryBuilder.FindItem(property, type, occurence));

        internal InQueryName GetReference(EntityQueryWithWhereBuilder.EntityQueryItem queryItem)
        {
            string alias = mConditionQueryBuilder.GetAlias(queryItem);
            return new InQueryName(queryItem, alias);
        }

        internal void CopyParametersFrom(SelectEntitiesQueryBase query) => mQuery.CopyParametersFrom(query.mQuery);
    }
}