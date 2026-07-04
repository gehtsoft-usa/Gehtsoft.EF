using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlDropQueryBuilder : DropTableBuilder
    {
        public MssqlDropQueryBuilder(SqlDbLanguageSpecifics specifics, TableDescriptor table) : base(specifics, table)
        {
        }

        protected override void AppendDropTable(StringBuilder builder, TableDescriptor descriptor)
        {
            builder.Append($@"IF OBJECT_ID ('{descriptor.Name}', 'U') IS NOT NULL
                                     DROP TABLE {descriptor.Name}");
        }
    }
}
