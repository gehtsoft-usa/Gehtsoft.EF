using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.MssqlDb
{
    internal class MssqlConditionBuilder : ConditionBuilder
    {
        private readonly MssqlSelectQueryBuilder mBuilder;

        public MssqlConditionBuilder(MssqlSelectQueryBuilder builder) : base(builder)
        {
            mBuilder = builder;
        }

        public override string Query(AQueryBuilder queryBuilder)
        {
            if (queryBuilder.Query == null)
                queryBuilder.PrepareQuery();

            if (queryBuilder is MssqlHierarchicalSelectQueryBuilder hierarchicalSelectQueryBuilder)
            {
                mBuilder.With = hierarchicalSelectQueryBuilder.With;
                return hierarchicalSelectQueryBuilder.Select;
            }
            else if (queryBuilder is MssqlSelectQueryBuilder selectQueryBuilder && (queryBuilder as MssqlSelectQueryBuilder).With != null)
            {
                mBuilder.With = selectQueryBuilder.With;
                if (queryBuilder.Query == null)
                    queryBuilder.PrepareQuery();
                string query = queryBuilder.Query.Substring(mBuilder.With.Length);
                return query;
            }
            return base.Query(queryBuilder);
        }
    }

    internal class MssqlSelectQueryBuilder : SelectQueryBuilder
    {
        public string With { get; internal set; } = null;

        internal MssqlSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            mWhere = new MssqlConditionBuilder(this);
        }

        public override void PrepareQuery()
        {
            StringBuilder query;

            if ((Limit > 0 || Skip > 0) && (mOrderBy == null || mOrderBy.Count == 0))
            {
                bool allAggregate = true;
                for (int i = 0; i < mResultset.Count && allAggregate; i++)
                    allAggregate &= mResultset[i].IsAggregate;

                if (allAggregate)
                    Limit = Skip = 0;
                else
                {
                    if (mGroupBy != null && mGroupBy.Count > 0)
                        AddOrderByExpr(mGroupBy[0].Expression);
                    else
                        AddOrderBy(Entities[0].Table.PrimaryKey);
                }
            }

            if (With != null)
            {
                query = new StringBuilder();
                query.Append(With);
                query.Append(PrepareSelectQueryCore());
            }
            else
            {
                query = PrepareSelectQueryCore();
            }

            if (Limit > 0 || Skip > 0)
            {
                query
                    .Append(" OFFSET ")
                    .Append(Skip)
                    .Append(" ROW ");
            }

            if (Limit > 0)
            {
                query.Append(" FETCH NEXT ")
                    .Append(Limit)
                    .Append(" ROWS ONLY");
            }
            mQuery = query.ToString();
        }
    }
}
