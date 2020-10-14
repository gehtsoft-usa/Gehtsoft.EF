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
    public class UpdateRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public UpdateRun()
        {
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=d:\testsql.db"); ;
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
        public void UpdateSuccess()
        {
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);
            array = result as List<object>;
            Int64 insertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];

            func = environment.Parse("test", $"SELECT * FROM Shipper LIMIT 1");
            result = func(null);
            array = result as List<object>;
            string shipperCompanyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string shipperPhone = (string)(array[0] as Dictionary<string, object>)["Phone"];

            func = environment.Parse("test", $"SELECT * FROM Employee WHERE PostalCode= '98122'");
            result = func(null);
            array = result as List<object>;
            string emploeeRegion = (string)(array[0] as Dictionary<string, object>)["Region"];

            func = environment.Parse("test", $"UPDATE Supplier SET " +
                $"ContactTitle = ContactTitle || ' SUPER', " +
                $"City = 'Omsk', " +
                $"Phone = (SELECT Phone FROM Shipper WHERE CompanyName='{shipperCompanyName}'), " +
                $"Region = 'was here: ' || (SELECT Region FROM Employee WHERE PostalCode= '98122') " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            array = result as List<object>;
            int updated = (int)(array[0] as Dictionary<string, object>)["Updated"];
            updated.Should().Be(1);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={insertedID}");
            result = func(null);
            array = result as List<object>;

            string contactTitle = (string)(array[0] as Dictionary<string, object>)["ContactTitle"];
            contactTitle.Should().Be("Wow SUPER");
            string city = (string)(array[0] as Dictionary<string, object>)["City"];
            city.Should().Be("Omsk");
            string phone = (string)(array[0] as Dictionary<string, object>)["Phone"];
            phone.Should().Be(shipperPhone);
            string region = (string)(array[0] as Dictionary<string, object>)["Region"];
            region.Should().Be("was here: " + emploeeRegion);
        }

        [Fact]
        public void UpdateParseError()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "UPDATE OrderD " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "UPDATE OrderDetail " +
                    "SET Disc = Discount * 1.1, UnitPrice = Unit * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "UPDATE OrderDetailing " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Order WHERE ShipCountry = 'UK')"
                )
            );
            Assert.Throws<SqlParserException>(() =>
                environment.Parse("test",
                    "UPDATE OrderDetail " +
                    "SET Discount= Discount * 1.1, UnitPrice = UnitPrice * 1.03 " +
                    "WHERE Order IN " +
                    "(SELECT OrderID FROM Orderi WHERE ShipC = 'UK')"
                )
            );
        }
    }
}
