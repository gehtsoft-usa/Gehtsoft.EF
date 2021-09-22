using System.Collections.Generic;
using System.Data;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    public class HierarchicalSelectTest : IClassFixture<HierarchicalSelectTest.Fixture>
    {
        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        #region fixture

        public class TreeComplete
        {
            public int Id { get; set; }
            public int? Parent { get; set; }
            public int Level { get; set; }
            public string Name { get; set; }
        }

        public class TreeIdOnly
        {
            public int Id { get; set; }
        }

        public class Fixture : SqlConnectionFixtureBase
        {
            public TableDescriptor HierachicalTable { get; }

            public bool DropAtEnd { get; } = false;

            public Fixture()
            {
                HierachicalTable = new TableDescriptor()
                {
                    Name = "hier_test"
                };

                HierachicalTable.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
                    DbType = DbType.Int32,
                    PrimaryKey = true,
                });

                HierachicalTable.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "parent",
                    DbType = DbType.Int32,
                    Nullable = true,
                    ForeignTable = HierachicalTable,
                });

                HierachicalTable.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                });
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(HierachicalTable)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetInsertQueryBuilder(HierachicalTable, true)))
                {
                    //create tree
                    // 1
                    // + 2
                    //   + 4
                    //   + 5
                    // + 3
                    //   + 6
                    //     + 7
                    //     + 8
                    //   + 9
                    // + 10
                    query.BindParam<int>("id", 1);
                    query.BindParam<int?>("parent", null);
                    query.BindParam<string>("name", "1");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 2);
                    query.BindParam<int?>("parent", 1);
                    query.BindParam<string>("name", "1=>2");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 4);
                    query.BindParam<int?>("parent", 2);
                    query.BindParam<string>("name", "1=>2=>4");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 5);
                    query.BindParam<int?>("parent", 2);
                    query.BindParam<string>("name", "1=>2=>5");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 3);
                    query.BindParam<int?>("parent", 1);
                    query.BindParam<string>("name", "1=>3");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 6);
                    query.BindParam<int?>("parent", 3);
                    query.BindParam<string>("name", "1=>3=>6");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 7);
                    query.BindParam<int?>("parent", 6);
                    query.BindParam<string>("name", "1=>3=>6=>7");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 8);
                    query.BindParam<int?>("parent", 6);
                    query.BindParam<string>("name", "1=>3=>6=>8");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 9);
                    query.BindParam<int?>("parent", 3);
                    query.BindParam<string>("name", "1=>3=>9");
                    query.ExecuteNoData();

                    query.BindParam<int>("id", 10);
                    query.BindParam<int?>("parent", 1);
                    query.BindParam<string>("name", "1=>9");
                    query.ExecuteNoData();
                }

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
                using (var query = connection.GetQuery(connection.GetDropTableBuilder(HierachicalTable)))
                    query.ExecuteNoData();
            }
        }
        #endregion

        private readonly Fixture mFixture;

        public HierarchicalSelectTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void SelectWholeTree(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.GetLanguageSpecifics().HierarchicalQuerySupported.Should().BeTrue();

            var select = connection.GetHierarchicalSelectQueryBuilder(mFixture.HierachicalTable, mFixture.HierachicalTable["parent"]);
            select.IdOnlyMode = false;

            var binder = new SelectQueryResultBinder(typeof(TreeComplete));
            binder.AutoBindType();

            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                var tree = binder.ReadAll<List<TreeComplete>, TreeComplete>(query);

                tree.Should().HaveCount(10);
                tree.Should().HaveElementMatching(t => t.Id == 1 && t.Parent == null && t.Level == 1);
                tree.Should().HaveElementMatching(t => t.Id == 2 && t.Parent == 1 && t.Level == 2);
                tree.Should().HaveElementMatching(t => t.Id == 4 && t.Parent == 2 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 5 && t.Parent == 2 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 3 && t.Parent == 1 && t.Level == 2);
                tree.Should().HaveElementMatching(t => t.Id == 6 && t.Parent == 3 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 7 && t.Parent == 6 && t.Level == 4);
                tree.Should().HaveElementMatching(t => t.Id == 8 && t.Parent == 6 && t.Level == 4);
                tree.Should().HaveElementMatching(t => t.Id == 9 && t.Parent == 3 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 10 && t.Parent == 1 && t.Level == 2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void SelectWholeTree_WhenRootIsRoot(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.GetLanguageSpecifics().HierarchicalQuerySupported.Should().BeTrue();

            int root;
            var selectRoot = connection.GetSelectQueryBuilder(mFixture.HierachicalTable);
            selectRoot.AddToResultset(mFixture.HierachicalTable["id"]);
            selectRoot.Where.Property(mFixture.HierachicalTable["parent"]).IsNull();

            using (var query = connection.GetQuery(selectRoot))
            {
                query.ExecuteReader();
                query.ReadNext();
                root = query.GetValue<int>(0);
            }

            root.Should().Be(1);

            var select = connection.GetHierarchicalSelectQueryBuilder(mFixture.HierachicalTable, mFixture.HierachicalTable["parent"], "root");
            select.IdOnlyMode = false;

            var binder = new SelectQueryResultBinder(typeof(TreeComplete));
            binder.AutoBindType();

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("root", root);
                query.ExecuteReader();
                var tree = binder.ReadAll<List<TreeComplete>, TreeComplete>(query);

                tree.Should().HaveCount(10);
                tree.Should().HaveElementMatching(t => t.Id == 1 && t.Parent == null && t.Level == 1);
                tree.Should().HaveElementMatching(t => t.Id == 2 && t.Parent == 1 && t.Level == 2);
                tree.Should().HaveElementMatching(t => t.Id == 4 && t.Parent == 2 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 5 && t.Parent == 2 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 3 && t.Parent == 1 && t.Level == 2);
                tree.Should().HaveElementMatching(t => t.Id == 6 && t.Parent == 3 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 7 && t.Parent == 6 && t.Level == 4);
                tree.Should().HaveElementMatching(t => t.Id == 8 && t.Parent == 6 && t.Level == 4);
                tree.Should().HaveElementMatching(t => t.Id == 9 && t.Parent == 3 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 10 && t.Parent == 1 && t.Level == 2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void SelectSubTree(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.GetLanguageSpecifics().HierarchicalQuerySupported.Should().BeTrue();

            var select = connection.GetHierarchicalSelectQueryBuilder(mFixture.HierachicalTable, mFixture.HierachicalTable["parent"], "root");
            select.IdOnlyMode = false;

            var binder = new SelectQueryResultBinder(typeof(TreeComplete));
            binder.AutoBindType();

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                var tree = binder.ReadAll<List<TreeComplete>, TreeComplete>(query);

                tree.Should().HaveCount(5);
                tree.Should().HaveElementMatching(t => t.Id == 3 && t.Parent == 1 && t.Level == 1);
                tree.Should().HaveElementMatching(t => t.Id == 6 && t.Parent == 3 && t.Level == 2);
                tree.Should().HaveElementMatching(t => t.Id == 7 && t.Parent == 6 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 8 && t.Parent == 6 && t.Level == 3);
                tree.Should().HaveElementMatching(t => t.Id == 9 && t.Parent == 3 && t.Level == 2);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void SelectSubTree_IdOnly(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.GetLanguageSpecifics().HierarchicalQuerySupported.Should().BeTrue();

            var select = connection.GetHierarchicalSelectQueryBuilder(mFixture.HierachicalTable, mFixture.HierachicalTable["parent"], "root");
            select.IdOnlyMode = true;

            var binder = new SelectQueryResultBinder(typeof(TreeComplete));
            binder.AutoBindType();

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                var tree = binder.ReadAll<List<TreeComplete>, TreeComplete>(query);

                tree.Should().HaveCount(5);
                tree.Should().HaveElementMatching(t => t.Id == 3);
                tree.Should().HaveElementMatching(t => t.Id == 6);
                tree.Should().HaveElementMatching(t => t.Id == 7);
                tree.Should().HaveElementMatching(t => t.Id == 8);
                tree.Should().HaveElementMatching(t => t.Id == 9);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mysql")]
        public void SelectSubTree_UseAsSubQuery(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            connection.GetLanguageSpecifics().HierarchicalQuerySupported.Should().BeTrue();

            var treeSelect = connection.GetHierarchicalSelectQueryBuilder(mFixture.HierachicalTable, mFixture.HierachicalTable["parent"], "root");
            treeSelect.IdOnlyMode = true;

            var select = connection.GetSelectQueryBuilder(mFixture.HierachicalTable);
            select.AddToResultset(mFixture.HierachicalTable);
            select.Where.Property(mFixture.HierachicalTable["id"]).In().Query(treeSelect);

            var binder = new SelectQueryResultBinder(typeof(TreeComplete));
            binder.AutoBindType();

            using (var query = connection.GetQuery(select))
            {
                query.BindParam("root", 3);
                query.ExecuteReader();
                var tree = binder.ReadAll<List<TreeComplete>, TreeComplete>(query);

                tree.Should().HaveCount(5);
                tree.Should().HaveElementMatching(t => t.Id == 3 && t.Parent == 1 && t.Name == "1=>3");
                tree.Should().HaveElementMatching(t => t.Id == 6 && t.Parent == 3 && t.Name == "1=>3=>6");
                tree.Should().HaveElementMatching(t => t.Id == 7 && t.Parent == 6 && t.Name == "1=>3=>6=>7");
                tree.Should().HaveElementMatching(t => t.Id == 8 && t.Parent == 6 && t.Name == "1=>3=>6=>8");
                tree.Should().HaveElementMatching(t => t.Id == 9 && t.Parent == 3 && t.Name == "1=>3=>9");
            }
        }
    }
}

