using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleCreateTableBuilder : CreateTableBuilder
    {
        public OracleCreateTableBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            DdlBuilder = new OracleTableDdlBuilder(specifics, table);
        }
    }
}
