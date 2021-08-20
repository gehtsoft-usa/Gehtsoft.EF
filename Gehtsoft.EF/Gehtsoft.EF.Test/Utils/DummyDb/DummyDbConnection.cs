using System;
using System.Data;
using System.Data.Common;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbConnection : DbConnection
    {
        public override string ConnectionString { get; set; } = "dummyConnectionString";

        public override string Database => "dummydb";

        public override string DataSource => "dummySource";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName)
        {
#pragma warning disable RCS1079 // Throwing of new NotImplementedException.
            throw new NotImplementedException();
#pragma warning restore RCS1079 // Throwing of new NotImplementedException.
        }

        public override void Close()
        {
        }

        public override void Open()
        {
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            return new DummyDbTransaction(isolationLevel, this);
        }

        protected override DbCommand CreateDbCommand() => new DummyDbCommand() { Connection = this };
    }
}
