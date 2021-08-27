using System.Data;
using System.Data.Common;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel { get; }

        protected override DbConnection DbConnection { get; }

        public DummyDbTransaction(IsolationLevel level, DbConnection connection)
        {
            IsolationLevel = level;
            DbConnection = connection;
        }

        public bool CommitCalled { get; set; }
        public bool RollbackCalled { get; set; }

        public override void Commit()
        {
            CommitCalled = true;
        }

        public override void Rollback()
        {
            RollbackCalled = true;
        }
    }
}
