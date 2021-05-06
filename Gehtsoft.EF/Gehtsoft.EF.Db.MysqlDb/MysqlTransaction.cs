using System.Data;
using System.Data.Common;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.MysqlDb
{
    public class MysqlDbTransaction : SqlDbTransaction
    {
        private readonly MysqlDbConnection mConnection;

        internal MysqlDbTransaction(MysqlDbConnection db, DbTransaction transaction) : base(transaction)
        {
            mConnection = db;
        }

        protected override void Dispose(bool disposing)
        {
            mConnection.EndTransaction();
            base.Dispose(disposing);
        }
    }
}
