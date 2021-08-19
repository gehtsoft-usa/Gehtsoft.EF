using System;
using System.Data;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.Query
{
    public sealed class SqlInjectPolicy : IDisposable
    {
        private readonly bool mOriginalPolicy;

        public SqlInjectPolicy()
        {
            mOriginalPolicy = SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries;
        }

        [Fact]
        public void DefaultPolicy()
        {
            mOriginalPolicy.Should().BeTrue();
        }

        [Fact]
        public void ProtectQuote()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());
            ((Action)(() => connection.GetQuery("select * from a where a.a = 'literal'"))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ProtectDoubleQuote()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());

            ((Action)(() => connection.GetQuery("select * from a where a.a = \"literal\""))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Success()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());

            ((Action)(() => connection.GetQuery("select * from a where a.a = :arg"))).Should().NotThrow();
        }

        [Fact]
        public void InSelectBuilder_Resultset()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());
            var customer = AllEntities.Get<Customer>().TableDescriptor;
            var builder = connection.GetSelectQueryBuilder(customer);

            //incorrect cases
            ((Action)(() => builder.AddToResultset(customer, "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(customer, "literal;"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(customer[0], "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(customer[0], "literal;"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(customer[0], builder.Entities[0], "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(AggFn.Max, customer[0], "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(AggFn.Max, customer[0], builder.Entities[0], "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddToResultset(AggFn.Count, "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddExpressionToResultset("a + b", DbType.Int32, false, "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddExpressionToResultset("a;", DbType.Int32, false, "'literal'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddExpressionToResultset("a", DbType.Int32, false, "literal;"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddExpressionToResultset("'a' + b", DbType.Int32, false, "literal"))).Should().Throw<ArgumentException>();

            //correct cases
            ((Action)(() => builder.AddToResultset(customer, "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddToResultset(customer[0], "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddToResultset(customer[0], builder.Entities[0], "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddToResultset(AggFn.Max, customer[0], "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddToResultset(AggFn.Max, customer[0], builder.Entities[0], "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddToResultset(AggFn.Count, "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddExpressionToResultset("a + b", DbType.Int32, false, "literal"))).Should().NotThrow();
            ((Action)(() => builder.AddExpressionToResultset("a + b", DbType.Int32))).Should().NotThrow();
        }

        [Fact]
        public void InSelectBuilder_OrderAndGroup()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());
            var customer = AllEntities.Get<Customer>().TableDescriptor;
            var builder = connection.GetSelectQueryBuilder(customer);

            //incorrect cases
            ((Action)(() => builder.AddOrderByExpr("a + 'a'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddGroupByExpr("a + 'a'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.AddGroupByExpr("a;"))).Should().Throw<ArgumentException>();

            //correct cases
            ((Action)(() => builder.AddOrderByExpr("a + a"))).Should().NotThrow();
            ((Action)(() => builder.AddGroupByExpr("a + a"))).Should().NotThrow();
        }

        [Fact]
        public void ConditionalQuery_Where()
        {
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());
            var customer = AllEntities.Get<Customer>().TableDescriptor;
            var builder = connection.GetSelectQueryBuilder(customer);

            //incorrect cases
            ((Action)(() => builder.Where.Property(customer[0]).Eq().Value("'abc'"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.Where.Property(customer[0]).Eq().Value("abc"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.Where.Raw("a'bcd'").Le().Parameter("x"))).Should().Throw<ArgumentException>();
            ((Action)(() => builder.Where.Raw(";abcd").Le().Parameter("x"))).Should().Throw<ArgumentException>();

            //correct cases
            ((Action)(() => builder.Where.Property(customer[0]).Eq().Value(123))).Should().NotThrow();
            ((Action)(() => builder.Where.Raw("abcd").Le().Parameter("x"))).Should().NotThrow();
        }

        [Fact]
        public void Override()
        {
            SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;
            DummySqlConnection connection = new DummySqlConnection(new DummyDbConnection());
            ((Action)(() => connection.GetQuery("select * from a where a.a = 'literal'"))).Should().NotThrow();
        }

        public void Dispose()
        {
            SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = mOriginalPolicy;
        }
    }
}
