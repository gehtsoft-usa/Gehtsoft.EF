using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    internal class MssqlTest
    {
        private SqlDbConnection mConnection;

        [OneTimeSetUp]
        public void Setup()
        {
            mConnection = UniversalSqlDbFactory.Create(UniversalSqlDbFactory.MSSQL, Config.Instance.Mssql);
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
        public void TestTasks()
        {
            mConnection.Should().NotBeNull();
            TestTasksImpl.Test(mConnection);
        }
    }
}