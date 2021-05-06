using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    public static class TestPerformance
    {
        [Entity(Table = "tperf")]
        public class TestTable
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 128, Sorted = true)]
            public string Name { get; set; }

            public TestTable()
            {
            }

            public TestTable(string name)
            {
                Name = name;
            }
        }

        private static void Create(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetCreateEntityQuery(type))
                query.Execute();
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

        public static void DoTest(SqlDbConnection connection)
        {
            Drop(connection, typeof(TestTable));
            Create(connection, typeof(TestTable));
            Stopwatch sw = new Stopwatch();

            sw.Start();
            using (var t = connection.BeginTransaction())
            {
                using (SqlDbQuery query = connection.GetQuery($"insert into tperf (name) values ({connection.GetLanguageSpecifics().ParameterInQueryPrefix}name)"))
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        query.BindParam<string>("name", $"name {i: 000}");
                        query.ExecuteNoData();
                    }
                }
                t.Commit();
            }
            sw.Stop();
            Console.WriteLine($"Insert using SQL {sw.ElapsedMilliseconds} ms");
            sw.Reset();

            sw.Start();
            using (var t = connection.BeginTransaction())
            {
                using (ModifyEntityQuery query = connection.GetInsertEntityQuery(typeof(TestTable)))
                {
                    for (int i = 1000; i < 2000; i++)
                    {
                        TestTable o = new TestTable() { Name = $"name {i: 000}" };
                        query.Execute(o);
                    }
                }
                t.Commit();
            }
            sw.Stop();
            Console.WriteLine($"Insert using QRE {sw.ElapsedMilliseconds} ms");
            sw.Reset();

            sw.Start();
            for (int a = 0; a < 10; a++)
            {
                EntityCollection<TestTable> t = new EntityCollection<TestTable>();
                using (SqlDbQuery query = connection.GetQuery("select * from tperf where id < 1000 order by name"))
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                    {
                        t.Add(new TestTable() { ID = query.GetValue<int>("id"), Name = query.GetValue<string>("name") });
                    }
                }
                Assert.AreNotEqual(0, t.Count);
            }
            sw.Stop();
            Console.WriteLine($"Fill collection 10 times using SQL {sw.ElapsedMilliseconds} ms");
            sw.Reset();

            sw.Start();
            for (int a = 0; a < 10; a++)
            {
                EntityCollection<TestTable> t = null;
                using (SelectEntitiesQuery query = connection.GetSelectEntitiesQuery(typeof(TestTable)))
                {
                    query.AddOrderBy(typeof(TestTable), nameof(TestTable.Name), SortDir.Asc);
                    query.Where.Property(nameof(TestTable.ID)).Is(CmpOp.Le).Value(1000);
                    t = query.ReadAll<EntityCollection<TestTable>, TestTable>();
                }
                Assert.IsNotNull(t);
                Assert.AreNotEqual(0, t.Count);
            }
            sw.Stop();
            Console.WriteLine($"Fill collection 10 times using QRE {sw.ElapsedMilliseconds} ms");
            sw.Reset();
        }
    }
}

