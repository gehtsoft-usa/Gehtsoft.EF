using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.TableManagement
{
    public class JsonTableCreateTest : IClassFixture<JsonTableCreateTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonTableCreateTest(Fixture fixture)
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

        // a JSON-column entity WITHOUT declared JSON indexes (isolates the column DDL)
        [Entity(Scope = "jsontable", Table = "json_doc")]
        public class JsonDoc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void CreateAndDrop_JsonColumn(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            // clean slate
            using (var q = connection.GetDropEntityQuery<JsonDoc>())
                q.Execute();

            using (var q = connection.GetCreateEntityQuery<JsonDoc>())
                q.Execute();

            try
            {
                connection.DoesObjectExist("json_doc", null, "table").Should().BeTrue("the table is created");
                connection.DoesObjectExist("json_doc", "data", "column").Should().BeTrue("the JSON column is created (as a string column)");
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<JsonDoc>();
                q.Execute();
            }

            connection.DoesObjectExist("json_doc", null, "table").Should().BeFalse("the table is dropped");
        }

        [Entity(Scope = "jsonidx", Table = "json_idx")]
        public class JsonIdx
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            [JsonIndex("$.name", DbType.String)]
            [JsonIndex("$.age", DbType.Int32)]
            [JsonIndex("$.active", DbType.Boolean)]
            [JsonIndex("$.score", DbType.Double)]
            [JsonIndex("$.born", DbType.DateTime)]
            public Payload Data { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void CreateAndDrop_JsonIndexes(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<JsonIdx>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<JsonIdx>())
                q.Execute();

            var names = new[] { "data_name_str", "data_age_i32", "data_active_bool", "data_score_dbl", "data_born_dt" };
            try
            {
                foreach (var n in names)
                    connection.DoesObjectExist("json_idx", n, "index").Should().BeTrue($"JSON index {n} was created");
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<JsonIdx>();
                q.Execute();
            }

            foreach (var n in names)
                connection.DoesObjectExist("json_idx", n, "index").Should().BeFalse($"JSON index {n} dropped with the table");
        }

        [Fact]
        public void JsonColumn_OnUnsupportedDriver_ThrowsOnCreate()
        {
            // a dialect that does not support JSON (SupportsJson == false, as on MSSQL/MySQL)
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsJson.Should().BeFalse();

            var table = new TableDescriptor("json_doc",
                new TableDescriptor.ColumnInfo[]
                {
                    new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                    new TableDescriptor.ColumnInfo
                    {
                        Name = "data",
                        DbType = DbType.String,
                        Nullable = true,
                        Json = new JsonColumnMetadata(typeof(Payload), Array.Empty<JsonIndexDefinition>()),
                    },
                });

            var builder = connection.GetCreateTableBuilder(table);
            ((Action)(() => builder.PrepareQuery()))
                .Should().Throw<EfSqlException>()
                .Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
        }
    }
}
