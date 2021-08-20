using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MysqlDb
{
    internal class MysqlCreateTableBuilder : CreateTableBuilder
    {
        public MysqlCreateTableBuilder(MysqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            DdlBuilder = new MysqlTableDdlBuilder(specifics, table);
        }
    }
}
