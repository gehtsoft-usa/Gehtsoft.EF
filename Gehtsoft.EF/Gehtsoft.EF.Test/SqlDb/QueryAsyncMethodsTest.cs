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

        private readonly Fixture mFixture;

        public QueryAsyncMethodsTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [TestOrder(1)]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        public async Task CreateTable(string connectionName)
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
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        public async Task Insert(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            if (!connection.DoesObjectExist(mFixture.Table.Name, null, "table"))
                await CreateTable(connectionName);

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
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        public async Task Read(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            if (!connection.DoesObjectExist(mFixture.Table.Name, null, "table"))
                await Insert(connectionName);

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
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        public async Task DropTable(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            if (!connection.DoesObjectExist(mFixture.Table.Name, null, "table"))
                await CreateTable(connectionName);

            using (var query = connection.GetQuery(connection.GetDropTableBuilder(mFixture.Table)))
                await query.ExecuteNoDataAsync();

            (await connection.DoesObjectExistAsync(mFixture.Table.Name, null, "table")).Should().BeFalse();
        }
    }

    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class DefaultValueInTableColumnTest : IClassFixture<DefaultValueInTableColumnTest.Fixture>
    {
        public class Fixture : ConnectionFixtureBase
        {
            public static bool DropAtEnd { get; set; } = false;

            public const string TableName = "defvaluetest_test";

            public TableDescriptor TableV1 { get; }
            public TableDescriptor TableV2 { get; }

            public Fixture()
            {
                TableV1 = new TableDescriptor()
                {
                    Name = TableName,
                };

                TableV1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "table_id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                TableV1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                TableV1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dv1",
                    DbType = DbType.String,
                    Size = 32,
                    DefaultValue = "v1"
                });

                TableV2 = new TableDescriptor()
                {
                    Name = TableName,
                };

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "table_id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dv1",
                    DbType = DbType.String,
                    Size = 32,
                    DefaultValue = "default1"
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dv2",
                    DbType = DbType.String,
                    Size = 32,
                    DefaultValue = "default2"
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
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(TableV1)))
                    query.ExecuteNoData();
            }
        }

        private readonly Fixture mFixture;

        public DefaultValueInTableColumnTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.ConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
        public void DoTest(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist(mFixture.TableV1.Name, null, "table").Should().BeFalse();

            using (var query = connection.GetQuery(connection.GetCreateTableBuilder(mFixture.TableV1)))
                query.ExecuteNoData();

            var insertBuilder = connection.GetInsertQueryBuilder(mFixture.TableV1);
            insertBuilder.IncludeOnly(new string[] { "table_id", "name" });
            using (var query = connection.GetQuery(insertBuilder))
            {
                query.BindParam("name", "name1");
                if (connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("table_id", DbType.Int32);
                query.ExecuteNoData();
            }

            insertBuilder = connection.GetInsertQueryBuilder(mFixture.TableV1);
            using (var query = connection.GetQuery(insertBuilder))
            {
                query.BindParam("name", "name2");
                query.BindParam("dv1", "value1_2");
                if (connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("table_id", DbType.Int32);
                query.ExecuteNoData();
            }

            //now records should be:
            //name1, default1
            //name2, value_12

            var alter = connection.GetAlterTableQueryBuilder();
            alter.SetTable(mFixture.TableV2, new[] { mFixture.TableV2["dv2"] }, null);
            foreach (var cmd in alter.GetQueries())
                using (var query = connection.GetQuery(cmd, true))
                    query.ExecuteNoData();

            //now records should be:
            //name1, default1, default2
            //name2, value_12, default2
            insertBuilder = connection.GetInsertQueryBuilder(mFixture.TableV1);
            insertBuilder.IncludeOnly(new string[] { "table_id", "name" });
            using (var query = connection.GetQuery(insertBuilder))
            {
                query.BindParam("name", "name3");
                if (connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("table_id", DbType.Int32);
                query.ExecuteNoData();
            }

            insertBuilder = connection.GetInsertQueryBuilder(mFixture.TableV1);
            using (var query = connection.GetQuery(insertBuilder))
            {
                query.BindParam("name", "name4");
                query.BindParam("dv1", "value1_4");
                query.BindParam("dv2", "value2_4");
                if (connection.GetLanguageSpecifics().AutoincrementReturnedAs == SqlDbLanguageSpecifics.AutoincrementReturnStyle.Parameter)
                    query.BindOutput("table_id", DbType.Int32);
                query.ExecuteNoData();
            }

            //now records should be:
            //name1, default1, default2
            //name2, value1_2, default2
            //name3, default1, default2
            //name4, value1_4, value2_4

            var select = connection.GetSelectQueryBuilder(mFixture.TableV2);
            select.AddToResultset(mFixture.TableV2);
            select.AddOrderBy(mFixture.TableV2["name"]);

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();

                query.ReadNext().Should().BeTrue();
                query.GetValue<string>("name").Should().Be("name1");

                query.ReadNext().Should().BeTrue();
                query.GetValue<string>("name").Should().Be("name2");

                query.ReadNext().Should().BeTrue();
                query.GetValue<string>("name").Should().Be("name3");

                query.ReadNext().Should().BeTrue();
                query.GetValue<string>("name").Should().Be("name4");

                query.ReadNext().Should().BeFalse();
            }
            connection.DoesObjectExist(mFixture.TableV1.Name, "name", "index");
        }
    }
}
