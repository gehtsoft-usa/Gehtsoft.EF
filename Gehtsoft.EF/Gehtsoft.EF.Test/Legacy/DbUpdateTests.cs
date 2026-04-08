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

        private static bool f1, f2, f3, f4;

        public static void OnEntity0Created(SqlDbConnection conneciton)
        {
            f1 = true;
        }

        public static void OnEntity0Dropped(SqlDbConnection conneciton)
        {
            f2 = true;
        }

        public static void OnEntity1ColCreated(SqlDbConnection conneciton)
        {
            f3 = true;
        }

        public static void OnEntity1ColDropped(SqlDbConnection conneciton)
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
    }
}
