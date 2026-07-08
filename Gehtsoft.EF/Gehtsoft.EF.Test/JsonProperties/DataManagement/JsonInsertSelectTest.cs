using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.JsonProperties.DataManagement
{
    // INSERT ... SELECT with JSON columns, via the pure SQL query builders only.
    public class JsonInsertSelectTest : IClassFixture<JsonInsertSelectTest.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public JsonInsertSelectTest(Fixture fixture)
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

        [Entity(Scope = "jsonis_src", Table = "json_is_src")]
        public class Src
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        [Entity(Scope = "jsonis_dst", Table = "json_is_dst")]
        public class Dst
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [JsonEntityProperty(Field = "data", Nullable = true)]
            public Payload Data { get; set; }
        }

        // target with a plain (non-JSON) integer column, for the "extract into a regular column" case
        [Entity(Scope = "jsonis_age", Table = "json_is_age")]
        public class AgeRow
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int Id { get; set; }

            [EntityProperty(Field = "age", DbType = DbType.Int32, Nullable = true)]
            public int Age { get; set; }
        }

        private static void Recreate(SqlDbConnection connection, TableDescriptor table)
        {
            using (var q = connection.GetQuery(connection.GetDropTableBuilder(table)))
                q.ExecuteNoData();
            using (var q = connection.GetQuery(connection.GetCreateTableBuilder(table)))
                q.ExecuteNoData();
        }

        private static List<Dst> ReadAllDst(SqlDbConnection connection, TableDescriptor table)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table);
            var binder = new SelectQueryResultBinder(typeof(Dst));
            binder.AutoBindType();
            var list = new List<Dst>();
            using var query = connection.GetQuery(select);
            query.ExecuteReader();
            while (query.ReadNext())
            {
                var d = new Dst();
                binder.Read(query, d);
                list.Add(d);
            }
            return list;
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void InsertSelect_CopiesWholeJsonColumn(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var src = AllEntities.Inst[typeof(Src)].TableDescriptor;
            var dst = AllEntities.Inst[typeof(Dst)].TableDescriptor;

            Recreate(connection, src);
            Recreate(connection, dst);

            try
            {
                var alice = new Src { Data = new Payload { Age = 30, Name = "alice" } };
                var bob = new Src { Data = new Payload { Age = 40, Name = "bob" } };
                var binder = new UpdateQueryToTypeBinder(typeof(Src));
                binder.AutoBind(src);
                using (var q = connection.GetQuery(connection.GetInsertQueryBuilder(src)))
                {
                    binder.BindAndExecute(q, alice, true);
                    binder.BindAndExecute(q, bob, true);
                }

                // copy the whole JSON column (id + data) into the target
                var select = connection.GetSelectQueryBuilder(src);
                select.AddToResultset(src["Id"]);
                select.AddToResultset(src["Data"]);
                var insert = connection.GetInsertSelectQueryBuilder(dst, select, true);
                using (var q = connection.GetQuery(insert))
                    q.ExecuteNoData();

                var rows = ReadAllDst(connection, dst);
                rows.Should().HaveCount(2);
                foreach (var r in rows)
                    r.Data.Should().NotBeNull("the JSON document survived the INSERT..SELECT");
                Find(rows, alice.Id).Data.Name.Should().Be("alice");
                Find(rows, bob.Id).Data.Age.Should().Be(40);
            }
            finally
            {
                using (var q = connection.GetQuery(connection.GetDropTableBuilder(dst))) q.ExecuteNoData();
                using (var q = connection.GetQuery(connection.GetDropTableBuilder(src))) q.ExecuteNoData();
            }
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "-mssql,-mysql")]
        public void InsertSelect_ExtractsJsonValueIntoRegularColumn(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            var src = AllEntities.Inst[typeof(Src)].TableDescriptor;
            var ageTable = AllEntities.Inst[typeof(AgeRow)].TableDescriptor;

            Recreate(connection, src);
            Recreate(connection, ageTable);

            try
            {
                var alice = new Src { Data = new Payload { Age = 30, Name = "alice" } };
                var bob = new Src { Data = new Payload { Age = 40, Name = "bob" } };
                var binder = new UpdateQueryToTypeBinder(typeof(Src));
                binder.AutoBind(src);
                using (var q = connection.GetQuery(connection.GetInsertQueryBuilder(src)))
                {
                    binder.BindAndExecute(q, alice, true);
                    binder.BindAndExecute(q, bob, true);
                }

                // project the extracted JSON value into the plain "age" column of the target
                var select = connection.GetSelectQueryBuilder(src);
                select.AddToResultset(src["Id"]);
                select.AddJsonValueToResultset(src["Data"], "$.Age", DbType.Int32);
                var insert = connection.GetInsertSelectQueryBuilder(ageTable, select, true);
                using (var q = connection.GetQuery(insert))
                    q.ExecuteNoData();

                var ages = new Dictionary<int, int>();
                var read = connection.GetSelectQueryBuilder(ageTable);
                read.AddToResultset(ageTable["Id"]);
                read.AddToResultset(ageTable["Age"]);
                using (var q = connection.GetQuery(read))
                {
                    q.ExecuteReader();
                    while (q.ReadNext())
                        ages[q.GetValue<int>(0)] = q.GetValue<int>(1);
                }

                ages.Should().HaveCount(2);
                ages[alice.Id].Should().Be(30);
                ages[bob.Id].Should().Be(40);
            }
            finally
            {
                using (var q = connection.GetQuery(connection.GetDropTableBuilder(ageTable))) q.ExecuteNoData();
                using (var q = connection.GetQuery(connection.GetDropTableBuilder(src))) q.ExecuteNoData();
            }
        }

        private static Dst Find(List<Dst> rows, int id)
        {
            foreach (var r in rows)
                if (r.Id == id)
                    return r;
            return null;
        }
    }
}
