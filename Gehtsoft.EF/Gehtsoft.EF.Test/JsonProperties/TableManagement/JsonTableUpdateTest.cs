using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.TableManagement
{
    public class JsonTableUpdateTest : IClassFixture<JsonTableUpdateTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonTableUpdateTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public class Payload
        {
            public int A { get; set; }
            public string B { get; set; }
        }

        // "before": no JSON property
        [Entity(Scope = "jsonupd_v1", Table = "json_upd")]
        public class JsonUpdV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        // "after": a JSON property was added
        [Entity(Scope = "jsonupd_v2", Table = "json_upd")]
        public class JsonUpdV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        // same table, JSON column WITHOUT a value index
        [Entity(Scope = "jsonridx_v1", Table = "json_ridx")]
        public class JsonRIdxV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        // same table, JSON column WITH a value index
        [Entity(Scope = "jsonridx_v2", Table = "json_ridx")]
        public class JsonRIdxV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            [JsonIndex("$.age", System.Data.DbType.Int32)]
            public Payload Data { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void UpdateTables_AddsAndDropsJsonIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            new CreateEntityController(typeof(JsonRIdxV1), "jsonridx_v1")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeFalse("no JSON index declared yet");

            try
            {
                // a JSON value index was declared -> UpdateTables creates it
                new CreateEntityController(typeof(JsonRIdxV2), "jsonridx_v2")
                    .UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeTrue("the JSON index was added on update");

                // the declaration was removed -> UpdateTables drops the (framework-owned) index
                new CreateEntityController(typeof(JsonRIdxV1), "jsonridx_v1")
                    .UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeFalse("the JSON index was dropped on update");
            }
            finally
            {
                new CreateEntityController(typeof(JsonRIdxV1), "jsonridx_v1").DropTables(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void UpdateTables_AddsJsonColumn(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            // baseline table without the JSON column
            new CreateEntityController(typeof(JsonUpdV1), "jsonupd_v1")
                .UpdateTables(connection, CreateEntityController.UpdateMode.Recreate);
            connection.DoesObjectExist("json_upd", "data", "column").Should().BeFalse("no JSON property yet");

            try
            {
                // the entity gained a JSON property -> UpdateTables adds the column
                new CreateEntityController(typeof(JsonUpdV2), "jsonupd_v2")
                    .UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("json_upd", "data", "column").Should().BeTrue("the JSON column was added on update");

                // idempotent: a second update changes nothing
                new CreateEntityController(typeof(JsonUpdV2), "jsonupd_v2")
                    .UpdateTables(connection, CreateEntityController.UpdateMode.Update);
                connection.DoesObjectExist("json_upd", "data", "column").Should().BeTrue();
            }
            finally
            {
                new CreateEntityController(typeof(JsonUpdV1), "jsonupd_v1").DropTables(connection);
            }
        }
    }
}
