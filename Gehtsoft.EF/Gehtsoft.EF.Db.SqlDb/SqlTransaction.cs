using System;
using System.Data;
using System.Data.Common;

namespace Gehtsoft.EF.Db.SqlDb
{
    public class SqlDbTransaction : IDisposable
    {
        private DbTransaction mTransaction;

        protected internal SqlDbTransaction(DbTransaction transaction)
        {
            mTransaction = transaction;
        }

        ~SqlDbTransaction()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mTransaction != null)
            {
                mTransaction.Dispose();
                mTransaction = null;
            }
        }

        public virtual void Rollback()
        {
            mTransaction?.Rollback();
        }

        public virtual void Commit()
        {
            mTransaction?.Commit();
        }
    }
}
