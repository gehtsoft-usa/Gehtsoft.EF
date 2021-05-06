using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlDropQueryBuilder : DropTableBuilder
    {
        public MssqlDropQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        public override void PrepareQuery()
        {
            mQuery = $@"IF OBJECT_ID ('{mDescriptor.Name}', 'U') IS NOT NULL
                                     DROP TABLE {mDescriptor.Name};";
        }
    }
}
