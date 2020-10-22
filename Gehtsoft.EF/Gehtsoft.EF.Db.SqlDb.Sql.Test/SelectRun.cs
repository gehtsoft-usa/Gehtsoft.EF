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
    public class SelectRun : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private ISqlDbConnectionFactory connectionFactory;
        private SqlDbConnection connection;

        public SelectRun()
        {
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.MSSQL, @"server=.\SQLEXPRESSTEO;Connection Lifetime=900;Load Balance Timeout=60;Max Pool Size=25;Pooling=true;Integrated Security=SSPI;"); ;
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.POSTGRES, @"server=127.0.0.1;database=test;user id=postgres;password=hurnish1962;"); ;
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.MYSQL, @"server=127.0.0.1;Database=test;Uid=root;Pwd=root;port=3306;AllowUserVariables=True;default command timeout=0"); ;
            //string tns = "(DESCRIPTION = (ADDRESS_LIST = (ADDRESS = (PROTOCOL = TCP)(HOST = 192.168.1.4)(PORT = 1521)))(CONNECT_DATA = (SERVER = DEDICATED)(SID = XE)))";
            //connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.ORACLE, $"Data Source={tns};user id=C##TEST;password=test;");
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, @"Data Source=:memory:"); ;
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
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT * FROM Category");
            object result = func(null);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryID").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("Description").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectFields()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT CategoryID AS Id, CategoryName FROM Category");
            object result = func(null);
            List<object> array = result as List<object>;
            array.Count().Should().Be(8);
            (array[0] as Dictionary<string, object>).ContainsKey("Id").Should().BeTrue();
            (array[0] as Dictionary<string, object>).ContainsKey("CategoryName").Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectCount()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Category");
            object result = func(null);
            List<object> array = result as List<object>;
            ((int)(array[0] as Dictionary<string, object>)["Total"]).Should().Be(8);
        }

        [Fact]
        public void SimpleSelectAgg()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT MAX(OrderDate) AS Max, MIN(OrderDate) AS Min FROM Order");
            object result = func(null);
            List<object> array = result as List<object>;
            DateTime max = (DateTime)(array[0] as Dictionary<string, object>)["Max"];
            DateTime min = (DateTime)(array[0] as Dictionary<string, object>)["Min"];
            (max > min).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectAggExpr()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT MAX(Freight) AS Max, MAX(Freight) + 2.0 AS MaxIncreased FROM Order");
            object result = func(null);
            List<object> array = result as List<object>;
            double max = (double)(array[0] as Dictionary<string, object>)["Max"];
            double maxIncreased = (double)(array[0] as Dictionary<string, object>)["MaxIncreased"];
            (maxIncreased - max == 2.0).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectConcatExpr()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT CompanyName || ' ' || ContactName AS Concatted, CompanyName, ContactName FROM Customer");
            object result = func(null);
            List<object> array = result as List<object>;
            string concatted = (string)(array[0] as Dictionary<string, object>)["Concatted"];
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string contactName = (string)(array[0] as Dictionary<string, object>)["ContactName"];
            (companyName + " " + contactName == concatted).Should().BeTrue();
        }

        [Fact]
        public void SimpleSelectTrimExpr()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test", "SELECT TRIM(' ' || CompanyName || ' ') AS Trimmed, CompanyName FROM Customer");
            object result = func(null);
            List<object> array = result as List<object>;
            string trimmed = (string)(array[0] as Dictionary<string, object>)["Trimmed"];
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            (companyName == trimmed).Should().BeTrue();
        }

        [Fact]
        public void SimpleJoinedSelect()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID"
                );
            object result = func(null);
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
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = ID " +
                "INNER JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "INNER JOIN Employee ON Order.Employee = Employee.EmployeeID " +
                "WHERE Quantity > 100"
                );
            object result = func(null);
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
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT OrderID AS ID, Quantity, " +
                "Order.OrderDate, Customer.CompanyName, Employee.FirstName " +
                "FROM OrderDetail " +
                "AUTO JOIN Order " +
                "AUTO JOIN Customer " +
                "AUTO JOIN Employee " +
                "WHERE Quantity > 100"
                );
            object result = func(null);
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
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT * " +
                "FROM OrderDetail " +
                "OFFSET 0 LIMIT 1"
                );
            object result = func(null);
            List<object> array = result as List<object>;

            array.Count.Should().Be(1);
            int idFirst = (int)(array[0] as Dictionary<string, object>)["Id"];

            func = environment.Parse("test",
                "SELECT * " +
                "FROM OrderDetail " +
                "OFFSET 20 LIMIT 10"
                );
            result = func(null);
            array = result as List<object>;

            array.Count.Should().Be(10);
            int id = (int)(array[0] as Dictionary<string, object>)["Id"];
            id.Should().Be(idFirst + 20);
        }

        [Fact]
        public void AutoJoinedSelectWithOrdering()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

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
            object result = func(null);
            List<object> array = result as List<object>;

            int orderID = (int)(array[0] as Dictionary<string, object>)["ID"];
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(array[0] as Dictionary<string, object>)["Q"];
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(array[0] as Dictionary<string, object>)["OrderDate"];
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(array[0] as Dictionary<string, object>)["FirstName"];
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            double max = double.MaxValue;
            foreach (object obj in array)
            {
                double quantity = (double)(obj as Dictionary<string, object>)["Q"];
                (quantity <= max).Should().BeTrue();
                max = quantity;
            }
        }

        [Fact]
        public void SelectWithGroupAndOrder()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE LOWER(Country) LIKE 'u' || '%' " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            object result = func(null);
            List<object> array = result as List<object>;

            int cstmCounter = (int)(array[0] as Dictionary<string, object>)["CustomersInCountry"];
            (cstmCounter > 0).Should().BeTrue();
            string country = (string)(array[0] as Dictionary<string, object>)["Country"];
            string.IsNullOrWhiteSpace(country).Should().BeFalse();

            int max = int.MaxValue;
            foreach (object obj in array)
            {
                int count = (int)(obj as Dictionary<string, object>)["CustomersInCountry"];
                (count <= max).Should().BeTrue();
                max = count;
                string countryName = (string)(obj as Dictionary<string, object>)["Country"];
                countryName.ToLower().StartsWith("u");
                func = environment.Parse("test", $"SELECT COUNT(*) AS q FROM Customer WHERE UPPER(Country) = UPPER('{countryName}')");
                object resultInner = func(null);
                List<object> arrayInner = resultInner as List<object>;
                int countFound = (int)(arrayInner[0] as Dictionary<string, object>)["q"];
                countFound.Should().Be(count);
            }
        }

        [Fact]
        public void AutoJoinedSelectWithAbs()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

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
            object result = func(null);
            List<object> array = result as List<object>;

            int orderID = (int)(array[0] as Dictionary<string, object>)["ID"];
            (orderID > 0).Should().BeTrue();
            double quantity1 = (double)(array[0] as Dictionary<string, object>)["Q"];
            (quantity1 > 0.0).Should().BeTrue();
            DateTime orderDate = (DateTime)(array[0] as Dictionary<string, object>)["OrderDate"];
            (orderDate > DateTime.MinValue).Should().BeTrue();
            string companyName = (string)(array[0] as Dictionary<string, object>)["CompanyName"];
            string.IsNullOrWhiteSpace(companyName).Should().BeFalse();
            string firstName = (string)(array[0] as Dictionary<string, object>)["FirstName"];
            string.IsNullOrWhiteSpace(firstName).Should().BeFalse();

            double max = double.MaxValue;
            foreach (object obj in array)
            {
                double quantity = (double)(obj as Dictionary<string, object>)["Q"];
                (quantity > 100.0).Should().BeTrue();
                (quantity <= max).Should().BeTrue();
                max = quantity;
            }
        }

        [Fact]
        public void SelectWithStartsWith()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE STARTSWITH(LOWER(Country), 'u') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            object result = func(null);
            List<object> array = result as List<object>;
            array.Count.Should().BeGreaterThan(0);

            foreach (object obj in array)
            {
                string countryName = (string)(obj as Dictionary<string, object>)["Country"];
                countryName.ToLower().StartsWith("u");
            }
        }

        [Fact]
        public void SelectWithEndsWith()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE ENDSWITH(LOWER(Country), 'a') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            object result = func(null);
            List<object> array = result as List<object>;
            array.Count.Should().BeGreaterThan(0);

            foreach (object obj in array)
            {
                string countryName = (string)(obj as Dictionary<string, object>)["Country"];
                countryName.ToLower().EndsWith("a");
            }
        }

        [Fact]
        public void SelectWithContains()
        {
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(CustomerID) AS CustomersInCountry, Country " +
                "FROM Customer " +
                "WHERE CONTAINS(LOWER(Country), 'gent') " +
                "GROUP BY Country " +
                "ORDER BY COUNT(CustomerID) DESC"
                );
            object result = func(null);
            List<object> array = result as List<object>;
            array.Count.Should().BeGreaterThan(0);

            foreach (object obj in array)
            {
                string countryName = (string)(obj as Dictionary<string, object>)["Country"];
                countryName.ToLower().Contains("gent");
            }
        }

        [Fact]
        public void SelectIn1()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            object result = func(null);
            List<object> array = result as List<object>;
            int total = (int)(array[0] as Dictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Country IN (SELECT Country FROM Supplier)");
            result = func(null);
            array = result as List<object>;
            int totalIn = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalIn.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Country NOT IN (SELECT Country FROM Supplier)");
            result = func(null);
            array = result as List<object>;
            int totalNotIn = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalNotIn.Should().BeGreaterThan(0);

            total.Should().Be(totalIn + totalNotIn);
        }

        [Fact]
        public void SelectIn2()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            object result = func(null);
            List<object> array = result as List<object>;
            int total = (int)(array[0] as Dictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE UPPER(Country) IN ('USA', 'AUSTRALIA')");
            result = func(null);
            array = result as List<object>;
            int totalIn = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalIn.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE UPPER(Country) NOT IN ('USA', 'AUSTRALIA')");
            result = func(null);
            array = result as List<object>;
            int totalNotIn = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalNotIn.Should().BeGreaterThan(0);

            total.Should().Be(totalIn + totalNotIn);
        }

        [Fact]
        public void SelectIsNull()
        {
            Func<IDictionary<string, object>, object> func;
            SqlCodeDomEnvironment environment  = DomBuilder.NewEnvironment(connection);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer");
            object result = func(null);
            List<object> array = result as List<object>;
            int total = (int)(array[0] as Dictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Region IS NULL");
            result = func(null);
            array = result as List<object>;
            int totalNull = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalNull.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(*) AS Total FROM Customer WHERE Region IS NOT NULL");
            result = func(null);
            array = result as List<object>;
            int totalNotNull = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalNotNull.Should().BeGreaterThan(0);

            func = environment.Parse("test", "SELECT COUNT(Region) AS Total FROM Customer");
            result = func(null);
            array = result as List<object>;
            int totalCountNotNull = (int)(array[0] as Dictionary<string, object>)["Total"];
            totalCountNotNull.Should().BeGreaterThan(0);
            totalCountNotNull.Should().BeLessOrEqualTo(totalNotNull);

            total.Should().Be(totalNull + totalNotNull);
        }
    }
}
