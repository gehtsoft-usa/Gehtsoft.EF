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
            if (queryBuilder is MssqlHierarchicalSelectQueryBuilder)
            {
                mBuilder.With = (queryBuilder as MssqlHierarchicalSelectQueryBuilder).With;
                return (queryBuilder as MssqlHierarchicalSelectQueryBuilder).Select;
            }
            else if (queryBuilder is MssqlSelectQueryBuilder && (queryBuilder as MssqlSelectQueryBuilder).With != null)
            {
                mBuilder.With = (queryBuilder as MssqlSelectQueryBuilder).With;
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
                    .Append(" ROWS FETCH NEXT ")
                    .Append(Limit)
                    .Append(" ROWS ONLY");
            }
            mQuery = query.ToString();
        }
    }
}
