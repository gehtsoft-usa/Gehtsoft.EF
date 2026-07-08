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
    // Projecting / aggregating / ordering / grouping by a JSON value — via the pure SQL query
    // builder only (no entity queries).
    public class JsonPureSqlProjectionTest : IClassFixture<JsonPureSqlProjectionTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonPureSqlProjectionTest(Fixture fixture)
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

        [Entity(Scope = "jsonproj", Table = "json_proj")]
        public class Doc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        private static TableDescriptor Table => AllEntities.Inst[typeof(Doc)].TableDescriptor;

        private static void Insert(SqlDbConnection connection, params Doc[] docs)
        {
            var binder = new UpdateQueryToTypeBinder(typeof(Doc));
            binder.AutoBind(Table);
            using var q = connection.GetQuery(connection.GetInsertQueryBuilder(Table));
            foreach (var d in docs)
                binder.BindAndExecute(q, d, true);
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Project_And_OrderBy_JsonValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var table = Table;

            using (var q = connection.GetQuery(connection.GetDropTableBuilder(table)))
                q.ExecuteNoData();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            try
            {
                Insert(connection,
                    new Doc { Data = new Payload { Age = 50, Team = "b" } },
                    new Doc { Data = new Payload { Age = 30, Team = "a" } },
                    new Doc { Data = new Payload { Age = 40, Team = "a" } });

                // project the JSON value and order by it
                var select = connection.GetSelectQueryBuilder(table);
                select.AddJsonValueToResultset(table["Data"], "$.Age", DbType.Int32, "age");
                select.AddJsonValueToOrderBy(table["Data"], "$.Age", DbType.Int32, SortDir.Asc);

                var ages = new List<int>();
                using (var query = connection.GetQuery(select))
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        ages.Add(query.GetValue<int>("age"));
                }

                ages.Should().Equal(30, 40, 50);
            }
            finally
            {
                using var q = connection.GetQuery(connection.GetDropTableBuilder(table));
                q.ExecuteNoData();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void Aggregate_And_GroupBy_JsonValue(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var table = Table;

            using (var q = connection.GetQuery(connection.GetDropTableBuilder(table)))
                q.ExecuteNoData();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            try
            {
                Insert(connection,
                    new Doc { Data = new Payload { Age = 30, Team = "a" } },
                    new Doc { Data = new Payload { Age = 40, Team = "a" } },
                    new Doc { Data = new Payload { Age = 50, Team = "b" } });

                // SUM(json age) grouped by json team
                var select = connection.GetSelectQueryBuilder(table);
                select.AddJsonValueToResultset(table["Data"], "$.Team", DbType.String, "team");
                select.AddJsonValueToResultset(AggFn.Sum, table["Data"], "$.Age", DbType.Int32, "total");
                select.AddJsonValueToGroupBy(table["Data"], "$.Team", DbType.String);

                var totals = new Dictionary<string, int>();
                using (var query = connection.GetQuery(select))
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        totals[query.GetValue<string>("team")] = query.GetValue<int>("total");
                }

                totals.Should().HaveCount(2);
                totals["a"].Should().Be(70);
                totals["b"].Should().Be(50);
            }
            finally
            {
                using var q = connection.GetQuery(connection.GetDropTableBuilder(table));
                q.ExecuteNoData();
            }
        }
    }
}
