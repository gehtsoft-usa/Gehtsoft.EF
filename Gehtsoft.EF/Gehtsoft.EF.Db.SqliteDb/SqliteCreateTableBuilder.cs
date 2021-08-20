using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.SqliteDb
{
    internal class SqliteCreateTableBuilder : CreateTableBuilder
    {
        public SqliteCreateTableBuilder(SqliteDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            DdlBuilder = new SqliteTableDdlBuilder(specifics, table);
        }
    }
}
