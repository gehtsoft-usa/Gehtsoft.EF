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

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public class SetRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public SetRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:"); ;
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
        public void SetSuccess()
        {
            object result;
            List<object> array;

            DomBuilder.Parse("test",
                "DECLARE qqq AS STRING;" +
                "SET qqq = 'u';" +
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE ?qqq || '%' " +
                "GROUP BY Country"
            );
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            array.Count.Should().Be(2);

            DomBuilder.Parse("test",
                "SET qqq = ?qqq || 'K';" +
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE LOWER(?qqq) || '%' " +
                "GROUP BY Country"
            );
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            array.Count.Should().Be(1);
        }
    }
}
