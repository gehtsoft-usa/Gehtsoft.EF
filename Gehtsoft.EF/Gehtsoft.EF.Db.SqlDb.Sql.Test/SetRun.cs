using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq;
using System.Text;
using Xunit;
using System.Linq.Expressions;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public sealed class SetRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public SetRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:");
            Snapshot snapshot = new Snapshot();
            connection = connectionFactory.GetConnection();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();
            EntityFinder.EntityTypeInfo[] entities = EntityFinder.FindEntities(new Assembly[] { typeof(Snapshot).Assembly }, "northwind", false);
            DomBuilder = new SqlCodeDomBuilder();
            DomBuilder.Build(entities, "entities");
        }

        public void Dispose()
        {
            if (connectionFactory.NeedDispose)
                connection.Dispose();
        }

        [Fact]
        public void Set()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test",
                "DECLARE qqq AS STRING;" +
                "SET qqq = 'u';" +
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE ?qqq || '%' " +
                "GROUP BY Country"
            );
            result = func(null);
            array = result as List<object>;
            array.Count.Should().Be(2);

            func = environment.Parse("test",
                "SET qqq = 'u';" +
                "SET qqq = ?qqq || 'K';" +
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE LOWER(?qqq) || '%' " +
                "GROUP BY Country"
            );
            result = func(null);
            array = result as List<object>;
            array.Count.Should().Be(1);
        }

        [Fact]
        public void SetParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET qqq = UPPER(field) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET qqq = COUNT(*) + 1"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET qqq = UPPER(?mmm AS INTEGER) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "SET qqq = UPPER(?mmm) = 'WWWWW'"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                "DECLARE qqq AS STRING, mmm AS STRING;" +
                "SET qqq = UPPER(?mmm) = 'WWWWW';"
                )
            );
        }
    }
}
