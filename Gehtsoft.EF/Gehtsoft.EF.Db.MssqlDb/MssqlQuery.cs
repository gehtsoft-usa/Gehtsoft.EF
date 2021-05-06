using System.Data.SqlClient;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlQuery : SqlDbQuery
    {
        private readonly SqlCommand mSqlCommand;

        protected internal MssqlQuery(MssqlDbConnection connection, SqlCommand command, SqlDbLanguageSpecifics specifics) : base(connection, command, specifics)
        {
            mSqlCommand = command;
        }

        public void SetTransaction(MssqlTransaction transaction)
        {
            mSqlCommand.Transaction = transaction.DbTransaction;
        }
    }
}
