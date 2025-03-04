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
    public sealed class SelectRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public SelectRun()
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
        public void SimpleSelectAll()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT * FROM Category");
            dynamic result = func(null);
            ((int)result.Count).Should().Be(8);
            (result[0] as IDictionary<string, object>).ContainsKey("CategoryID").Should().BeTrue();
            (result[0] as IDictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
            (result[0] as IDictionary<string, object>).ContainsKey("Description").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectFields()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT CategoryID AS Id, CategoryName FROM Category");
            dynamic result = func(null);
            ((int)result.Count).Should().Be(8);
            (result[0] as IDictionary<string, object>).ContainsKey("Id").Should().BeTrue();
            (result[0] as IDictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectCount()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Category");
            dynamic result = func(null);
            ((int)(result[0].Total)).Should().Be(8);
        }

        [Fact]
        public void SimpleSelectAgg()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT MAX(OrderDate) AS Max, MIN(OrderDate) AS Min FROM Order");
            dynamic result = func(null);
            DateTime max = (DateTime)(result[0].Max);
            DateTime min = (DateTime)(result[0].Min);
            (max > min).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectAggExpr()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT MAX(Freight) AS Max, MAX(Freight) + 2.0 AS MaxIncreased FROM Order");
            dynamic result = func(null);
            double max = (double)(result[0].Max);
            double maxIncreased = (double)(result[0].MaxIncreased);
            (maxIncreased - max == 2.0).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectConcatExpr()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT CompanyName || ' ' || ContactName AS Concatted, CompanyName, ContactName FROM Customer");
            dynamic result = func(null);
            string concatted = (string)(result[0].Concatted);
            string companyName = (string)(result[0].CompanyName);
            string contactName = (string)(result[0].ContactName);
            (companyName + " " + contactName == concatted).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectTrimExpr()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT TRIM(' ' || CompanyName || ' ') AS Trimmed, CompanyName FROM Customer");
            dynamic result = func(null);
            string trimmed = (string)(result[0].Trimmed);
            string companyName = (string)(result[0].CompanyName);
            (companyName == trimmed).Should().BeTrue();
        }

        [Fact]
        public void SimpleJoinedSelect()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID"
                );
            dynamic result = func(null);

            int orderID = (int)(result[0].ID);
            (orderID > 0).Should().BeTrue();
            double quantity = (double)(result[0].Quantity);
            (quantity > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(result[0].OrderDate);
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(result[0].CompanyName);
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(result[0].FirstName);
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();
        }

        [Fact]
        public void InnerJoinedSelectWithWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID " +
                "WHERE Quantity > 100"
                );
            dynamic result = func(null);

            foreach (object obj in result)
            {
                double quantity = (double)(obj as IDictionary<string, object>)["Quantity"];
                (quantity > 100.0).Should().BeTrue();
            }
        }

        [Fact]
        public void AutoJoinedSelectWithWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "WHERE Quantity > 100"
                );
            dynamic result = func(null);

            int orderID = (int)(result[0].ID);
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(result[0].Quantity);
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(result[0].OrderDate);
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(result[0].CompanyName);
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(result[0].FirstName);
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            foreach (object obj in result)
            {
                double quantity = (double)(obj as IDictionary<string, object>)["Quantity"];
                (quantity > 100.0).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectWithOffsetLimit()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT * " +
                "FROM OrderDetail " +
                "OFFSET 0 LIMIT 1"
                );
            dynamic result = func(null);

            ((int)result.Count).Should().Be(1);
            int idFirst = (int)(result[0].Id);

            func = environment.Parse("test",
                "SELECT * " +
                "FROM OrderDetail " +
                "OFFSET 20 LIMIT 10"
                );
            result = func(null);
            ((int)result.Count).Should().Be(10);
            int id = (int)(result[0].Id);
            id.Should().Be(idFirst + 20);
        }

        [Fact]
        public void AutoJoinedSelectWithOrdering()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity+1 AS Q, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "WHERE Q > 10 " +
                "ORDER BY Quantity DESC, Order.OrderDate DESC"
                );
            dynamic result = func(null);

            int orderID = (int)(result[0].ID);
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(result[0].Q);
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(result[0].OrderDate);
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(result[0].CompanyName);
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(result[0].FirstName);
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            double max = double.MaxValue;
            foreach (dynamic obj in result)
            {
                double quantity = (double)(obj as IDictionary<string, object>)["Q"];
                (quantity <= max).Should().BeTrue();
                max = quantity;
            }
        }

        [Fact]
        public void SelectWithGroupAndOrder()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE 'u' || '%' " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            dynamic result = func(null);

            int cstmCounter = (int)(result[0].CustomersInCountry);
            (cstmCounter > 0).Should().BeTrue();
            string country = (string)(result[0].Country);
            string.IsNullOrWhiteSpace(country).Should().BeFalse();

            int max = int.MaxValue;
            foreach (dynamic obj in result)
            {
                int count = (int)(obj as IDictionary<string, object>)["CustomersInCountry"];
                (count <= max).Should().BeTrue();
                max = count;
                string countryName = (string)(obj as IDictionary<string, object>)["Country"];
                countryName.StartsWith("u", StringComparison.OrdinalIgnoreCase);
                func = environment.Parse("test", $"SELECT COUNT(*) AS q FROM Customer WHERE UPPER(Country) = UPPER('{countryName}')");
                dynamic resultInner = func(null);
                int countFound = (int)(resultInner[0] as IDictionary<string, object>)["q"];
                countFound.Should().Be(count);
            }
        }

        [Fact]
        public void AutoJoinedSelectWithAbs()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity AS Q, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "WHERE ABS(-Q) > 100 " +
                "ORDER BY Q DESC"
                );
            dynamic result = func(null);

            int orderID = (int)(result[0].ID);
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(result[0].Q);
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(result[0].OrderDate);
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(result[0].CompanyName);
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(result[0].FirstName);
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            double max = double.MaxValue;
            foreach (dynamic obj in result)
            {
                double quantity = (double)(obj as IDictionary<string, object>)["Q"];
                (quantity > 100.0).Should().BeTrue();
                (quantity <= max).Should().BeTrue();
                max = quantity;
            }
        }

        [Fact]
        public void SelectWithStartsWith()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE STARTSWITH(LOWER(Country), 'u') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);

            foreach (dynamic obj in result)
            {
                string countryName = (string)(obj as IDictionary<string, object>)["Country"];
                countryName.StartsWith("u", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectWithEndsWith()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE ENDSWITH(LOWER(Country), 'a') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);

            foreach (dynamic obj in result)
            {
                string countryName = (string)(obj as IDictionary<string, object>)["Country"];
                countryName.EndsWith("a", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectWithContains()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE CONTAINS(LOWER(Country), 'gent') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);

            foreach (dynamic obj in result)
            {
                string countryName = (string)(obj as IDictionary<string, object>)["Country"];
                countryName.Contains("gent", StringComparison.OrdinalIgnoreCase).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectIn1()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            dynamic result = func(null);
            int total = (int)(result[0].Total);
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Country IN (SELECT Country FROM Supplier)");
            result = func(null);
            int totalIn = (int)(result[0].Total);
            totalIn.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Country NOT IN (SELECT Country FROM Supplier)");
            result = func(null);
            int totalNotIn = (int)(result[0].Total);
            totalNotIn.Should().BeGreaterThan(0);

            total.Should().Be(totalIn + totalNotIn);
        }

        [Fact]
        public void SelectIn2()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            dynamic result = func(null);
            int total = (int)(result[0].Total);
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE UPPER(Country) IN ('USA', 'AUSTRALIA')");
            result = func(null);
            int totalIn = (int)(result[0].Total);
            totalIn.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE UPPER(Country) NOT IN ('USA', 'AUSTRALIA')");
            result = func(null);
            int totalNotIn = (int)(result[0].Total);
            totalNotIn.Should().BeGreaterThan(0);

            total.Should().Be(totalIn + totalNotIn);
        }

        [Fact]
        public void SelectIsNull()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            dynamic result = func(null);
            int total = (int)(result[0].Total);
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Region IS NULL");
            result = func(null);
            int totalNull = (int)(result[0].Total);
            totalNull.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Region IS NOT NULL");
            result = func(null);
            int totalNotNull = (int)(result[0].Total);
            totalNotNull.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(Region) AS Total FROM Customer");
            result = func(null);
            int totalCountNotNull = (int)(result[0].Total);
            totalCountNotNull.Should().BeGreaterThan(0);
            totalCountNotNull.Should().BeLessThanOrEqualTo(totalNotNull);

            total.Should().Be(totalNull + totalNotNull);
        }

        [Fact]
        public void SelectWithNotDeclaredParameter()
        {
            var env = DomBuilder.NewEnvironment(connection);
            var statement = env.Parse("query", "SELECT * FROM Category WHERE CategoryID > ?CategoryID");
            // not declared ?CategoryID anyway
            dynamic result = statement(new Dictionary<string, object> { { "CategoryID", 3 } });
            ((int)result.Count).Should().BeGreaterThan(0);

            foreach (dynamic obj in result)
            {
                int categoryID = (int)(obj as IDictionary<string, object>)["CategoryID"];
                (categoryID > 3).Should().BeTrue();
            }
        }
    }
}
