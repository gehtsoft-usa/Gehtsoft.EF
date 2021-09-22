using System.Data;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class DefaultValueInTableColumnTest : IClassFixture<DefaultValueInTableColumnTest.Fixture>
    {
        #region fixture
        public class Fixture : SqlConnectionFixtureBase
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
        #endregion

        private readonly Fixture mFixture;
        public DefaultValueInTableColumnTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(SqlConnectionSources.SqlConnectionNames), "", MemberType = typeof(SqlConnectionSources))]
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
