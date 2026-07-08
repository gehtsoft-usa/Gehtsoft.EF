using System;
using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class DbUpdateTests : IClassFixture<DbUpdateTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public DbUpdateTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void AlterTable(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            TableDescriptor table1 = new TableDescriptor("altertabletest1",
                new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "id",
                        DbType = DbType.Int32,
                        PrimaryKey = true,
                    }
                });

            TableDescriptor table2 = new TableDescriptor("altertabletest2",
                new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "id",
                        DbType = DbType.Int32,
                        PrimaryKey = true,
                    },
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "code",
                        DbType = DbType.Int32,
                    },
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "ref",
                        DbType = DbType.Int32,
                        ForeignTable = table1,
                    },
                });

            using (SqlDbQuery query = connection.GetQuery())
            {
                DropTableBuilder builder1 = connection.GetDropTableBuilder(table2);
                builder1.PrepareQuery();
                query.CommandText = builder1.Query;
                query.ExecuteNoData();

                builder1 = connection.GetDropTableBuilder(table1);
                builder1.PrepareQuery();
                query.CommandText = builder1.Query;
                query.ExecuteNoData();
            }

            using (SqlDbQuery query = connection.GetQuery())
            {
                CreateTableBuilder builder2 = connection.GetCreateTableBuilder(table1);
                builder2.PrepareQuery();
                query.CommandText = builder2.Query;
                query.ExecuteNoData();

                builder2 = connection.GetCreateTableBuilder(table2);
                builder2.PrepareQuery();
                query.CommandText = builder2.Query;
                query.ExecuteNoData();
            }

            using (SqlDbQuery query1 = connection.GetQuery())
            {
                TableDescriptor.ColumnInfo[] add = new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "name",
                        DbType = DbType.String,
                        Size = 32,
                        Sorted = true,
                        Nullable = true,
                    },
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "name1",
                        DbType = DbType.String,
                        Size = 32,
                        Sorted = true,
                        Nullable = true,
                    },
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "ref1",
                        DbType = DbType.Int32,
                        ForeignTable = table1,
                        Nullable = true,
                    },
                };

                TableDescriptor.ColumnInfo[] drop = new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "code",
                    },

                    new TableDescriptor.ColumnInfo()
                    {
                        Name = "ref",
                        ForeignTable = table1,
                    },
                };

                bool dropSupported = connection.GetLanguageSpecifics().DropColumnSupported;

                AlterTableQueryBuilder builder = connection.GetAlterTableQueryBuilder();
                builder.SetTable(table2, add, dropSupported ? drop : null);
                foreach (string queryText in builder.GetQueries())
                {
                    using (SqlDbQuery query = connection.GetQuery(queryText))
                        query.ExecuteNoData();
                }

                TableDescriptor[] schema = connection.Schema();
                schema.Contains(table2.Name, "id").Should().BeTrue("id");
                schema.Contains(table2.Name, "code").Should().Be(!dropSupported, "code");
                schema.Contains(table2.Name, "ref").Should().Be(!dropSupported, "ref");
                schema.Contains(table2.Name, "name").Should().BeTrue("name");
                schema.Contains(table2.Name, "name1").Should().BeTrue("name1");
                schema.Contains(table2.Name, "ref1").Should().BeTrue("ref1");
            }
        }

        [OnEntityCreate(typeof(DbUpdateTests), nameof(DbUpdateTests.OnEntity0Created))]
        [Entity(Scope = "lv1", Table = "lentity0")]
        public class Entity0
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        [Entity(Scope = "lv1", Table = "lentity1")]
        public class Entity1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        [Entity(Scope = "lv1", Table = "lview1", View = true, Metadata = typeof(View1Metadata))]
        public class View1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        public class View1Metadata : IViewCreationMetadata
        {
            public SelectQueryBuilder GetSelectQuery(SqlDbConnection connection)
            {
                using var query = connection.GetSelectEntitiesQuery<Entity1>();
                return query.SelectBuilder;
            }
        }

        [Entity(Scope = "lv1", Table = "lentity2")]
        public class Entity2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "e1", ForeignKey = true)]
            public Entity1 E1 { get; set; }
        }

        [Entity(Scope = "lv2", Table = "lentity1")]
        public class Entity1_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [OnEntityPropertyDrop(typeof(DbUpdateTests), nameof(DbUpdateTests.OnEntity1ColDropped))]
            [ObsoleteEntityProperty(Field = "code", Sorted = true)]
            public string Code { get; set; }

            [OnEntityPropertyCreate(typeof(DbUpdateTests), nameof(DbUpdateTests.OnEntity1ColCreated))]
            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true, Nullable = true)]
            public string Name { get; set; }
        }

        [Entity(Scope = "lv2", Table = "lview1", View = true, Metadata = typeof(View1_2Metadata))]
        public class View1_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        public class View1_2Metadata : IViewCreationMetadata
        {
            public SelectQueryBuilder GetSelectQuery(SqlDbConnection connection)
            {
                using var query = connection.GetSelectEntitiesQuery<Entity1_2>();
                return query.SelectBuilder;
            }
        }

        [Entity(Scope = "lv2", Table = "lentity2")]
        public class Entity2_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [ObsoleteEntityProperty(Field = "e1", ForeignKey = true)]
            public Entity1_2 E1 { get; set; }

            [EntityProperty(Field = "e3", ForeignKey = true, Nullable = true)]
            public Entity3 E3 { get; set; }
        }

        [Entity(Scope = "lv2", Table = "lentity3")]
        public class Entity3
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        [OnEntityDrop(typeof(DbUpdateTests), nameof(DbUpdateTests.OnEntity0Dropped))]
        [ObsoleteEntity(Scope = "lv2", Table = "lentity0")]
        public class Entity0_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        // ---- Index-reconciliation test entities (three variants of table "idxr") ------------------

        // no Sorted column, no composite index
        [Entity(Scope = "idxr_base", Table = "idxr")]
        public class IdxBase
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "a", DbType = DbType.Int32, Nullable = true)] public int A { get; set; }
            [EntityProperty(Field = "b", DbType = DbType.Int32, Nullable = true)] public int B { get; set; }
            [EntityProperty(Field = "c", DbType = DbType.Int32, Nullable = true)] public int C { get; set; }
        }

        // "a" is Sorted, plus a composite index cmp(a, b)
        [Entity(Scope = "idxr_add", Table = "idxr", Metadata = typeof(CmpAB))]
        public class IdxAdd
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "a", DbType = DbType.Int32, Sorted = true, Nullable = true)] public int A { get; set; }
            [EntityProperty(Field = "b", DbType = DbType.Int32, Nullable = true)] public int B { get; set; }
            [EntityProperty(Field = "c", DbType = DbType.Int32, Nullable = true)] public int C { get; set; }
        }

        // "a" no longer Sorted; composite index cmp changed to (a, c)
        [Entity(Scope = "idxr_change", Table = "idxr", Metadata = typeof(CmpAC))]
        public class IdxChange
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "a", DbType = DbType.Int32, Nullable = true)] public int A { get; set; }
            [EntityProperty(Field = "b", DbType = DbType.Int32, Nullable = true)] public int B { get; set; }
            [EntityProperty(Field = "c", DbType = DbType.Int32, Nullable = true)] public int C { get; set; }
        }

        public class CmpAB : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get { var i = new CompositeIndex("cmp"); i.Add("a"); i.Add("b"); yield return i; }
            }
        }

        public class CmpAC : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get { var i = new CompositeIndex("cmp"); i.Add("a"); i.Add("c"); yield return i; }
            }
        }

        private static bool f1, f2, f3, f4;

        private static void OnEntity0Created(SqlDbConnection conneciton)
        {
            f1 = true;
        }

        private static void OnEntity0Dropped(SqlDbConnection conneciton)
        {
            f2 = true;
        }

        private static void OnEntity1ColCreated(SqlDbConnection conneciton)
        {
            f3 = true;
        }

        private static void OnEntity1ColDropped(SqlDbConnection conneciton)
        {
            f4 = true;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void EntityUpdate(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            f1 = f2 = f3 = f4 = false;

            CreateEntityController controller = new CreateEntityController(typeof(Entity0), "lv1");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            controller = new CreateEntityController(typeof(Entity0), "lv2");
            Dictionary<Type, CreateEntityController.UpdateMode> modes = null;
            if (!connection.GetLanguageSpecifics().DropColumnSupported)
            {
                modes = new Dictionary<Type, CreateEntityController.UpdateMode>
                {
                    [typeof(Entity2_2)] = CreateEntityController.UpdateMode.Recreate
                };
            }
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update, modes);

            TableDescriptor[] schema = connection.Schema();
            schema.Contains("lentity0").Should().BeFalse("lentity0");
            schema.Contains("lentity1").Should().BeTrue("lentity1");
            (!schema.Contains("lentity1", "code")).Should().Be(connection.GetLanguageSpecifics().DropColumnSupported, "code");
            schema.Contains("lentity1", "name").Should().BeTrue("name");
            schema.Contains("lentity2", "e1").Should().BeFalse("e1");
            schema.Contains("lentity2", "e3").Should().BeTrue("e3");
            schema.ContainsView("lview1").Should().BeTrue("view");
            f1.Should().BeTrue("f1");
            f2.Should().BeTrue("f2");
            f3.Should().BeTrue("f3");
            f4.Should().Be(connection.GetLanguageSpecifics().DropColumnSupported, "f4");
        }

        [Entity(Scope = "lv_guard", Table = "lguard_parent")]
        public class GuardParent
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32)]
            public string Name { get; set; }
        }

        [Entity(Scope = "lv_guard", Table = "lguard_child")]
        public class GuardChild
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(ForeignKey = true)]
            public GuardParent Parent { get; set; }
        }

        [Fact]
        public void EntityUpdateDetectsContradictoryModes()
        {
            using var connection = Gehtsoft.EF.Db.SqliteDb.SqliteDbConnectionFactory.CreateMemory();

            // first create both tables
            var controller = new CreateEntityController(typeof(GuardParent), "lv_guard");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            // now try to Recreate the parent while keeping the child as Update
            controller = new CreateEntityController(typeof(GuardParent), "lv_guard");
            var modes = new Dictionary<Type, CreateEntityController.UpdateMode>
            {
                [typeof(GuardParent)] = CreateEntityController.UpdateMode.Recreate,
                [typeof(GuardChild)] = CreateEntityController.UpdateMode.Update,
            };

            ((Action)(() => controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update, modes)))
                .Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.CannotRecreateTable);
        }

        // ---- Stage 0: GetTableIndexes enumeration ------------------------------------------------

        private sealed class IndexTestMetadata : ICompositeIndexMetadata
        {
            public IEnumerable<CompositeIndex> Indexes
            {
                get
                {
                    var ci = new CompositeIndex("cmp");
                    ci.Add("a");
                    ci.Add("b");
                    yield return ci;
                }
            }
        }

        private static TableDescriptor BuildIndexTestTable()
        {
            var table = new TableDescriptor("idxtest",
                new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                    new TableDescriptor.ColumnInfo { Name = "code", DbType = DbType.String, Size = 32, Sorted = true, Nullable = true },
                    new TableDescriptor.ColumnInfo { Name = "email", DbType = DbType.String, Size = 64, Unique = true, Nullable = true },
                    new TableDescriptor.ColumnInfo { Name = "a", DbType = DbType.Int32, Nullable = true },
                    new TableDescriptor.ColumnInfo { Name = "b", DbType = DbType.Int32, Nullable = true },
                })
            {
                Metadata = new IndexTestMetadata()
            };
            return table;
        }

        private static TableIndexInfo FindIndex(TableIndexInfo[] all, string name)
        {
            foreach (var i in all)
                if (string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return null;
        }

        [Fact]
        public void GetTableIndexes_Sqlite_Deep()
        {
            using var connection = Gehtsoft.EF.Db.SqliteDb.SqliteDbConnectionFactory.CreateMemory();
            var table = BuildIndexTestTable();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            var idx = connection.GetTableIndexes("idxtest");

            var code = FindIndex(idx, "idxtest_code");
            code.Should().NotBeNull();
            code.IsUnique.Should().BeFalse();
            code.IsPrimary.Should().BeFalse();
            code.IsExpression.Should().BeFalse();
            code.Columns.Should().Equal("code");

            var cmp = FindIndex(idx, "idxtest_cmp");
            cmp.Should().NotBeNull();
            cmp.IsUnique.Should().BeFalse();
            cmp.IsPrimary.Should().BeFalse();
            cmp.Columns.Should().Equal("a", "b");

            // the UNIQUE column produces a unique backing index, reported and flagged
            idx.Should().Contain(i => i.IsUnique, "the UNIQUE column must produce a unique backing index");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void GetTableIndexes_ReportsPlainAndFlagsConstraints(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var table = BuildIndexTestTable();

            using (var q = connection.GetQuery(connection.GetDropTableBuilder(table)))
                q.ExecuteNoData();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            try
            {
                var idx = connection.GetTableIndexes("idxtest");

                var code = FindIndex(idx, "idxtest_code");
                code.Should().NotBeNull("the Sorted column index must be reported");
                code.IsUnique.Should().BeFalse();
                code.IsPrimary.Should().BeFalse();
                code.Columns.Should().Equal("code");

                var cmp = FindIndex(idx, "idxtest_cmp");
                cmp.Should().NotBeNull("the composite index must be reported");
                cmp.IsUnique.Should().BeFalse();
                cmp.IsPrimary.Should().BeFalse();
                cmp.Columns.Should().Equal("a", "b");

                // every other reported index must be a PK/unique backing index (correctly flagged)
                foreach (var i in idx)
                    if (!string.Equals(i.Name, "idxtest_code", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(i.Name, "idxtest_cmp", StringComparison.OrdinalIgnoreCase))
                        (i.IsUnique || i.IsPrimary).Should().BeTrue($"index {i.Name} is neither of our plain indexes, so it must be a PK/unique backing index");
            }
            finally
            {
                using var q = connection.GetQuery(connection.GetDropTableBuilder(table));
                q.ExecuteNoData();
            }
        }

        // ---- Stage 1: UpdateTables index reconciliation ------------------------------------------

        [Fact]
        public void ReconcileIndexes_Lifecycle_Sqlite()
        {
            using var connection = Gehtsoft.EF.Db.SqliteDb.SqliteDbConnectionFactory.CreateMemory();

            // 1. baseline: table exists, no plain indexes
            new CreateEntityController(typeof(IdxBase), "idxr_base").UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            connection.DoesObjectExist("idxr", "a", "index").Should().BeFalse("no Sorted column yet");
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeFalse("no composite index yet");

            // 2. add: "a" Sorted + composite cmp(a,b)
            new CreateEntityController(typeof(IdxAdd), "idxr_add").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("idxr", "a", "index").Should().BeTrue("Sorted column index added");
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeTrue("composite index added");
            FindIndex(connection.GetTableIndexes("idxr"), "idxr_cmp").Columns.Should().Equal("a", "b");

            // 2b. idempotent: a second Update changes nothing
            new CreateEntityController(typeof(IdxAdd), "idxr_add").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("idxr", "a", "index").Should().BeTrue();
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeTrue();

            // 3. change: "a" no longer Sorted (single index dropped), cmp -> (a,c) (recreated)
            new CreateEntityController(typeof(IdxChange), "idxr_change").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("idxr", "a", "index").Should().BeFalse("column no longer Sorted");
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeTrue();
            FindIndex(connection.GetTableIndexes("idxr"), "idxr_cmp").Columns.Should().Equal("a", "c");

            // 4. remove all: back to baseline drops the composite
            new CreateEntityController(typeof(IdxBase), "idxr_base").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeFalse("composite index removed");
        }

        [Fact]
        public void ReconcileIndexes_LeavesNonConventionIndex_Sqlite()
        {
            using var connection = Gehtsoft.EF.Db.SqliteDb.SqliteDbConnectionFactory.CreateMemory();
            new CreateEntityController(typeof(IdxAdd), "idxr_add").UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            // a manually created index whose name does NOT follow the <table>_<name> convention
            using (var q = connection.GetQuery("CREATE INDEX manualidx ON idxr(b)", true))
                q.ExecuteNoData();

            // an unrelated update must not touch it
            new CreateEntityController(typeof(IdxAdd), "idxr_add").UpdateTables(connection, CreateEntityController.UpdateMode.Update);

            FindIndex(connection.GetTableIndexes("idxr"), "manualidx").Should().NotBeNull("a non-convention manual index is not framework-owned");
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeTrue("declared indexes remain");
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void ReconcileIndexes_AddAndDrop(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            // clean slate
            new CreateEntityController(typeof(IdxBase), "idxr_base").UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            connection.DoesObjectExist("idxr", "a", "index").Should().BeFalse();
            connection.DoesObjectExist("idxr", "cmp", "index").Should().BeFalse();

            try
            {
                // add Sorted + composite index
                new CreateEntityController(typeof(IdxAdd), "idxr_add").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("idxr", "a", "index").Should().BeTrue("Sorted index created");
                connection.DoesObjectExist("idxr", "cmp", "index").Should().BeTrue("composite index created");

                // remove them (back to baseline)
                new CreateEntityController(typeof(IdxBase), "idxr_base").UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("idxr", "a", "index").Should().BeFalse("Sorted index dropped");
                connection.DoesObjectExist("idxr", "cmp", "index").Should().BeFalse("composite index dropped");
            }
            finally
            {
                new CreateEntityController(typeof(IdxBase), "idxr_base").DropTables(connection);
            }
        }
    }
}
