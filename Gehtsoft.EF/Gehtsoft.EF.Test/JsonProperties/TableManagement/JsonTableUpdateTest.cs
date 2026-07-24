using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.TableManagement
{
    // JSON schema migration is exercised through the current CatalogEntityController. The catalogue reconciles
    // against its own recorded state, so an incremental V1->V2 migration is set up by building the V1 shape,
    // seeding the target scope's catalogue with it, then running UpdateTables against the V2 model.
    public class JsonTableUpdateTest : IClassFixture<JsonTableUpdateTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(JsonTableUpdateTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonTableUpdateTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        private static void DropJsonRidx(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("json_ridx", null, "table"))
                using (var q = connection.GetDropEntityQuery<JsonRIdxV1>())
                    q.Execute();
        }

        private static void DropJsonUpd(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("json_upd", null, "table"))
                using (var q = connection.GetDropEntityQuery<JsonUpdV1>())
                    q.Execute();
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
        public void UpdateTables_AddsJsonIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            DropJsonRidx(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                // before: JSON column, no value index
                new CatalogEntityController(typeof(JsonRIdxV1), "jsonridx_v1").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeFalse("no JSON index declared yet");

                // migrate to the model that declares the JSON value index -> it is created
                CatalogTestSupport.Seed(connection, "jsonridx_v2", "json_ridx", typeof(JsonRIdxV1), "1.0.0");
                new CatalogEntityController(typeof(JsonRIdxV2), "jsonridx_v2")
                    .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeTrue("the JSON index was added on update");
            }
            finally
            {
                DropJsonRidx(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void UpdateTables_DropsJsonIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            DropJsonRidx(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                // before: JSON column WITH a value index
                new CatalogEntityController(typeof(JsonRIdxV2), "jsonridx_v2").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeTrue("the JSON index is present initially");

                // migrate to the model that no longer declares it -> the framework-owned index is dropped
                CatalogTestSupport.Seed(connection, "jsonridx_v1", "json_ridx", typeof(JsonRIdxV2), "1.0.0");
                new CatalogEntityController(typeof(JsonRIdxV1), "jsonridx_v1")
                    .UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
                connection.DoesObjectExist("json_ridx", "data_age_i32", "index").Should().BeFalse("the JSON index was dropped on update");
            }
            finally
            {
                DropJsonRidx(connection);
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void UpdateTables_AddsJsonColumn(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            DropJsonUpd(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                // baseline table without the JSON column
                new CatalogEntityController(typeof(JsonUpdV1), "jsonupd_v1").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("json_upd", "data", "column").Should().BeFalse("no JSON property yet");

                // the entity gained a JSON property -> UpdateTables adds the column
                CatalogTestSupport.Seed(connection, "jsonupd_v2", "json_upd", typeof(JsonUpdV1), "1.0.0");
                var controller = new CatalogEntityController(typeof(JsonUpdV2), "jsonupd_v2");
                controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
                connection.DoesObjectExist("json_upd", "data", "column").Should().BeTrue("the JSON column was added on update");

                // idempotent: a re-run at the same version with an unchanged model changes nothing
                controller.UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);
                connection.DoesObjectExist("json_upd", "data", "column").Should().BeTrue();
            }
            finally
            {
                DropJsonUpd(connection);
            }
        }
    }
}
