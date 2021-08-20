using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresCreateTableBuilder : CreateTableBuilder
    {
        public PostgresCreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
            DdlBuilder = new PostgresTableDdlBuilder(specifics, tableDescriptor);
        }
    }
}

