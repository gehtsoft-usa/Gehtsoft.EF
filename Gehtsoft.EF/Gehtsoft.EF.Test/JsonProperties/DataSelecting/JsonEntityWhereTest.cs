using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.DataSelecting
{
    // Entity-level WHERE on a JSON value: the string-path form and the LINQ (expression) form.
    public class JsonEntityWhereTest : IClassFixture<JsonEntityWhereTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonEntityWhereTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public class Profile
        {
            public int Age { get; set; }
            public string Name { get; set; }
        }

        [Entity(Scope = "jsonwhere_e", Table = "json_where_e")]
        public class Person
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "profile", Nullable = true)]
            public Profile Profile { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void EntityWhere_JsonValue_StringAndLinq(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<Person>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Person>())
                q.Execute();

            try
            {
                Person alice, bob;
                using (var q = connection.GetInsertEntityQuery<Person>())
                {
                    alice = new Person { Profile = new Profile { Age = 30, Name = "alice" } };
                    q.Execute(alice);
                    bob = new Person { Profile = new Profile { Age = 40, Name = "bob" } };
                    q.Execute(bob);
                }

                // string-path form
                using (var q = connection.GetSelectEntitiesQuery<Person>())
                {
                    q.Where.JsonPropertyOf<Person>("Profile", "$.Age", DbType.Int32).Eq().Value(30);
                    q.Execute();
                    var list = q.ReadAll<Person>();
                    list.Should().ContainSingle().Which.Id.Should().Be(alice.Id);
                }

                // LINQ (expression) form
                using (var q = connection.GetSelectEntitiesQuery<Person>())
                {
                    q.Where.JsonPropertyOf<Person>(p => p.Profile.Age).Eq().Value(40);
                    q.Execute();
                    var list = q.ReadAll<Person>();
                    list.Should().ContainSingle().Which.Id.Should().Be(bob.Id);
                }
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<Person>();
                q.Execute();
            }
        }
    }
}
