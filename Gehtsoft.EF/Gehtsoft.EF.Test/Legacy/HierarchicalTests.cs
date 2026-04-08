using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class HierarchicalTests : IClassFixture<HierarchicalTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public HierarchicalTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        private static readonly TableDescriptor gHierarchicalTable = new TableDescriptor
        (
            "hierarchicaltest",
            new TableDescriptor.ColumnInfo[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true},
                new TableDescriptor.ColumnInfo { Name = "parent", DbType = DbType.Int32, Sorted = true, Nullable = true},
                new TableDescriptor.ColumnInfo { Name = "data", DbType = DbType.Int32, Sorted = true},
            }
        );

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void HierarchicalQuery(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            DropTableBuilder dbuilder = connection.GetDropTableBuilder(gHierarchicalTable);
            CreateTableBuilder cbuilder = connection.GetCreateTableBuilder(gHierarchicalTable);
            InsertQueryBuilder ibuilder = connection.GetInsertQueryBuilder(gHierarchicalTable);

            SqlDbQuery query;

            using (query = connection.GetQuery(dbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(cbuilder))
                query.ExecuteNoData();

            using (query = connection.GetQuery(ibuilder))
            {
                //create tree
                // 1
                // + 2
                //   + 4
                //   + 5
                // + 3
                //   + 6
                //     + 8
                //   + 7
                // + 9
                query.BindParam("id", 1);
                query.BindNull("parent", DbType.Int32);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 2);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();
                query.BindParam("id", 3);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 4);
                query.BindParam("parent", 2);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 5);
                query.BindParam("parent", 2);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 6);
                query.BindParam("parent", 3);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 7);
                query.BindParam("parent", 3);
                query.BindParam("data", 1);
                query.ExecuteNoData();

                query.BindParam("id", 8);
                query.BindParam("parent", 6);
                query.BindParam("data", 0);
                query.ExecuteNoData();

                query.BindParam("id", 9);
                query.BindParam("parent", 1);
                query.BindParam("data", 0);
                query.ExecuteNoData();
            }

            //read whole tree
            HierarchicalSelectQueryBuilder hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], null);
            using (query = connection.GetQuery(hbuilder))
            {
                query.ExecuteReader();
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 1:
                            query.GetValue<int>("level").Should().Be(1);
                            query.IsNull("parent").Should().BeTrue();
                            break;
                        case 2:
                            query.GetValue<int>("level").Should().Be(2);
                            query.GetValue<int>("parent").Should().Be(1);
                            break;
                        case 3:
                            query.GetValue<int>("level").Should().Be(2);
                            query.GetValue<int>("parent").Should().Be(1);
                            break;
                        case 4:
                            query.GetValue<int>("level").Should().Be(3);
                            query.GetValue<int>("parent").Should().Be(2);
                            break;
                        case 5:
                            query.GetValue<int>("level").Should().Be(3);
                            query.GetValue<int>("parent").Should().Be(2);
                            break;
                        case 6:
                            query.GetValue<int>("level").Should().Be(3);
                            query.GetValue<int>("parent").Should().Be(3);
                            break;
                        case 7:
                            query.GetValue<int>("level").Should().Be(3);
                            query.GetValue<int>("parent").Should().Be(3);
                            break;
                        case 8:
                            query.GetValue<int>("level").Should().Be(4);
                            query.GetValue<int>("parent").Should().Be(6);
                            break;
                        case 9:
                            query.GetValue<int>("level").Should().Be(2);
                            query.GetValue<int>("parent").Should().Be(1);
                            break;
                        default:
                            Assert.Fail("Unknown ID");
                            break;
                    }
                }
                rc.Should().Be(9);
            }

            hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], "root");
            using (query = connection.GetQuery(hbuilder))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 3:
                            query.GetValue<int>("level").Should().Be(1);
                            break;
                        case 6:
                            query.GetValue<int>("level").Should().Be(2);
                            break;
                        case 7:
                            query.GetValue<int>("level").Should().Be(2);
                            break;
                        case 8:
                            query.GetValue<int>("level").Should().Be(3);
                            break;
                        default:
                            Assert.Fail("Unknown ID");
                            break;
                    }
                }
                rc.Should().Be(4);
            }

            hbuilder = connection.GetHierarchicalSelectQueryBuilder(gHierarchicalTable, gHierarchicalTable["parent"], "root");
            hbuilder.IdOnlyMode = true;
            using (query = connection.GetQuery(hbuilder))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                query.FieldCount.Should().Be(1);
                int rc = 0;
                while (query.ReadNext())
                {
                    rc++;
                    switch (query.GetValue<int>("id"))
                    {
                        case 3:
                            break;
                        case 6:
                            break;
                        case 7:
                            break;
                        case 8:
                            break;
                        default:
                            Assert.Fail("Unknown ID");
                            break;
                    }
                }
                rc.Should().Be(4);
            }
        }
    }
}
