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
    public class DeleteRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public DeleteRun()
        {
            //string tns = "(DESCRIPTION = (ADDRESS_LIST = (ADDRESS = (PROTOCOL = TCP)(HOST = 192.168.1.4)(PORT = 1521)))(CONNECT_DATA = (SERVER = DEDICATED)(SID = XE)))";
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.ORACLE, $"Data Source={tns};user id=C##TEST;password=test;");
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:"); ;
            connection = connectionFactory.GetConnection();
            Snapshot snapshot = new Snapshot();
            snapshot.CreateAsync(connection).ConfigureAwait(true).GetAwaiter().GetResult();

            //// trick Oracle's 'nextval'
            ////----------------------------------------------------------------------------
            //bool prot = SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries;
            //SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;
            //using (SqlDbQuery q1 = connection.GetQuery("BEGIN \r\nEXECUTE IMMEDIATE 'DROP SEQUENCE nw_suppliers_supplierID';\r\nEXECUTE IMMEDIATE 'CREATE SEQUENCE nw_suppliers_supplierID START WITH 100';\r\nEND; \r\n"))
            //{
            //    q1.ExecuteNoData();
            //}
            //SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = prot;
            ////----------------------------------------------------------------------------

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
            Func<IDictionary<string, object>, object> func;
            object result;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);
            List<object> array;

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countBefore = (int)(array[0] as Dictionary<string, object>)["Total"];

            func = environment.Parse("test",
                "INSERT INTO Supplier " +
                "(CompanyName, ContactName, ContactTitle, Address, City, Region, PostalCode, Country) " +
                "VALUES " +
                "('Gehtsoft', 'Just Gehtsoft', 'Wow', '1-st street 1', 'Moscow', 'Siberia', '644000', 'Russia')"
            );
            result = func(null);
            array = result as List<object>;
            Int64 insertedID = (Int64)(array[0] as Dictionary<string, object>)["LastInsertedId"];

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countAfterInsert = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterInsert.Should().Be(countBefore + 1);

            func = environment.Parse("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            array = result as List<object>;
            int deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(1);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countAfterDelete = (int)(array[0] as Dictionary<string, object>)["Total"];
            countAfterDelete.Should().Be(countBefore);

            func = environment.Parse("test", $"DELETE FROM Supplier " +
                $"WHERE SupplierID={insertedID}");
            result = func(null);
            array = result as List<object>;
            deleted = (int)(array[0] as Dictionary<string, object>)["Deleted"];
            deleted.Should().Be(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Supplier");
            result = func(null);
            array = result as List<object>;
            int countAfterDelete1 = (int)(array[0] as Dictionary<string, object>)["Total"];
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
