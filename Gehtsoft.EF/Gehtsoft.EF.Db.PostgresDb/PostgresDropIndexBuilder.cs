using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDropIndexBuilder : DropIndexBuilder
    {
        public PostgresDropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name) : base(specifics, table, name)
        {
        }

        public override void PrepareQuery()
        {
            mQuery = $"DROP INDEX IF EXISTS {mTable}_{mName};";
        }
    }
}