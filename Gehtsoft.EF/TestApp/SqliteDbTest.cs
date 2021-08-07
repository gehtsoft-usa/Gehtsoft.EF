using System;
using System.IO;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Context;
using MongoDB.Bson.Serialization.IdGenerators;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    internal class SqliteTest
    {
        private SqlDbConnection mConnection;
        private readonly bool mMemory = true;

        [OneTimeSetUp]
        public void Setup()
        {
            if (mMemory)
            {
                mConnection = UniversalSqlDbFactory.CreateAsync(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:").Result;
            }
            else
            {
                if (File.Exists(@"d:\test.db"))
                    File.Delete(@"d:\test.db");
                mConnection = UniversalSqlDbFactory.CreateAsync(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\test.db").Result;
            }
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            mConnection?.Dispose();
        }

        [Test]
        public void TestDropCreate()
        {
            mConnection.Should().NotBeNull();
            TestCreateAndDrop.Do(mConnection);
        }

        [Test]
        public void TestHierarchicalQuery()
        {
            mConnection.Should().NotBeNull();
            TestHierarchical.Do(mConnection);
        }

        [Test]
        public void TestEntities()
        {
            mConnection.Should().NotBeNull();
            TestEntity1.TestEntities(mConnection);
        }

        [Test]
        public void TestDynamicEntities()
        {
            mConnection.Should().NotBeNull();
            TestEntity1.TestDynamicEntity(mConnection);
        }

        [Test]
        [Explicit]
        public void PerformanceTest()
        {
            mConnection.Should().NotBeNull();
            TestPerformance.DoTest(mConnection);
        }

        [Test]
        public void TestFts1()
        {
            mConnection.Should().NotBeNull();
            TestFts.DoTestFts(mConnection);
        }

        [Test]
        public void TestFts2()
        {
            mConnection.Should().NotBeNull();
            TestFts.DoTestFtsAsync(mConnection).Wait();
        }

        [Test]
        public void TestAlterTables()
        {
            mConnection.Should().NotBeNull();
            TestDbUpdate.TestAlterTable(mConnection);
        }

        [Test]
        public void TestAlterEntities()
        {
            mConnection.Should().NotBeNull();
            TestDbUpdate.TestEntityUpdate(mConnection);
        }

        [Test]
        public void TestNestedTransactions()
        {
            mConnection.Should().NotBeNull();
            NestedTransactionsTest.Do(mConnection);
        }

        [Test]
        public void TestAggregatingAccessor()
        {
            mConnection.Should().NotBeNull();
            AggregatesTest.Do(mConnection);
        }

        [Test]
        public void TestTasks()
        {
            mConnection.Should().NotBeNull();
            TestTasksImpl.Test(mConnection);
        }

        [Ignore("Debug test")]
        [Test]
        [Explicit]
        public void DebugTest()
        {
            using (SqlDbConnection connection = SqliteDbConnectionFactory.CreateFile(@"F:\collections\data\coins.collection", false))
            {
                using (SqlDbQuery query = connection.GetQuery("select * from items"))
                {
                    query.ExecuteReader();
                    query.ReadNext();
                    object v1 = query.GetValue("lastchange");
                    v1.Should().BeOfType(typeof(DateTime));
                    DateTime v2 = query.GetValue<DateTime>("lastchange");
                    v1.Should().Be(v2);
                }
            }
        }

        [Test]
        public void TestNorthwind()
        {
            mConnection.Should().NotBeNull();
            NorthwindTest t = new NorthwindTest();
            t.Test(mConnection);
        }

        [Test]
        public void TestEntityContext()
        {
            mConnection.Should().NotBeNull();
            EntityContextTest.Test(mConnection);
        }

        [Test]
        public void TestInjections()
        {
            mConnection.Should().NotBeNull();
            TestSqlInjections.Do(mConnection);
        }

        [Entity(Table = "testdate")]
        public class DateTestEntity
        {
            [EntityProperty(Sorted = true)]
            public DateTime Date { get; set; }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DateModes(bool asString)
        {
            try
            {
                Random r = new Random();
                var dt = DateTime.Now;

                SqliteGlobalOptions.StoreDateAsString = asString;
                using var connection = SqliteDbConnectionFactory.CreateMemory();
                using (var query = connection.CreateEntity<DateTestEntity>())
                {
                    query.Execute();
                }

                for (int i = 0; i < 50; i++, dt = dt.AddMinutes(r.Next(5, 60)))
                {
                    var t = new DateTestEntity()
                    {
                        Date = dt
                    };

                    using var query = connection.InsertEntity<DateTestEntity>();
                    query.Execute(t);
                }

                using (var query = connection.Select<DateTestEntity>())
                {
                    query.Order.Add(nameof(DateTestEntity.Date));
                    var all = query.ReadAll<EntityCollection<DateTestEntity>, DateTestEntity>();
                    all.Should().HaveCount(50);
                    all.Should().BeInAscendingOrder(e => e.Date);
                }

                using (var query = connection.GetQuery("select * from testdate"))
                {
                    query.ExecuteReader();
                    query.ReadNext();
                    object v = query.GetValue(0);
                    if (asString)
                    {
                        v.Should().BeOfType<string>();
                        (v as string).Should().Match("????-??-?? ??:??:??");
                    }
                    else
                    {
                        v.Should().BeOfType<double>();
                        DateTime.FromOADate((double)v).Should().BeWithin(TimeSpan.FromHours(50));
                    }
                }
            }
            finally
            {
                SqliteGlobalOptions.StoreDateAsString = false;
            }
        }
    }
}