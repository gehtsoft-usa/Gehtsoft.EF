using System;
using System.Collections.Generic;
using System.Text;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;

namespace Gehtsoft.EF.Serialization.IO.Db
{
    public class DbEntityWriter : IEntityWriter, IDisposable
    {
        private SqlDbConnection mConnection;
        private SqlDbTransaction mTransaction;
        private bool mTransactEachType;

        public DbEntityWriter(SqlDbConnection connection, bool? transactEachType = null)
        {
            mConnection = connection;
            mTransaction = null;
            mTransactEachType = transactEachType ?? (mConnection.GetLanguageSpecifics().SupportsTransactions == SqlDbLanguageSpecifics.TransactionSupport.Nested);
        }

        public void Dispose()
        {
            if (mTransaction != null)
            {
                mTransaction.Commit();
                mTransaction = null;
            }

            if (mInsertQuery != null)
            {
                mInsertQuery.Dispose();
                mInsertQuery = null;
            }
        }

        private ModifyEntityQuery mInsertQuery;

        public void Start(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (mInsertQuery != null)
            {
                mInsertQuery.Dispose();
                mInsertQuery = null;
            }

            if (mTransaction != null)
            {
                mTransaction.Commit();
                mTransaction.Dispose();
                mTransaction = null;
            }

            if (mTransactEachType)
                mTransaction = mConnection.BeginTransaction();

            mInsertQuery = mConnection.GetInsertEntityQuery(type, true);

        }

        public void Write(object entity)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            
            mInsertQuery.Execute(entity);
        }
    }
}
