using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    class OracleSelectQueryBuilder : SelectQueryBuilder
    {
        public OracleSelectQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
            
        }

        public override void PrepareQuery()
        {
            StringBuilder query = PrepareSelectQueryCore();
            if (Limit > 0 || Skip > 0)
            {
                string fromto1 = NextAlias;
                string fromto2 = NextAlias;

                mQuery = $"SELECT * FROM (SELECT {fromto1}.*, rownum rnum FROM ({query.ToString()}) {fromto1} WHERE rownum <= {Skip + Limit}) {fromto2} WHERE rnum > {Skip}";
            }
            else
                mQuery = query.ToString();
        }
    }
}
