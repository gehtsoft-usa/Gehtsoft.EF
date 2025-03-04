using System;
using Gehtsoft.EF.Db.SqlDb;
using Microsoft.Data.SqlClient;

namespace Gehtsoft.EF.Db.MssqlDb
{
    public class MssqlTransaction : SqlDbTransaction
    {
        private MssqlDbConnection mDb;
        private readonly string mSavePointName = null;

        internal MssqlTransaction(MssqlDbConnection db, SqlTransaction transaction) : base(transaction)
        {
            DbTransaction = transaction;
            mDb = db;
        }
        internal MssqlTransaction(MssqlDbConnection db, SqlTransaction transaction, string savepointName) : base(transaction)
        {
            DbTransaction = transaction;
            mDb = db;
            mSavePointName = savepointName;
            DbTransaction.Save(mSavePointName);
        }

        public SqlTransaction DbTransaction { get; set; }

        public override void Commit()
        {
            if (mSavePointName == null)
                base.Commit();
        }

        public override void Rollback()
        {
            if (mSavePointName == null)
                base.Rollback();
            else
                DbTransaction.Rollback(mSavePointName);
        }

        protected override void Dispose(bool disposing)
        {
            if (mSavePointName == null)
            {
                if (mDb != null)
                {
                    mDb.EndTransaction(this);
                    mDb = null;
                }
                base.Dispose(disposing);
            }
        }
    }
}
