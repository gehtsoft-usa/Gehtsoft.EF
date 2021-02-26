using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{

    public class MssqlDropViewBuilder : DropViewBuilder
    {
        public MssqlDropViewBuilder(SqlDbLanguageSpecifics specifics, string name) : base(specifics, name)
        {

        }

        public override void PrepareQuery()
        {
            mQuery = $@"IF OBJECT_ID ('{mName}', 'V') IS NOT NULL
                                     DROP VIEW {mName};";
        }
    }
}
