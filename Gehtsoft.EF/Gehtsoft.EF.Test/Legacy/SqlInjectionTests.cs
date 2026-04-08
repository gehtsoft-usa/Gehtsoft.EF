using System;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Legacy
{
    public class SqlInjectionTests : IClassFixture<SqlInjectionTests.Fixture>
    {
        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public SqlInjectionTests(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> ConnectionNames(string flags = "")
            => SqlConnectionSources.SqlConnectionNames(flags);

        [Entity(Table = "tgoodsqi2")]
        public class Good
        {
            [EntityProperty(Field = "id", DbType = DbType.Int32, PrimaryKey = true, Autoincrement = true)]
            public int ID { get; set; }

            [EntityProperty(Field = "name", DbType = DbType.String, Size = 32, Sorted = true)]
            public string Name { get; set; }

            public Good()
            {
            }
        }

        private static void Create(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetCreateEntityQuery(type))
                query.Execute();
        }

        private static void Drop(SqlDbConnection connection, Type type)
        {
            EntityQuery query;
            using (query = connection.GetDropEntityQuery(type))
                query.Execute();
        }

        [Theory]
        [MemberData(nameof(ConnectionNames), "")]
        public void SqlInjectionPrevention(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);

            Drop(connection, typeof(Good));
            Create(connection, typeof(Good));

            var goodDescriptor = AllEntities.Inst[typeof(Good)].TableDescriptor;

            ((Action)(() => connection.GetQuery($"select * from {goodDescriptor.Name} where good = ';  -- '"))).Should().Throw<ArgumentException>();
            ((Action)(() => connection.GetQuery($"select * from {goodDescriptor.Name} where good = \";  -- \""))).Should().Throw<ArgumentException>();

            //check delete query
            {
                var builder = connection.GetDeleteQueryBuilder(goodDescriptor);
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("'"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("\""))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["ID"]).Eq().Value(1))).Should().NotThrow<ArgumentException>();
            }

            //check update query
            {
                var builder = connection.GetUpdateQueryBuilder(goodDescriptor);

                ((Action)(() => builder.AddUpdateColumn(goodDescriptor["Name"], "'; -- '"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("'"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("\""))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["ID"]).Eq().Value(1))).Should().NotThrow<ArgumentException>();
            }

            //check select query
            {
                var builder = connection.GetSelectQueryBuilder(goodDescriptor);
                ((Action)(() => builder.AddExpressionToResultset("'; --", DbType.String))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddExpressionToResultset("; --", DbType.String))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddToResultset(goodDescriptor["Name"], "';--"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.AddToResultset(goodDescriptor["Name"], ";--"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Where.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
                ((Action)(() => builder.Having.And().Property(goodDescriptor["Name"]).Eq().Value("a"))).Should().Throw<ArgumentException>();
            }

            using (var query = connection.GetGenericSelectEntityQuery<Good>())
            {
                ((Action)(() => query.AddExpressionToResultset("'; --", DbType.String, "hack"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddExpressionToResultset("; --", DbType.String, "hack"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddExpressionToResultset("Name", DbType.String, "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddToResultset("Name", "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddToResultset(AggFn.Avg, "Name", "hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.Where.Property("Name").Eq().Raw("hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.Where.Property("Name").Eq().Raw("'hack;--"))).Should().Throw<ArgumentException>();
                ((Action)(() => query.AddOrderByExpr("Name;"))).Should().Throw<ArgumentException>();
            }
        }
    }
}
