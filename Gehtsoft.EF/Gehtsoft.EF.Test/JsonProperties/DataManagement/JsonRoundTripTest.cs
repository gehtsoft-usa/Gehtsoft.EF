using System;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.DataManagement
{
    public class JsonRoundTripTest : IClassFixture<JsonRoundTripTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonRoundTripTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public class Profile
        {
            [JsonPropertyName("full_name")]
            public string Name { get; set; }
            public int Age { get; set; }
            public bool Active { get; set; }
            public double Score { get; set; }
            public DateTime Born { get; set; }
            public int[] Tags { get; set; }
        }

        [Entity(Scope = "jsonrt", Table = "json_rt")]
        public class JsonRt
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Profile Data { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void WholeValue_RoundTrips(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<JsonRt>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<JsonRt>())
                q.Execute();

            try
            {
                var born = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                JsonRt withData, withNull;

                using (var q = connection.GetInsertEntityQuery<JsonRt>())
                {
                    withData = new JsonRt
                    {
                        Data = new Profile
                        {
                            Name = "alice",
                            Age = 30,
                            Active = true,
                            Score = 9.5,
                            Born = born,
                            Tags = new[] { 1, 2, 3 },
                        }
                    };
                    q.Execute(withData);

                    withNull = new JsonRt { Data = null };
                    q.Execute(withNull);
                }

                JsonRt gotData = null, gotNull = null;
                using (var q = connection.GetSelectEntitiesQuery<JsonRt>())
                {
                    q.Execute();
                    var all = q.ReadAll<JsonRt>();
                    foreach (var x in all)
                    {
                        if (x.ID == withData.ID)
                            gotData = x;
                        else if (x.ID == withNull.ID)
                            gotNull = x;
                    }
                }

                gotData.Should().NotBeNull();
                gotData.Data.Should().NotBeNull("the JSON value must deserialize on load");
                gotData.Data.Name.Should().Be("alice");
                gotData.Data.Age.Should().Be(30);
                gotData.Data.Active.Should().BeTrue();
                gotData.Data.Score.Should().Be(9.5);
                gotData.Data.Born.Should().Be(born);
                gotData.Data.Tags.Should().Equal(1, 2, 3);

                gotNull.Should().NotBeNull();
                gotNull.Data.Should().BeNull("a null CLR value stays SQL NULL and loads back as null");
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<JsonRt>();
                q.Execute();
            }
        }
    }
}
