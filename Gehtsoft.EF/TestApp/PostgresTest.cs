using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    internal class PostgresTest
    {
        private SqlDbConnection mConnection;

        [OneTimeSetUp]
        public void Setup()
        {
            mConnection = UniversalSqlDbFactory.CreateAsync(UniversalSqlDbFactory.POSTGRES, Config.Instance.Postgres).Result;
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            mConnection.Dispose();
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
        [Explicit]
        public void PerformanceTest()
        {
            mConnection.Should().NotBeNull();
            TestPerformance.DoTest(mConnection);
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
        public void TestEntityContext()
        {
            mConnection.Should().NotBeNull();
            EntityContextTest.Test(mConnection);
        }

        [Test]
        public void TestNorthwind()
        {
            NorthwindTest t = new NorthwindTest();
            t.Test(mConnection);
        }
    }
}