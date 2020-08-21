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
            TestCreateAndDrop.Do(mConnection);
        }

        [Test]
        public void TestEntities()
        {
            TestEntity1.TestEntities(mConnection, false);
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
        public void TestAggregatingAccessor()
        {
            AggregatesTest.Do(mConnection);
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
        public void TestTasks()
        {
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