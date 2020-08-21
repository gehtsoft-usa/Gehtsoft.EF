using Gehtsoft.EF.Db.SqlDb;
using MySqlConnector;

namespace Gehtsoft.EF.Db.MysqlDb
{
    public class MysqlDbQuery : SqlDbQuery
    {
        private MySqlCommand mSqlCommand;

        protected internal MysqlDbQuery(MysqlDbConnection connection, MySqlCommand command, SqlDbLanguageSpecifics specifics) : base(connection, command, specifics)
        {
            mSqlCommand = command;
        }
    }
}
