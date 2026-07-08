using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataManagement
{
    /// <summary>
    /// DEBUG/scratch test for making the multi-statement batched dynamic-property INSERT work on every
    /// driver (KNOWN_ISSUES #1: fails on MySQL "connection already in use" and Oracle ORA-50028).
    /// Minimal: insert one owner with two properties (which triggers the combined multi-statement
    /// command), then verify the two rows landed. Remove once the CRUD multi-driver tests cover this.
    /// </summary>
    public class DynamicPropertiesMultiStatementDebugTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public DynamicPropertiesMultiStatementDebugTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        [Entity(Scope = "dp_dbg", Table = "dp_dbg_owner")]
        [DynamicProperties]
        public class DbgOwner : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32, Nullable = true)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        private static int PropsCount(SqlDbConnection c, int owner)
        {
            using (var q = c.GetQuery($"SELECT COUNT(*) FROM dp_dbg_owner_props WHERE owner = {owner}", true))
            {
                q.ExecuteReader();
                q.ReadNext();
                return q.GetValue<int>(0);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void InsertWithProperties(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<DbgOwner>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<DbgOwner>())
                q.Execute();
            try
            {
                var e = new DbgOwner { Name = "x" };
                var bag = e.InitializeDynamicProperties();
                bag.Set("a", 10);
                bag.Set("b", "hello");

                using (var q = connection.GetInsertEntityQuery<DbgOwner>())
                    q.Execute(e);

                e.Id.Should().BeGreaterThan(0);
                PropsCount(connection, e.Id).Should().Be(2);
            }
            finally
            {
                using (var q = connection.GetDropEntityQuery<DbgOwner>())
                    q.Execute();
            }
        }
    }
}
