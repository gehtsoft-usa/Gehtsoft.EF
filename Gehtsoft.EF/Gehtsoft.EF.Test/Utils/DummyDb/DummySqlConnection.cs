using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;

namespace Gehtsoft.EF.Test.Utils.DummyDb
{
    internal class DummySqlConnection : SqlDbConnection
    {
        public override string ConnectionType => "dummy";

        public override HierarchicalSelectQueryBuilder GetHierarchicalSelectQueryBuilder(TableDescriptor descriptor, TableDescriptor.ColumnInfo parentReferenceColumn, string rootParameter = null) => throw new NotFiniteNumberException();

        public DummyDbSpecifics DummyDbSpecifics { get; } = new DummyDbSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics() => DummyDbSpecifics;

        protected override Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token) => throw new NotFiniteNumberException();

        public DummySqlConnection() : this(new DummyDbConnection())
        {
        }

        public DummySqlConnection(DbConnection connection) : base(connection)
        {
        }

        private readonly Dictionary<Tuple<string, string>, bool> mObjectExist = new Dictionary<Tuple<string, string>, bool>();

        public void SetObjectExist(string name, string type, bool value) => mObjectExist[new Tuple<string, string>(name, type)] = value;

        protected override ValueTask<bool> DoesObjectExistCore(string tableName, string objectName, string objectType, bool executeAsync)
        {
            if (objectName != null)
                tableName += "_" + objectName;
            if (mObjectExist.TryGetValue(new Tuple<string, string>(tableName, objectType), out var v))
                return ValueTask.FromResult(v);
            return ValueTask.FromResult(v);
        }
    }
}
