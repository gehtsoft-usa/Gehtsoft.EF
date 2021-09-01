using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb
{
    /// <summary>
    /// The database transaction.
    ///
    /// Use <see cref="SqlDbConnection.BeginTransaction()"/> to create a transaction.
    ///
    /// The transaction is considered ended when the object is disposed. The transaction
    /// should be committed explicitly, otherwise the driver will roll it back at
    /// the disposal.
    /// </summary>
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

        /// <summary>
        /// Disposes the transaction object.
        /// </summary>
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

        /// <summary>
        /// Rolls the transaction back.
        /// </summary>
        public virtual void Rollback()
        {
            mTransaction?.Rollback();
        }

        /// <summary>
        /// Rolls the transaction back asynchronously.
        /// </summary>
        /// <returns></returns>
        public virtual Task RollbackAsync() => Task.Run(() => Rollback());

        /// <summary>
        /// Commits the transaction.
        /// </summary>
        public virtual void Commit()
        {
            mTransaction?.Commit();
        }

        /// <summary>
        /// Commits the transaction asynchronously.
        /// </summary>
        /// <returns></returns>
        public virtual Task CommitAsync() => Task.Run(() => Commit());
    }
}
