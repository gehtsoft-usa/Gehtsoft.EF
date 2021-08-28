using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Utils;

namespace Gehtsoft.EF.Db.SqlDb.QueryBuilder
{
    /// <summary>
    /// The query builder for `CREATE VIEW` command.
    ///
    /// Use <see cref="SqlDbConnection.GetCreateViewBuilder(string, SelectQueryBuilder)"/> to create an instance of this object.
    /// </summary>
    public class CreateViewBuilder : AQueryBuilder
    {
        protected readonly string mName;
        protected readonly SelectQueryBuilder mSelectQuery;
        protected string mQuery;

        [DocgenIgnore]
        override public string Query => mQuery;

        [DocgenIgnore]
        internal protected CreateViewBuilder(SqlDbLanguageSpecifics specifics, string name, SelectQueryBuilder selectQuery) : base(specifics)
        {
            mSpecifics = specifics;
            mName = name;
            mSelectQuery = selectQuery;
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            mSelectQuery.PrepareQuery();

            mQuery = $"CREATE VIEW {mName} AS {mSelectQuery.Query}";
        }
    }
}