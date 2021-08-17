using System;
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

        private readonly Sql92LanguageSpecifics mLanguageSpecifics = new Sql92LanguageSpecifics();

        public override SqlDbLanguageSpecifics GetLanguageSpecifics() => mLanguageSpecifics;

        protected override Task<TableDescriptor[]> SchemaCore(bool sync, CancellationToken? token) => throw new NotFiniteNumberException();

        public DummySqlConnection(DbConnection connection) : base(connection)
        {
        }
    }
}
