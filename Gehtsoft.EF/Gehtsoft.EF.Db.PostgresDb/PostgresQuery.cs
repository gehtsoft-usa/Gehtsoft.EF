using System.Data;
using System.Data.Common;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbQuery : SqlDbQuery
    {
        protected internal PostgresDbQuery(PostgresDbConnection connection, DbCommand command, SqlDbLanguageSpecifics specifics) : base(connection, command, specifics)
        {
        }

        protected override void HandleFieldName(ref string name)
        {
            name = name.ToLower();
        }
    }
}
