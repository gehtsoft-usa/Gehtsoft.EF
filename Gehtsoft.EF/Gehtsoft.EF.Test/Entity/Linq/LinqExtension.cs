using System;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Tools;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    public class LinqExtension
    {
        [Entity(Scope = "linq2")]
        public class Dict
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        };

        [Entity(Scope = "linq2")]
        public class Entity
        {
            [AutoId]
            public int ID { get; set; }

            [ForeignKey]
            public Dict Reference { get; set; }

            [EntityProperty]
            public string StringValue { get; set; }

            [EntityProperty]
            public int IntValue { get; set; }

            [EntityProperty]
            public double RealValue { get; set; }

            [EntityProperty]
            public decimal DecimalValue { get; set; }

            [EntityProperty]
            public bool BooleanValue { get; set; }

            [EntityProperty]
            public DateTime DateTimeValue { get; set; }
        }

        [Fact]
        public void Resultset1()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();

            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");

            query.SelectBuilder.ResultColumn(0)
                .Alias.Should().Be("v1");
            query.SelectBuilder.ResultColumn(0)
                .Expression.Should().MatchPattern(query, "@1.intvalue");
        }

        [Fact]
        public void Resultset2()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();

            query.AddToResultset<Dict, string>(d => d.Name, "v2");

            query.SelectBuilder.ResultColumn(0)
                .Alias.Should().Be("v2");
            query.SelectBuilder.ResultColumn(0)
                .Expression.Should().MatchPattern(query, "@2.name");
        }

        [Fact]
        public void Resultset3()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();

            query.AddToResultset<Entity, Dict, string>((e, d) => e.StringValue + d.Name, "v3");

            query.SelectBuilder.ResultColumn(0)
               .Alias.Should().Be("v3");
            query.SelectBuilder.ResultColumn(0)
                .Expression.Should().MatchPattern(query, "@1.stringvalue || @2.name");
        }

        [Fact]
        public void Order()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.AddOrderBy<Entity>(e => e.StringValue);
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 ORDER BY @1.stringvalue");
        }

        [Fact]
        public void Group()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.AddGroupBy<Entity>(e => e.StringValue);
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 GROUP BY @1.stringvalue");
        }

        [Fact]
        public void Entity1()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Entity, Dict>(typeof(Entity), TableJoinType.Inner, (e, d) => e.Reference == d);
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @2.intvalue AS v1 FROM Dict AS @1 INNER JOIN Entity AS @2 ON (@2.reference = @1.id)");
        }

        [Fact]
        public void Entity2()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Dict>();
            query.AddEntity<Dict, Entity[]>(typeof(Entity), TableJoinType.Inner, (d, e) => e[0].Reference == d && e[0].IntValue == 1);
            query.AddEntity<Dict, Entity[]>(typeof(Entity), TableJoinType.Inner, (d, e) => e[1].Reference == d && e[1].IntValue == 2);
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @2.intvalue AS v1 FROM Dict AS @1 INNER JOIN Entity AS @2 ON ((@2.reference = @1.id) AND (@2.intvalue = @p)) INNER JOIN Entity AS @3 ON ((@3.reference = @1.id) AND (@3.intvalue = @p))");
        }

        [Fact]
        public void Where1()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.Where.Expression<Dict>(d => d.Name.StartsWith("a"));
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE @2.name LIKE @p || '%'");
        }

        [Fact]
        public void Where2()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.Where.Expression<Entity, Dict>((e, d) => d.Name == e.StringValue);
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE (@2.name = @1.stringvalue)");
        }

        [Fact]
        public void Where3()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.Where.And().Expression<Entity, Dict>((e, d) => d.Name == e.StringValue);
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE (@2.name = @1.stringvalue)");
        }

        [Fact]
        public void Where4()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetSelectEntitiesQueryBase<Entity>();
            query.AddEntity<Dict>();
            query.AddToResultset<Entity, int>(e => e.IntValue, "v1");
            query.Where.And().Expression<Entity>(e => e.Reference.Name.ToUpper() == SqlFunction.Left(e.StringValue, 5));
            query.PrepareQuery();
            query.SelectBuilder.Query
                .Should().MatchPattern(query, "SELECT @1.intvalue AS v1 FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE (UPPER(@2.name) = LEFT(@1.stringvalue, @p))");
        }

        [Fact]
        public void Update()
        {
            using var dummyConnection = new DummySqlConnection();
            using var query = dummyConnection.GetMultiUpdateEntityQuery<Dict>();

            query.AddUpdateColumn<Dict, string>(nameof(Dict.Name), d => d.Name.ToUpper());

            query.PrepareQuery();
            query.ConditionQueryBuilder.Query
                .Should().MatchPattern("UPDATE Dict SET name = UPPER(Dict.name)");
        }
    }
}
