using System;
using System.Threading;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    public class ConditionEntityQueryBase : EntityQuery
    {
        private int mAutoParam = 1;

        private static int mQueryID = 1;

        private static int NextQueryID => mQueryID = (mQueryID + 1) & 0xff_ffff;

        public string WhereParamPrefix { get; set; } = $"auto{NextQueryID}_";

        protected internal string NextParam => $"{WhereParamPrefix}{mAutoParam++}";

        protected readonly EntityQueryWithWhereBuilder mConditionQueryBuilder;

        public EntityQueryWithWhereBuilder ConditionQueryBuilder => mConditionQueryBuilder;

        public EntityQueryConditionBuilder Where { get; protected set; }

        protected virtual bool IsReader => false;

        internal ConditionEntityQueryBase(SqlDbQuery query, EntityQueryWithWhereBuilder builder) : base(query, builder)
        {
            mConditionQueryBuilder = builder;
            Where = new EntityQueryConditionBuilder(this, builder.Where);
        }

        protected bool Executed { get; set; } = false;

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

        public override async Task<int> ExecuteAsync(CancellationToken? token)
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

        internal EntityQueryWithWhereBuilder.EntityQueryItem GetItem(Type type, string property, int occurrence = 0)
        {
            return mConditionQueryBuilder.FindItem(property, type, occurrence);
        }

        public class InQueryName
        {
            internal EntityQueryWithWhereBuilder.EntityQueryItem Item { get; }

            public string Path => Item.Path;

            private readonly string mAlias;

            public string Alias => mAlias ?? $"{Item.QueryEntity.Alias}.{Item.Column.Name}";

            internal InQueryName(EntityQueryWithWhereBuilder.EntityQueryItem item, string alias = null)
            {
                mAlias = alias;
                Item = item;
            }
        }

        public InQueryName GetReference(string path) => GetReference(mConditionQueryBuilder.FindPath(path));

        public InQueryName GetReference(Type type, string property) => GetReference(type, 0, property);

        public InQueryName GetReference(Type type, int occurence, string property) => GetReference(mConditionQueryBuilder.FindItem(property, type, occurence));

        internal InQueryName GetReference(EntityQueryWithWhereBuilder.EntityQueryItem queryItem)
        {
            string alias = mConditionQueryBuilder.GetAlias(queryItem);
            return new InQueryName(queryItem, alias);
        }

        internal void CopyParametersFrom(SelectEntitiesQueryBase query) => mQuery.CopyParametersFrom(query.mQuery);
    }
}