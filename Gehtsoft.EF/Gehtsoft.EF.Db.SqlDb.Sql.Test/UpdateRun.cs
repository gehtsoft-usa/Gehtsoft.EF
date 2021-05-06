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
    public sealed class UpdateRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public UpdateRun()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:");
            connection = connectionFactory.GetConnection();
            Snapshot snapshot = new Snapshot();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();

            if (connection.ConnectionType == UniversalSqlDbFactory.ORACLE)
            {
                // trick Oracle's 'nextval'
                //----------------------------------------------------------------------------
                bool prot = SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries;
                SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;

                int start = 0;
                using (SqlDbQuery q1 = connection.GetQuery("SELECT MAX(supplierID) FROM nw_suppliers"))
                {
                    q1.ExecuteReader();
                    while (q1.ReadNext())
                    {
                        object v = q1.GetValue(0);
                        start = (int)Convert.ChangeType(v, typeof(int));
                    }
                }
                using (SqlDbQuery q1 = connection.GetQuery($"BEGIN \r\nEXECUTE IMMEDIATE 'DROP SEQUENCE nw_suppliers_supplierID';\r\nEXECUTE IMMEDIATE 'CREATE SEQUENCE nw_suppliers_supplierID START WITH {start + 1}';\r\nEND; \r\n"))
                {
                    q1.ExecuteNoData();
                }
                SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = prot;
            }
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
            Func<IDictionary<string, object>, dynamic> func;
            dynamic result;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);
            Int64 insertedID = (Int64)result[0].LastInsertedId;

            func = environment.Parse("test", "SELECT * FROM Shipper LIMIT 1");
            result = func(null);
            string shipperCompanyName = (string)(result[0].CompanyName);
            string shipperPhone = (string)(result[0].Phone);

            func = environment.Parse("test", "SELECT * FROM Employee WHERE PostalCode= '98122'");
            result = func(null);
            string emploeeRegion = (string)(result[0].Region);

            func = environment.Parse("test", "UPDATE Supplier SET " +
                "ContactTitle = ContactTitle || ' SUPER', " +
                "City = 'Omsk', " +
                $"Phone = (SELECT Phone FROM Shipper WHERE CompanyName='{shipperCompanyName}'), " +
                "Region = 'was here: ' || (SELECT Region FROM Employee WHERE PostalCode= '98122') " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            int updated = (int)(result.Updated);
            updated.Should().Be(1);

            func = environment.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={insertedID}");
            result = func(null);

            string contactTitle = (string)(result[0].ContactTitle);
            contactTitle.Should().Be("Wow SUPER");
            string city = (string)(result[0].City);
            city.Should().Be("Omsk");
            string phone = (string)(result[0].Phone);
            phone.Should().Be(shipperPhone);
            string region = (string)(result[0].Region);
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
