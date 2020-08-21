using System;
using System.IO;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    internal class SqliteTest
    {
        private SqlDbConnection mConnection;

        [OneTimeSetUp]
        public void Setup()
        {
            bool memory = true;
            if (memory)
                mConnection = UniversalSqlDbFactory.CreateAsync(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:").Result;
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
            TestCreateAndDrop.Do(mConnection);
        }

        [Test]
        public void TestHierarchicalQuery()
        {
            TestHierarchical.Do(mConnection);
        }

        [Test]
        public void TestEntities()
        {
            TestEntity1.TestEntities(mConnection);
        }

        [Test]
        public void TestDynamicEntities()
        {
            TestEntity1.TestDynamicEntity(mConnection);
        }

        [Test]
        [Explicit]
        public void PerformanceTest()
        {
            TestPerformance.DoTest(mConnection);
        }

        [Test]
        public void TestFts1()
        {
            TestFts.DoTestFts(mConnection);
        }

        [Test]
        public void TestFts2()
        {
            TestFts.DoTestFtsAsync(mConnection).Wait();
        }

        [Test]
        public void TestAlterTables()
        {
            TestDbUpdate.TestAlterTable(mConnection);
        }

        [Test]
        public void TestAlterEntities()
        {
            TestDbUpdate.TestEntityUpdate(mConnection);
        }

        [Test]
        public void TestNestedTransactions()
        {
            NestedTransactionsTest.Do(mConnection);
        }

        [Test]
        public void TestAggregatingAccessor()
        {
            AggregatesTest.Do(mConnection);
        }

        [Test]
        public void TestTasks()
        {
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
                    DateTime v2 = query.GetValue<DateTime>("lastchange");
                }
            }
        }

        [Test]
        public void TestNorthwind()
        {
            NorthwindTest t = new NorthwindTest();
            t.Test(mConnection);
        }

        [Test]
        public void TestEntityContext()
        {
            EntityContextTest.Test(mConnection);
        }

        [Test]
        public void TestInjections()
        {
            TestSqlInjections.Do(mConnection);
        }
    }
}