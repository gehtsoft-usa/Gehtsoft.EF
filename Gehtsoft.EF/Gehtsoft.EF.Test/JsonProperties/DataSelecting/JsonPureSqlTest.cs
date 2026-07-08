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
    // Exercises JSON columns via the pure SQL query builders + binders only — no entity queries.
    // The entity attributes are used solely to obtain a TableDescriptor (which carries the JSON
    // accessor); all CRUD goes through InsertQueryBuilder / SelectQueryBuilder / UpdateQueryBuilder /
    // DeleteQueryBuilder and the UpdateQueryToTypeBinder / SelectQueryResultBinder.
    public class JsonPureSqlTest : IClassFixture<JsonPureSqlTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonPureSqlTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        public class Payload
        {
            public int Age { get; set; }
            public string Name { get; set; }
        }

        [Entity(Scope = "jsonpuresql", Table = "json_pure")]
        public class Doc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        private static TableDescriptor Table => AllEntities.Inst[typeof(Doc)].TableDescriptor;

        private static List<Doc> ReadAll(SqlDbConnection connection)
        {
            var select = connection.GetSelectQueryBuilder(Table);
            select.AddToResultset(Table);
            var binder = new SelectQueryResultBinder(typeof(Doc));
            binder.AutoBindType();

            var list = new List<Doc>();
            using (var query = connection.GetQuery(select))
            {
                query.ExecuteReader();
                while (query.ReadNext())
                {
                    var doc = new Doc();
                    binder.Read(query, doc);
                    list.Add(doc);
                }
            }
            return list;
        }

        private static Doc Find(List<Doc> docs, int id)
        {
            foreach (var d in docs)
                if (d.Id == id)
                    return d;
            return null;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void PureSql_Json_Crud(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var table = Table;

            using (var q = connection.GetQuery(connection.GetDropTableBuilder(table)))
                q.ExecuteNoData();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();

            try
            {
                // INSERT (the whole JSON value is serialized by the accessor via the binder)
                var insertBinder = new UpdateQueryToTypeBinder(typeof(Doc));
                insertBinder.AutoBind(table);
                var alice = new Doc { Data = new Payload { Age = 30, Name = "alice" } };
                var bob = new Doc { Data = new Payload { Age = 40, Name = "bob" } };
                using (var q = connection.GetQuery(connection.GetInsertQueryBuilder(table)))
                {
                    insertBinder.BindAndExecute(q, alice, true);
                    insertBinder.BindAndExecute(q, bob, true);
                }
                alice.Id.Should().BeGreaterThan(0, "the auto id is read back");

                // SELECT all (the whole JSON value is deserialized by the accessor via the binder)
                var got = Find(ReadAll(connection), alice.Id);
                got.Data.Should().NotBeNull();
                got.Data.Age.Should().Be(30);
                got.Data.Name.Should().Be("alice");

                // SELECT with a JSON-value WHERE (pure-SQL ConditionBuilder.JsonValue)
                var select = connection.GetSelectQueryBuilder(table);
                select.AddToResultset(table["Id"]);
                select.Where.JsonValue(table["Data"], "$.Age", DbType.Int32).Eq().Value(40);
                var ids = new List<int>();
                using (var query = connection.GetQuery(select))
                {
                    query.ExecuteReader();
                    while (query.ReadNext())
                        ids.Add(query.GetValue<int>(0));
                }
                ids.Should().ContainSingle().Which.Should().Be(bob.Id);

                // UPDATE the whole JSON value
                alice.Data = new Payload { Age = 31, Name = "alice2" };
                var update = connection.GetUpdateQueryBuilder(table);
                update.AddUpdateAllColumns();
                update.UpdateById();
                var updateBinder = new UpdateQueryToTypeBinder(typeof(Doc));
                updateBinder.AutoBind(table);
                using (var q = connection.GetQuery(update))
                    updateBinder.BindAndExecute(q, alice, false);

                var reread = Find(ReadAll(connection), alice.Id);
                reread.Data.Age.Should().Be(31);
                reread.Data.Name.Should().Be("alice2");

                // DELETE with a JSON-value WHERE
                var delete = connection.GetDeleteQueryBuilder(table);
                delete.Where.JsonValue(table["Data"], "$.Age", DbType.Int32).Eq().Value(40);
                using (var q = connection.GetQuery(delete))
                    q.ExecuteNoData();

                ReadAll(connection).Should().HaveCount(1, "the row matching the JSON WHERE was deleted");
            }
            finally
            {
                using var q = connection.GetQuery(connection.GetDropTableBuilder(table));
                q.ExecuteNoData();
            }
        }
    }
}
