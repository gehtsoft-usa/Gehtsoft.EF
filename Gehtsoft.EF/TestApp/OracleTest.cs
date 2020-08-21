using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.Tools.ConfigurationProfile;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    class OracleTest
    {
        private SqlDbConnection mConnection;

        [OneTimeSetUp]
        public void Setup()
        {
            string tns = Config.Instance.OracleTns;
            mConnection = UniversalSqlDbFactory.Create(UniversalSqlDbFactory.ORACLE, $"Data Source={tns};user id={Config.Instance.OracleUser};password={Config.Instance.OraclePassword};");
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
        public void TestTasks()
        {
            TestTasksImpl.Test(mConnection);
        }

    }
}
