using System.Data;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    internal class MysqlTest
    {
        private SqlDbConnection mConnection;

        [OneTimeSetUp]
        public void Setup()
        {
            mConnection = UniversalSqlDbFactory.Create(UniversalSqlDbFactory.MYSQL, Config.Instance.Mysql);
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
        public void TestEntities()
        {
            mConnection.Should().NotBeNull();
            TestEntity1.TestEntities(mConnection, false);
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

        [Test]
        public void TestNorthwind()
        {
            NorthwindTest t = new NorthwindTest();
            t.Test(mConnection);
        }
    }
}