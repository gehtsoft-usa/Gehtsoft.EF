using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using System.Text;

namespace Gehtsoft.EF.Db.PostgresDb
{
    internal class PostgresTableDdlBuilder : TableDdlBuilder
    {
        public PostgresTableDdlBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor tableDescriptor) : base(specifics, tableDescriptor)
        {
        }
        public override void HandleAutoincrement(StringBuilder builder, TableDescriptor.ColumnInfo ci)
        {
            //prevent for handling autoincremenet flag
        }
    }
}

