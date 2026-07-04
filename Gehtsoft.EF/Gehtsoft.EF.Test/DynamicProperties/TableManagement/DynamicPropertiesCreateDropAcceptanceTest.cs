using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    public class DynamicPropertiesCreateDropAcceptanceTest : IClassFixture<SqlConnectionFixtureBase>
    {
        private readonly SqlConnectionFixtureBase mFixture;

        [Entity(Scope = "dynprops_ddl_acc", Table = "ddlacc_owner")]
        [DynamicProperties]
        public class Owner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }
        }

        public static TheoryData<string> ConnectionNames(string flags = null) => SqlConnectionSources.SqlConnectionNames(flags);

        public DynamicPropertiesCreateDropAcceptanceTest(SqlConnectionFixtureBase fixture)
        {
            mFixture = fixture;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void CreateDropSequence_CompletesWithoutError(string connectionName)
        {
            SqlDbConnection connection = mFixture.GetInstance(connectionName);

            // clean slate (safe no-op if nothing exists)
            using (EntityQuery query = connection.GetDropEntityQuery<Owner>())
                query.Execute();

            using (EntityQuery query = connection.GetCreateEntityQuery<Owner>())
                query.Execute();

            connection.DoesObjectExist("ddlacc_owner", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ddlacc_owner_props", null, "table").Should().BeTrue();

            using (EntityQuery query = connection.GetDropEntityQuery<Owner>())
                query.Execute();

            connection.DoesObjectExist("ddlacc_owner", null, "table").Should().BeFalse();
            connection.DoesObjectExist("ddlacc_owner_props", null, "table").Should().BeFalse();
        }
    }
}
