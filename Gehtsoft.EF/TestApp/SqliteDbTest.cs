using System;
using System.IO;
using FluentAssertions;
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
    }
}