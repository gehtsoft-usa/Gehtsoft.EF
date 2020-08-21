using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Db.MssqlDb
{
    class MssqlConditionBuilder : ConditionBuilder
    {
        private MssqlSelectQueryBuilder mBuilder;

        public MssqlConditionBuilder(MssqlSelectQueryBuilder builder) : base(builder)
        {
            mBuilder = builder;

        }

        public override string Query(AQueryBuilder builder)
        {
            if (builder is MssqlHierarchicalSelectQueryBuilder)
            {
                mBuilder.With = (builder as MssqlHierarchicalSelectQueryBuilder).With;
                return (builder as MssqlHierarchicalSelectQueryBuilder).Select;
            }
            else if (builder is MssqlSelectQueryBuilder && (builder as MssqlSelectQueryBuilder).With != null)
            {
                mBuilder.With = (builder as MssqlSelectQueryBuilder).With;
                if (builder.Query == null)
                    builder.PrepareQuery();
                string query = builder.Query.Substring(mBuilder.With.Length);
                return query;
            }
            return base.Query(builder);
        }
    }

    class MssqlSelectQueryBuilder : SelectQueryBuilder
    {
        private string mWith = null;

        public string With
        {
            get => mWith;
            internal set => mWith = value;
        }

        internal MssqlSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table)  : base(specifics, table)
        {
            mWhere = new MssqlConditionBuilder(this);
        }

        public override void PrepareQuery()
        {
            StringBuilder query;

            if (mWith != null)
            {
                query = new StringBuilder();
                query.Append(mWith);
                query.Append(PrepareSelectQueryCore());
            }
            else
            {
                query = PrepareSelectQueryCore();
            }

            if (Limit > 0 || Skip > 0)
            {
                query.Append($" OFFSET {Skip} ROWS FETCH NEXT {Limit} ROWS ONLY");
            }
            mQuery = query.ToString();
        }
    }
}
