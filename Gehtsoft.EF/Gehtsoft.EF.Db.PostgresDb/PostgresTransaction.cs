using System.Data;
using System.Data.Common;
using Gehtsoft.EF.Db.SqlDb;

namespace Gehtsoft.EF.Db.PostgresDb
{
    public class PostgresDbTransaction : SqlDbTransaction
    {
        private static int gSavepointId = 1;
        private bool mFinalized = false;

        private PostgresDbConnection mConnection;
        internal bool IsSavePoint { get; private set; }
        internal string SavePoint { get; private set; }
        

        internal PostgresDbTransaction(PostgresDbConnection db, DbTransaction transaction) : base(transaction)
        {
            IsSavePoint = false;
            SavePoint = null;
            mConnection = db;
        }
        internal PostgresDbTransaction(PostgresDbConnection db) : base(null)
        {
            IsSavePoint = true;
            SavePoint = $"sp{gSavepointId++}";
            mConnection = db;
            using (SqlDbQuery query = mConnection.GetQuery($"SAVEPOINT {SavePoint}"))
                query.ExecuteNoData();
        }

        public override void Commit()
        {
            if (IsSavePoint)
            {
                using (SqlDbQuery query = mConnection.GetQuery($"RELEASE SAVEPOINT {SavePoint}"))
                    query.ExecuteNoData();
                mFinalized = true;
            }
            else
                base.Commit();
        }
        public override void Rollback()
        {
            if (IsSavePoint)
            {
                using (SqlDbQuery query = mConnection.GetQuery($"ROLLBACK TO SAVEPOINT {SavePoint}"))
                    query.ExecuteNoData();
                mFinalized = true;
            }
            else
                base.Rollback();
        }

        protected override void Dispose(bool disposing)
        {
            if (IsSavePoint && !mFinalized)
                Rollback();

            mConnection.EndTransaction(this);
            base.Dispose(disposing);
        }
    }
}
