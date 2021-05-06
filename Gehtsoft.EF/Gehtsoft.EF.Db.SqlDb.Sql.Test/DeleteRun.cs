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
    public sealed class DeleteRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public DeleteRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:");
            connection = connectionFactory.GetConnection();
            Snapshot snapshot = new Snapshot();
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
        public void DeleteSuccess()
        {
            Func<IDictionary<string, object>, dynamic> func;
            dynamic result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countBefore = (int)(result[0].Total);

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);
            Int64 insertedID = (Int64)(result[0].LastInsertedId);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countAfterInsert = (int)(result[0].Total);
            countAfterInsert.Should().Be(countBefore + 1);

            func = environment.Parse("test", "DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            int deleted = (int)(result.Deleted);
            deleted.Should().Be(1);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countAfterDelete = (int)(result[0].Total);
            countAfterDelete.Should().Be(countBefore);

            func = environment.Parse("test", "DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            deleted = (int)(result.Deleted);
            deleted.Should().Be(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            int countAfterDelete1 = (int)(result[0].Total);
            countAfterDelete1.Should().Be(countBefore);
        }

        [Fact]
        public void DeleteParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "DELETE FROM OrderD " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "DELETE FROM OrderDetail " +
                    "WHERE Order IN " +
                    "(SELECT ShipCountry FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "DELETE FROM OrderDetail " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Orderi WHERE ShipC = 'UK')"
                )
            );
        }
    }
}
