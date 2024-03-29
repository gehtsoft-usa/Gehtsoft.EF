﻿using System;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Tools;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Linq
{
    public class SelectCompiler
    {
        [Entity(Scope = "linq3")]
        public class Dict
        {
            [AutoId]
            public int ID { get; set; }

            [EntityProperty]
            public string Name { get; set; }
        };

        [Entity(Scope = "linq3")]
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

            [EntityProperty]
            public Guid GuidValue { get; set; }

            [EntityProperty]
            public int? NullableIntValue { get; set; }

            [EntityProperty]
            public DateTime? NullableDataTime { get; set; }
        }

        [Fact]
        public void CountAll()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Count());

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder, "SELECT COUNT(*) AS @w FROM Dict AS @1");
        }

        [Fact]
        public void CountWhere1()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Where(e => e.ID > 5).Count());

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder, "SELECT COUNT(*) AS @w FROM Dict AS @1 WHERE (@1.id > @p)");
        }

        [Fact]
        public void CountWhere2()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Count(e => e.ID > 5));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder, "SELECT COUNT(*) AS @w FROM Dict AS @1 WHERE (@1.id > @p)");
        }

        [Fact]
        public void CountGroupBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Count() }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder, 
                "SELECT @2.id AS @w, COUNT(*) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id");
        }

        [Fact]
        public void CountGroupBy_ComplexKey()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => new { RefId = e.Reference.ID, Iv = e.IntValue }).Select(g => new { id = g.Key.RefId, iv = g.Key.Iv, c = g.Count() }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, @1.intvalue AS @w, COUNT(*) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id, @1.intvalue");
        }

        [Fact]
        public void CountGroupByWhereInCount()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Count(e => e.IntValue > 5) }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, COUNT(*) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE (@1.intvalue > @p) GROUP BY @2.id");
        }

        [Fact]
        public void CountGroupByWhere()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.Where(e => e.IntValue > 5).GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Count() }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, COUNT(*) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference WHERE (@1.intvalue > @p) GROUP BY @2.id");
        }

        [Fact]
        public void SumGroupBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Sum(o => o.IntValue) }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, SUM(@1.intvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id");
        }

        [Fact]
        public void AvgGroupBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Average(o => o.IntValue) }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, AVG(@1.intvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id");
        }

        [Fact]
        public void MinGroupBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Min(o => o.IntValue) }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, MIN(@1.intvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id");
        }

        [Fact]
        public void MaxGroupBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.GroupBy(e => e.Reference.ID).Select(g => new { id = g.Key, c = g.Max(o => o.IntValue) }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @2.id AS @w, MAX(@1.intvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference GROUP BY @2.id");
        }

        [Fact]
        public void MaxInt()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.Max(e => e.IntValue));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT MAX(@1.intvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference");
        }

        [Fact]
        public void MaxReal()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.Max(e => e.RealValue));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT MAX(@1.realvalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference");
        }

        [Fact]
        public void MaxDate()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Entity>(connection,
                e => e.Max(e => e.DateTimeValue));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT MAX(@1.datetimevalue) AS @w FROM Entity AS @1 INNER JOIN Dict AS @2 ON @2.id = @1.reference");
        }

        [Fact]
        public void SelectAll()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Where(e => e.ID > 5));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @1_id, @1.name AS @1_name FROM Dict AS @1 WHERE (@1.id > @p)");
        }

        [Fact]
        public void OrderBy()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.OrderBy(o => o.Name));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @1_id, @1.name AS @1_name FROM Dict AS @1 ORDER BY @1.name");
        }

        [Fact]
        public void OrderBy2()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.OrderBy(o => new { o.Name, o.ID }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @1_id, @1.name AS @1_name FROM Dict AS @1 ORDER BY @1.name, @1.id");
        }

        [Fact]
        public void Resultset1()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Select(v => new { v.ID, Name = v.Name.ToLower(), Name1 = SqlFunction.Left(v.Name, 5), Name2 = v.Name + v.ID.ToString() }));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @w, LOWER(@1.name) AS @w, LEFT(@1.name, @p) AS @w, @1.name || TOSTRING(@1.id) AS @w FROM Dict AS @1");
        }

        [Fact]
        public void Take()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Take(1));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @w, @1.name AS @w FROM Dict AS @1 LIMIT 1 ");
        }

        [Fact]
        public void Skip()
        {
            using var connection = new DummySqlConnection();
            var factory = new ExistingConnectionFactory(connection);
            var entityProvider = new QueryableEntityProvider(factory);

            var query = entityProvider.CompileToQuery<Dict>(connection, e => e.Skip(10));

            query.Query.Builder.PrepareQuery();
            var command = query.Query.Builder.Query;

            command.Should().MatchPattern(query.Query.Builder,
                "SELECT @1.id AS @w, @1.name AS @w FROM Dict AS @1 OFFSET 10 ");
        }
    }
}


