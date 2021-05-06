using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    public class DropViewBuilder : AQueryBuilder
    {
        protected readonly string mName;
        protected string mQuery;
        override public string Query => mQuery;

        public DropViewBuilder(SqlDbLanguageSpecifics specifics, string name) : base(specifics)
        {
            mSpecifics = specifics;
            mName = name;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            mQuery = $"DROP VIEW IF EXISTS {mName}";
        }
    }
}