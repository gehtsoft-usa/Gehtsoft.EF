using System;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleSelectQueryBuilder : SelectQueryBuilder
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

                mQuery = $"SELECT * FROM (SELECT {fromto1}.*, rownum rnum FROM ({query}) {fromto1} WHERE rownum <= {Skip + (Limit == 0 ? Int32.MaxValue / 2 : Limit )}) {fromto2} WHERE rnum > {Skip}";
            }
            else
                mQuery = query.ToString();
        }
    }
}
