using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlDropIndexBuilder : DropIndexBuilder
    {
        public MssqlDropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name) : base(specifics, table, name)
        {
        }

        public override void PrepareQuery()
        {
            mQuery = $@"IF IndexProperty(Object_Id('{mTable}'), '{mTable}_{mName}', 'IndexID') IS NOT NULL
                            DROP INDEX {mTable}_{mName} ON {mTable};";
        }
    }
}
