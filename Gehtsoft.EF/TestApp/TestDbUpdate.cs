using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.FTS;
using NUnit.Framework;

namespace TestApp
{
    public static class TestDbUpdate
    {
        public static void TestAlterTable(SqlDbConnection connection)
        {
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
                Assert.IsTrue(schema.Contains(table2.Name, "id"), "id");
                Assert.AreEqual(!dropSupported, schema.Contains(table2.Name, "code"), "code");
                Assert.AreEqual(!dropSupported, schema.Contains(table2.Name, "ref"), "ref");
                Assert.IsTrue(schema.Contains(table2.Name, "name"), "name");
                Assert.IsTrue(schema.Contains(table2.Name, "name1"), "name1");
                Assert.IsTrue(schema.Contains(table2.Name, "ref1"), "ref1");
            }
        }

        [OnEntityCreate(typeof(TestDbUpdate), nameof(TestDbUpdate.OnEntity0Created))]
        [Entity(Scope = "v1", Table = "entity0")]
        public class Entity0
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        [Entity(Scope = "v1", Table = "entity1")]
        public class Entity1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "code", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Code { get; set; }
        }

        [Entity(Scope = "v1", Table = "view1", View = true, Metadata = typeof(View1Metadata))]
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
                var builder = connection.GetSelectEntityQueryBuilder<Entity1>();
                return builder.SelectQueryBuilder;
            }
        }

        [Entity(Scope = "v1", Table = "entity2")]
        public class Entity2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "e1", ForeignKey = true)]
            public Entity1 E1 { get; set; }
        }

        [Entity(Scope = "v2", Table = "entity1")]
        public class Entity1_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [OnEntityPropertyDrop(typeof(TestDbUpdate), nameof(TestDbUpdate.OnEntity1ColDropped))]
            [ObsoleteEntityProperty(Field = "code", Sorted = true)]
            public string Code { get; set; }

            [OnEntityPropertyCreate(typeof(TestDbUpdate), nameof(TestDbUpdate.OnEntity1ColCreated))]
            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true, Nullable = true)]
            public string Name { get; set; }
        }

        [Entity(Scope = "v2", Table = "view1", View = true, Metadata = typeof(View1_2Metadata))]
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
                var builder = connection.GetSelectEntityQueryBuilder<Entity1_2>();
                return builder.SelectQueryBuilder;
            }
        }

        [Entity(Scope = "v2", Table = "entity2")]
        public class Entity2_2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [ObsoleteEntityProperty(Field = "e1", ForeignKey = true)]
            public Entity1_2 E1 { get; set; }

            [EntityProperty(Field = "e3", ForeignKey = true, Nullable = true)]
            public Entity3  E3 { get; set; }
        }

        [Entity(Scope = "v2", Table = "entity3")]
        public class Entity3
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

        }

        [OnEntityDrop(typeof(TestDbUpdate), nameof(TestDbUpdate.OnEntity0Dropped))]
        [ObsoleteEntity(Scope = "v2", Table = "entity0")]
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
        public static void TestEntityUpdate(SqlDbConnection connection)
        {
            f1 = f2 = f3 = f4 = false;

            CreateEntityController controller = new CreateEntityController(typeof(Entity0), "v1");
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);

            controller = new CreateEntityController(typeof(Entity0), "v2");
            Dictionary<Type, CreateEntityController.UpdateMode> modes = null;
            if (!connection.GetLanguageSpecifics().DropColumnSupported)
            {
                modes = new Dictionary<Type, CreateEntityController.UpdateMode>();
                modes[typeof(Entity2_2)] = CreateEntityController.UpdateMode.Recreate;
            }
            controller.UpdateTables(connection, CreateEntityController.UpdateMode.Update, modes);

            TableDescriptor[] schema = connection.Schema();
            Assert.IsFalse(schema.Contains("entity0"), "entity0");
            Assert.IsTrue(schema.Contains("entity1"), "entity1");
            Assert.AreEqual(connection.GetLanguageSpecifics().DropColumnSupported, !schema.Contains("entity1", "code"), "code");
            Assert.IsTrue(schema.Contains("entity1", "name"), "name");
            Assert.IsFalse(schema.Contains("entity2", "e1"), "e1");
            Assert.IsTrue(schema.Contains("entity2", "e3"), "e3");
            Assert.IsTrue(schema.ContainsView("view1"), "view");
            Assert.IsTrue(f1, "f1");
            Assert.IsTrue(f2, "f2");
            Assert.IsTrue(f3, "f3");
            Assert.AreEqual(connection.GetLanguageSpecifics().DropColumnSupported, f4, "f4");
        }

    }
}

