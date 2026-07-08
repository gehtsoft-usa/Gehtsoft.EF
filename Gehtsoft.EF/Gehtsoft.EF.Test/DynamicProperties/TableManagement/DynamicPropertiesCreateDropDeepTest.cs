using System.Threading.Tasks;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqliteDb;
using Gehtsoft.EF.Entities;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.TableManagement
{
    public class DynamicPropertiesCreateDropDeepTest
    {
        [Entity(Scope = "dynprops_ddl_deep", Table = "ddl_owner")]
        [DynamicProperties]
        public class OwnerWithProps : IDynamicPropertiesOwner
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }

            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Entity(Scope = "dynprops_ddl_deep", Table = "ddl_plain")]
        public class PlainEntity
        {
            [AutoId]
            public int Id { get; set; }

            [EntityProperty(Size = 32)]
            public string Name { get; set; }
        }

        private const string PropsTable = "ddl_owner_props";

        private static readonly string[] PropsIndexes =
            { "owner", "owner_name", "name_str", "name_int", "name_real" };

        private static SqlDbConnection CreateConnection() => SqliteDbConnectionFactory.CreateMemory();

        [Fact]
        public void CreatedWhenNeeded_TableAndAllIndexes()
        {
            using SqlDbConnection connection = CreateConnection();

            using (EntityQuery query = connection.GetCreateEntityQuery<OwnerWithProps>())
                query.Execute();

            connection.DoesObjectExist("ddl_owner", null, "table").Should().BeTrue();
            connection.DoesObjectExist(PropsTable, null, "table").Should().BeTrue();

            foreach (string index in PropsIndexes)
                connection.DoesObjectExist(PropsTable, index, "index")
                          .Should().BeTrue($"index {PropsTable}_{index} should exist");
        }

        [Fact]
        public void NotCreatedWhenNotNeeded()
        {
            using SqlDbConnection connection = CreateConnection();

            using (EntityQuery query = connection.GetCreateEntityQuery<PlainEntity>())
                query.Execute();

            connection.DoesObjectExist("ddl_plain", null, "table").Should().BeTrue();
            connection.DoesObjectExist("ddl_plain_props", null, "table").Should().BeFalse();
        }

        [Fact]
        public void Drop_RemovesOwnerAndPropsTables()
        {
            using SqlDbConnection connection = CreateConnection();

            using (EntityQuery query = connection.GetCreateEntityQuery<OwnerWithProps>())
                query.Execute();

            using (EntityQuery query = connection.GetDropEntityQuery<OwnerWithProps>())
                query.Execute();

            connection.DoesObjectExist("ddl_owner", null, "table").Should().BeFalse();
            connection.DoesObjectExist(PropsTable, null, "table").Should().BeFalse();
        }

        [Fact]
        public void Drop_PlainEntity_Succeeds()
        {
            using SqlDbConnection connection = CreateConnection();

            using (EntityQuery query = connection.GetCreateEntityQuery<PlainEntity>())
                query.Execute();

            using (EntityQuery query = connection.GetDropEntityQuery<PlainEntity>())
                query.Execute();

            connection.DoesObjectExist("ddl_plain", null, "table").Should().BeFalse();
        }

        [Fact]
        public void Drop_WhenPropsTableNeverCreated_Succeeds()
        {
            using SqlDbConnection connection = CreateConnection();

            // Never created anything; drop must be a safe no-op (DROP TABLE IF EXISTS).
            using (EntityQuery query = connection.GetDropEntityQuery<OwnerWithProps>())
                query.Execute();

            connection.DoesObjectExist("ddl_owner", null, "table").Should().BeFalse();
            connection.DoesObjectExist(PropsTable, null, "table").Should().BeFalse();
        }

        [Fact]
        public async Task CreateAndDrop_Async()
        {
            using SqlDbConnection connection = CreateConnection();

            using (EntityQuery query = connection.GetCreateEntityQuery<OwnerWithProps>())
                await query.ExecuteAsync();

            connection.DoesObjectExist(PropsTable, null, "table").Should().BeTrue();

            using (EntityQuery query = connection.GetDropEntityQuery<OwnerWithProps>())
                await query.ExecuteAsync();

            connection.DoesObjectExist(PropsTable, null, "table").Should().BeFalse();
        }
    }
}
