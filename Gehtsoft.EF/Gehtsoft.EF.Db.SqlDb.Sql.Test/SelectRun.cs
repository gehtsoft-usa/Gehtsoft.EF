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
    public class SelectRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public SelectRun()
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
        public void SimpleSelectAll()
        {
            DomBuilder.Parse("test", "SELECT * FROM Category");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryID").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("Description").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectFields()
        {
            DomBuilder.Parse("test", "SELECT CategoryID AS Id, CategoryName FROM Category");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("Id").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectCount()
        {
            DomBuilder.Parse("test", "SELECT COUNT(*) AS Total FROM Category");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            ((int)(array[0] as Dictionary<string, object>)["Total"]).Should().Be(8);
        }

        [Fact]
        public void SimpleSelectAgg()
        {
            DomBuilder.Parse("test", "SELECT MAX(OrderDate) AS Max, MIN(OrderDate) AS Min FROM Order");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            DateTime max = (DateTime)(array[0] as Dictionary<string, object>)["Max"];
            DateTime min = (DateTime)(array[0] as Dictionary<string, object>)["Min"];
            (max > min).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectAggExpr()
        {
            DomBuilder.Parse("test", "SELECT MAX(Freight) AS Max, MAX(Freight) + 2.0 AS MaxIncreased FROM Order");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            double max = (double)(array[0] as Dictionary<string, object>)["Max"];
            double maxIncreased = (double)(array[0] as Dictionary<string, object>)["MaxIncreased"];
            (maxIncreased - max == 2.0).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectConcatExpr()
        {
            DomBuilder.Parse("test", "SELECT CompanyName || ' ' || ContactName AS Concatted, CompanyName, ContactName FROM Customer");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            string concatted = (string)(array[0] as Dictionary<string, object>)["Concatted"];
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string contactName = (string)(array[0] as Dictionary<string, object>)["ContactName"];
            (companyName + " "+ contactName == concatted).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectTrimExpr()
        {
            DomBuilder.Parse("test", "SELECT TRIM(' ' || CompanyName || ' ') AS Trimmed, CompanyName FROM Customer");
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;
            string trimmed = (string)(array[0] as Dictionary<string, object>)["Trimmed"];
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            (companyName == trimmed).Should().BeTrue();
        }

        [Fact]
        public void SimpleJoinedSelect()
        {
            DomBuilder.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID"
                );
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;

            int orderID = (int)(array[0] as Dictionary<string, object>)["ID"];
            (orderID > 0).Should().BeTrue();
            double quantity = (double)(array[0] as Dictionary<string, object>)["Quantity"];
            (quantity > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(array[0] as Dictionary<string, object>)["OrderDate"];
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(array[0] as Dictionary<string, object>)["FirstName"];
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();
        }

        [Fact]
        public void InnerJoinedSelectWithWhere()
        {
            DomBuilder.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID " +
                "WHERE Quantity > 100"
                );
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;

            foreach (object obj in array)
            {
                double quantity = (double)(obj as Dictionary<string, object>)["Quantity"];
                (quantity > 100.0).Should().BeTrue();
            }
        }

        [Fact]
        public void AutoJoinedSelectWithWhere()
        {
            DomBuilder.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "WHERE Quantity > 100"
                );
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;

            int orderID = (int)(array[0] as Dictionary<string, object>)["ID"];
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(array[0] as Dictionary<string, object>)["Quantity"];
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(array[0] as Dictionary<string, object>)["OrderDate"];
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(array[0] as Dictionary<string, object>)["FirstName"];
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            foreach (object obj in array)
            {
                double quantity = (double)(obj as Dictionary<string, object>)["Quantity"];
                (quantity > 100.0).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectWithOffsetLimit()
        {
            DomBuilder.Parse("test",
                "SELECT * " +
                "FROM OrderDetail " +
                "OFFSET 20 LIMIT 10"
                );
            object result = DomBuilder.Run(connection);
            List<object> array = result as List<object>;

            array.Count.Should().Be(10);
            int id = (int)(array[0] as Dictionary<string, object>)["Id"];
            id.Should().Be(21);
        }
    }
}
