using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class QueryAsyncMethodsTest : IClassFixture<QueryAsyncMethodsTest.Fixture>
    {
        #region fixture
        public class Fixture : ConnectionFixtureBase
        {
            public static bool DropAtEnd { get; set; } = false;

            public const string TableName = "async_test";

            public TableDescriptor Table { get; }

            public Fixture()
            {
                Table = new TableDescriptor()
                {
                    Name = TableName,
                };

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "table_id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                Table.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);
                base.ConfigureConnection(connection);
            }

            protected override void TearDownConnection(SqlDbConnection connection)
            {
                if (DropAtEnd)
                    Drop(connection);

                base.TearDownConnection(connection);
            }

            private void Drop(SqlDbConnection connection)
            {
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(Table)))
                    query.ExecuteNoData();
            }
        }
        #endregion
        
        private readonly Fixture mFixture;

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.ConnectionNames(flags);

        public QueryAsyncMethodsTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [TestOrder(1)]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task T1_CreateTable(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            (await connection.DoesObjectExistAsync(mFixture.Table.Name, null, "table")).Should().BeFalse();

            using (var query = connection.GetQuery(connection.GetCreateTableBuilder(mFixture.Table)))
                await query.ExecuteNoDataAsync();

            (await connection.DoesObjectExistAsync(mFixture.Table.Name, null, "table")).Should().BeTrue();
            connection.DoesObjectExist(mFixture.Table.Name, "name", "index");
        }

        [Theory]
        [TestOrder(2)]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task T2_Insert(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await T1_CreateTable(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var t = connection.BeginTransaction())
            {
                using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(mFixture.Table)))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        query.BindParam("name", $"name {i}");

                        if (connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                            query.BindOutput("table_id", DbType.Int32);

                        (await query.ExecuteNoDataAsync()).Should().Be(1);
                    }
                }
                await t.CommitAsync();
            }
        }

        [Theory]
        [TestOrder(3)]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task T3_Read(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await T2_Insert(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var builder = connection.GetSelectQueryBuilder(mFixture.Table);
            builder.AddToResultset(AggFn.Count);
            using (var query = connection.GetQuery(builder))
            {
                await query.ExecuteReaderAsync();
                (await query.ReadNextAsync()).Should().BeTrue();
                query.GetValue<int>(0).Should().Be(10);
                (await query.ReadNextAsync()).Should().BeFalse();
            }
        }

        [Theory]
        [TestOrder(4)]
        [MemberData(nameof(ConnectionNames), "")]
        public async Task T4_DropTable(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                await T1_CreateTable(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            using (var query = connection.GetQuery(connection.GetDropTableBuilder(mFixture.Table)))
                await query.ExecuteNoDataAsync();

            (await connection.DoesObjectExistAsync(mFixture.Table.Name, null, "table")).Should().BeFalse();
        }
    }
}
