using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.InteropServices;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb
{
    [TestCaseOrderer(TestOrderAttributeOrderer.CLASS, TestOrderAttributeOrderer.ASSEMBLY)]
    public class DropCreateTest : IClassFixture<DropCreateTest.Fixture>
    {
        #region fixture
        public class Fixture : ConnectionFixtureBase
        {
            public static bool DropAtEnd { get; set; } = false;

            public const string TableName = "dropcreate_test";
            public const string Dict1Name = "dropcreate_dict1";
            public const string Dict2Name = "dropcreate_dict2";

            public TableDescriptor TableDict1 { get; }
            public TableDescriptor TableDict2 { get; }
            public TableDescriptor TableV1 { get; }
            public TableDescriptor TableV2 { get; }

            public Fixture()
            {
                TableDict1 = new TableDescriptor()
                {
                    Name = Dict1Name,
                };

                TableDict1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                TableDict1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                TableDict2 = new TableDescriptor()
                {
                    Name = Dict2Name,
                };

                TableDict2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
                    PrimaryKey = true,
                    Autoincrement = true,
                    DbType = DbType.Int32
                });

                TableDict2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "name",
                    DbType = DbType.String,
                    Size = 32,
                    Sorted = true,
                });

                TableV1 = new TableDescriptor()
                {
                    Name = TableName,
                };

                TableV1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
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
                    Name = "dict1",
                    DbType = DbType.Int32,
                    ForeignTable = TableDict1
                });

                TableV1.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "description",
                    DbType = DbType.String,
                    Size = 32,
                });

                TableV2 = new TableDescriptor()
                {
                    Name = TableName,
                };

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "id",
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
                    Name = "dict1",
                    DbType = DbType.Int32,
                    ForeignTable = TableDict1
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "dict2",
                    DbType = DbType.Int32,
                    ForeignTable = TableDict2
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "new_description",
                    DbType = DbType.String,
                    Size = 32,
                });

                TableV2.Add(new TableDescriptor.ColumnInfo()
                {
                    Name = "due",
                    DbType = DbType.Date,
                    Sorted = true,
                    Nullable = true
                });
            }

            protected override void ConfigureConnection(SqlDbConnection connection)
            {
                Drop(connection);

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(TableDict1)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetCreateTableBuilder(TableDict2)))
                    query.ExecuteNoData();

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

                using (var query = connection.GetQuery(connection.GetDropTableBuilder(TableDict1)))
                    query.ExecuteNoData();

                using (var query = connection.GetQuery(connection.GetDropTableBuilder(TableDict2)))
                    query.ExecuteNoData();
            }
        }
        #endregion

        private readonly Fixture mFixture;

        public static IEnumerable<object[]> ConnectionNames(string flags = null) => SqlConnectionSources.ConnectionNames(flags);

        public DropCreateTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [TestOrder(1)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T1_CreateTable(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist(mFixture.TableV1.Name, null, "table").Should().BeFalse();

            using (var query = connection.GetQuery(connection.GetCreateTableBuilder(mFixture.TableV1)))
                query.ExecuteNoData();

            connection.DoesObjectExist(mFixture.TableV1.Name, null, "table").Should().BeTrue();
            connection.DoesObjectExist(mFixture.TableV1.Name, "name", "index");
        }

        [Theory]
        [TestOrder(2)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T2_AlterTable(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_CreateTable(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            var qb = connection.GetAlterTableQueryBuilder();
            TableDescriptor.ColumnInfo[] drop = null;

            if (connection.GetLanguageSpecifics().DropColumnSupported)
                drop = new[] { mFixture.TableV1["description"] };

            TableDescriptor.ColumnInfo[] add = new[] {
                mFixture.TableV2["new_description"],
                mFixture.TableV2["dict2"],
                mFixture.TableV2["due"],
            };

            qb.SetTable(mFixture.TableV2, add, drop);

            foreach (var queryText in qb.GetQueries())
                using (var query = connection.GetQuery(queryText))
                    query.ExecuteNoData();

            connection.DoesObjectExist(mFixture.TableV2.Name, "new_description", "column").Should().BeTrue();
            connection.DoesObjectExist(mFixture.TableV2.Name, "dict2", "column").Should().BeTrue();
            connection.DoesObjectExist(mFixture.TableV2.Name, "due", "column").Should().BeTrue();
            connection.DoesObjectExist(mFixture.TableV2.Name, "due", "index").Should().BeTrue();

            if (connection.GetLanguageSpecifics().DropColumnSupported)
                connection.DoesObjectExist(mFixture.TableV2.Name, "description", "column").Should().BeFalse();
        }

        [Theory]
        [TestOrder(3)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T3_CreateIndex(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T2_AlterTable(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist(mFixture.TableV2.Name, "composite", "index").Should().BeFalse();

            CompositeIndex index = new CompositeIndex("composite")
            {
                "due",
                "name"
            };

            using (var query = connection.GetQuery(connection.GetCreateIndexBuilder(mFixture.TableV2, index)))
                query.ExecuteNoData();

            connection.DoesObjectExist(mFixture.TableV2.Name, "composite", "index").Should().BeTrue();
        }

        [Theory]
        [TestOrder(4)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T4_DropIndex(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T3_CreateIndex(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist(mFixture.TableV2.Name, "composite", "index").Should().BeTrue();

            using (var query = connection.GetQuery(connection.GetDropIndexBuilder(mFixture.TableV2, "composite")))
                query.ExecuteNoData();

            connection.DoesObjectExist(mFixture.TableV2.Name, "composite", "index").Should().BeFalse();
        }

        [Theory]
        [TestOrder(5)]
        [MemberData(nameof(ConnectionNames), "")]
        public void T5_DropTable(string connectionName)
        {
            if (!mFixture.Started(connectionName))
                T1_CreateTable(connectionName);

            var connection = mFixture.GetInstance(connectionName);

            connection.DoesObjectExist(mFixture.TableV2.Name, null, "table").Should().BeTrue();

            using (var query = connection.GetQuery(connection.GetDropTableBuilder(mFixture.TableV2)))
                query.ExecuteNoData();

            connection.DoesObjectExist(mFixture.TableV2.Name, null, "table").Should().BeFalse();

            //check that it is save to call it again
            using (var query = connection.GetQuery(connection.GetDropTableBuilder(mFixture.TableV2)))
                ((Action)(() => query.ExecuteNoData())).Should().NotThrow();
        }
    }
}
