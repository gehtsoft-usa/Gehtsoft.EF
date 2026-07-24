using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    /// <summary>
    /// The <c>DROP INDEX</c> builder for MySQL 8, which has no <c>DROP INDEX IF EXISTS</c>. To stay idempotent
    /// (dropping an absent index must be a no-op, like every other dialect), it guards the drop with an
    /// <c>information_schema</c> existence check driving a prepared statement - the MySQL analogue of SQL
    /// Server's <c>IF IndexProperty(...) IS NOT NULL</c> and Oracle's <c>EXCEPTION WHEN OTHERS</c>.
    /// </summary>
    internal sealed class MySql8DropIndexBuilder : DropIndexBuilder
    {
        public MySql8DropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name)
            : base(specifics, table, name)
        {
        }

        public override void PrepareQuery()
        {
            if (mQuery != null)
                return;

            string index = mSpecifics.IndexName(mTable, mName);
            mQuery =
                $"SET @ef_drop_idx := IF((SELECT COUNT(1) FROM information_schema.statistics " +
                $"WHERE table_schema = DATABASE() AND table_name = '{mTable}' AND index_name = '{index}') > 0, " +
                $"'DROP INDEX `{index}` ON `{mTable}`', 'DO 0');\r\n" +
                "PREPARE ef_drop_idx_stmt FROM @ef_drop_idx;\r\n" +
                "EXECUTE ef_drop_idx_stmt;\r\n" +
                "DEALLOCATE PREPARE ef_drop_idx_stmt;";
        }
    }
}
