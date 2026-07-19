using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The <c>DROP INDEX</c> builder for MariaDB, which supports the native idempotent
    /// <c>DROP INDEX IF EXISTS &lt;name&gt; ON &lt;table&gt;</c> form (since 10.1.4).
    /// </summary>
    internal sealed class MariaDbDropIndexBuilder : DropIndexBuilder
    {
        public MariaDbDropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name)
            : base(specifics, table, name)
        {
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;
            mQuery = $"DROP INDEX IF EXISTS {mSpecifics.IndexName(mTable, mName)} ON {mTable};";
        }
    }
}
