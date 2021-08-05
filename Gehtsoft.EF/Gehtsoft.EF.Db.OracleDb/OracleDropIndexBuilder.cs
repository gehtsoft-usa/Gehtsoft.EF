using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.OracleDb
{
    internal class OracleDropIndexBuilder : DropIndexBuilder
    {
        public OracleDropIndexBuilder(SqlDbLanguageSpecifics specifics, string table, string name) : base(specifics, table, name)
        {
        }

        public override void PrepareQuery()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append(mSpecifics.PreBlock);
            builder.Append(mSpecifics.PreQueryInBlock);
            builder.Append("DROP INDEX ")
                .Append(mTable).Append('_').Append(mName);
            builder.Append(mSpecifics.PostQueryInBlock);
            builder.Append("EXCEPTION\r\n");
            builder.Append("  WHEN OTHERS THEN NULL;\r\n");
            builder.Append(mSpecifics.PostBlock);

            mQuery = builder.ToString();
        }
    }
}
