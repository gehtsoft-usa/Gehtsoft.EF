using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gehtsoft.EF.Db.SqlDb.EntityQueries
{
    internal class ContextTransaction : IEntityContextTransaction
    {
        private readonly SqlDbTransaction mTransaction;

        public ContextTransaction(SqlDbTransaction transaction)
        {
            mTransaction = transaction;
        }

        public void Commit()
        {
            mTransaction.Commit();
        }

        public void Dispose()
        {
            mTransaction.Dispose();
        }

        public void Rollback()
        {
            mTransaction.Rollback();
        }
    }
}