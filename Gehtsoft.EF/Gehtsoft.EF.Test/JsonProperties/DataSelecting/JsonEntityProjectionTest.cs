using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.DataSelecting
{
    // Projecting / aggregating / ordering / grouping by a JSON value at the ENTITY query level
    // (SelectEntitiesQuery + ReadAllDynamic).
    public class JsonEntityProjectionTest : IClassFixture<JsonEntityProjectionTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonEntityProjectionTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public class Payload
        {
            public int Age { get; set; }
            public string Team { get; set; }
        }

        [Entity(Scope = "jsoneproj", Table = "json_e_proj")]
        public class Doc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void EntityProject_And_OrderBy_JsonValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<Doc>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Doc>())
                q.Execute();

            try
            {
                using (var q = connection.GetInsertEntityQuery<Doc>())
                {
                    q.Execute(new Doc { Data = new Payload { Age = 50, Team = "b" } });
                    q.Execute(new Doc { Data = new Payload { Age = 30, Team = "a" } });
                    q.Execute(new Doc { Data = new Payload { Age = 40, Team = "a" } });
                }

                var ages = new List<int>();
                using (var q = connection.GetSelectEntitiesQueryBase(typeof(Doc)))
                {
                    q.AddJsonValueToResultset<Doc>("Data", "$.Age", DbType.Int32, "age");
                    q.AddJsonValueToOrderBy<Doc>("Data", "$.Age", DbType.Int32, SortDir.Asc);
                    q.Execute();
                    foreach (dynamic row in q.ReadAllDynamic())
                        ages.Add((int)row.age);
                }

                ages.Should().Equal(30, 40, 50);
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<Doc>();
                q.Execute();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void EntityAggregate_And_GroupBy_JsonValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            using (var q = connection.GetDropEntityQuery<Doc>())
                q.Execute();
            using (var q = connection.GetCreateEntityQuery<Doc>())
                q.Execute();

            try
            {
                using (var q = connection.GetInsertEntityQuery<Doc>())
                {
                    q.Execute(new Doc { Data = new Payload { Age = 30, Team = "a" } });
                    q.Execute(new Doc { Data = new Payload { Age = 40, Team = "a" } });
                    q.Execute(new Doc { Data = new Payload { Age = 50, Team = "b" } });
                }

                var totals = new Dictionary<string, int>();
                using (var q = connection.GetSelectEntitiesQueryBase(typeof(Doc)))
                {
                    q.AddJsonValueToResultset<Doc>("Data", "$.Team", DbType.String, "team");
                    q.AddJsonValueToResultset<Doc>(AggFn.Sum, "Data", "$.Age", DbType.Int32, "total");
                    q.AddJsonValueToGroupBy<Doc>("Data", "$.Team", DbType.String);
                    q.Execute();
                    foreach (dynamic row in q.ReadAllDynamic())
                        totals[(string)row.team] = (int)row.total;
                }

                totals.Should().HaveCount(2);
                totals["a"].Should().Be(70);
                totals["b"].Should().Be(50);
            }
            finally
            {
                using var q = connection.GetDropEntityQuery<Doc>();
                q.Execute();
            }
        }
    }
}
