using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.Tools.ConfigurationProfile;
using NUnit.Framework;

namespace TestApp
{
    [TestFixture]
    public class OracleTest
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
        public void TestTasks()
        {
            mConnection.Should().NotBeNull();
            TestTasksImpl.Test(mConnection);
        }
    }
}
