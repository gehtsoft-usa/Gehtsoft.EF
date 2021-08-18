using System;
using System.Data;
using System.Data.Common;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;

#pragma warning disable RCS1079 // Throwing of new NotImplementedException.

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummyDbCommand : DbCommand
    {
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection { get; } = new DummyDbParameterCollection();

        protected override DbTransaction DbTransaction { get; set; }

        public bool CancelCalled { get; set; }

        public override void Cancel() => CancelCalled = true;

        public int ExecuteNonQueryReturnValue { get; set; }
        public bool ExecuteNonQueryCalled { get; set; }
        public bool ExecuteNonQueryAsyncCalled { get; set; }

        public override int ExecuteNonQuery()
        {
            ExecuteNonQueryCalled = true;
            return ExecuteNonQueryReturnValue;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            ExecuteNonQueryAsyncCalled = true;
            return Task.FromResult(ExecuteNonQueryReturnValue);
        }

        public override object ExecuteScalar() => throw new NotImplementedException();

        public bool PrepareCalled { get; set; }

        public override void Prepare()
        {
            PrepareCalled = true;
        }

        protected override DbParameter CreateDbParameter() => new DummyDbParameter();

        public DbDataReader ReturnReader { get; set; }
        public bool ExecuteDbReaderCalled { get; set; }
        public bool ExecuteDbReaderAsyncCalled { get; set; }
        public CommandBehavior? ExecuteDbReaderCommandParameter { get; set; }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            ExecuteDbReaderCommandParameter = behavior;
            ExecuteDbReaderCalled = true;
            return ReturnReader;
        }

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            ExecuteDbReaderCommandParameter = behavior;
            ExecuteDbReaderAsyncCalled = true;
            return Task.FromResult(ReturnReader);
        }

        public bool DisposedCalled { get; set; }

        protected override void Dispose(bool disposing)
        {
            DisposedCalled = true;
            base.Dispose(disposing);
        }
    }
}
