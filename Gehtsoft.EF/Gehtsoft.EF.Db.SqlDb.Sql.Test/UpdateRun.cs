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
            object result;
            List<object> array;

            DomBuilder.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            Int64 insertedID = (Int64)(array[0]);

            DomBuilder.Parse("test", $"SELECT * FROM Shipper LIMIT 1");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            string shipperCompanyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string shipperPhone = (string)(array[0] as Dictionary<string, object>)["Phone"];

            DomBuilder.Parse("test", $"SELECT * FROM Employee WHERE PostalCode= '98122'");
            result = DomBuilder.Run(connection);
            array = result as List<object>;
            string emploeeRegion = (string)(array[0] as Dictionary<string, object>)["Region"];

            DomBuilder.Parse("test", $"UPDATE Supplier SET "+
                $"ContactTitle = ContactTitle || ' SUPER', " +
                $"City = 'Omsk', " +
                $"Phone = (SELECT Phone FROM Shipper WHERE CompanyName='{shipperCompanyName}'), " +
                $"Region = 'was here: ' || (SELECT Region FROM Employee WHERE PostalCode= '98122') " +
                $"WHERE SupplierID={insertedID}");
            result = DomBuilder.Run(connection);

            DomBuilder.Parse("test", $"SELECT * FROM Supplier WHERE SupplierID={insertedID}");
            result = DomBuilder.Run(connection);
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
    }
}
