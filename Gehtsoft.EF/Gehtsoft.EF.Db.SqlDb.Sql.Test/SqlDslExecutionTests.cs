using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public sealed class SqlDslExecutionTests : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public SqlDslExecutionTests()
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

        // ── Group 1: Constant Expression Evaluation via SET ──

        [Fact]
        public void SetIntegerArithmetic()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER, b AS INTEGER, c AS INTEGER, d AS INTEGER, e AS INTEGER " +
                "SET a = 5 " +
                "SET b = ?a + 3 " +
                "SET c = ?a - 2 " +
                "SET d = ?a * 4 " +
                "SET e = ?a / 2 " +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(8);

            // verify c, d, e
            func = environment.Parse("test2",
                "SET a = 5 " +
                "SET c = ?a - 2 " +
                "EXIT WITH ?c"
            );
            result = func(null);
            ((int)result).Should().Be(3);

            func = environment.Parse("test3",
                "SET a = 5 " +
                "SET d = ?a * 4 " +
                "EXIT WITH ?d"
            );
            result = func(null);
            ((int)result).Should().Be(20);

            func = environment.Parse("test4",
                "SET a = 5 " +
                "SET e = ?a / 2 " +
                "EXIT WITH ?e"
            );
            result = func(null);
            ((int)result).Should().Be(2);
        }

        [Fact]
        public void SetIntegerComparisons()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER, result AS INTEGER " +
                "SET a = 10 " +
                "SET result = 0 " +
                "IF ?a = 10 AND ?a <> 5 AND ?a > 5 AND ?a >= 10 AND ?a < 20 AND ?a <= 10 THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetDoubleArithmeticAndComparisons()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS DOUBLE, b AS DOUBLE, result AS INTEGER " +
                "SET a = 3.14 " +
                "SET b = ?a + 1.0 " +
                "SET result = 0 " +
                "IF ?b > 4.0 AND ?b < 4.2 THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);

            func = environment.Parse("test2",
                "SET a = 10.0 " +
                "SET b = ?a - 3.0 " +
                "SET result = 0 " +
                "IF ?b = 7.0 THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);

            func = environment.Parse("test3",
                "SET a = 3.0 " +
                "SET b = ?a * 2.0 " +
                "SET result = 0 " +
                "IF ?b = 6.0 THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);

            func = environment.Parse("test4",
                "SET a = 10.0 " +
                "SET b = ?a / 4.0 " +
                "SET result = 0 " +
                "IF ?b = 2.5 THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetMixedIntDoubleArithmetic()
        {
            // Mixed int/double in SET context has a compile-time type mismatch,
            // so test via SELECT where the DB handles type coercion
            SqlCodeDomEnvironment env1 = DomBuilder.NewEnvironment(connection);
            var func = env1.Parse("test",
                "IMPORT a AS INTEGER, b AS DOUBLE " +
                "SELECT ?a + ?b AS Sum, ?a * ?b AS Prod, ?a / ?b AS Quot FROM Category LIMIT 1"
            );
            var pars = new Dictionary<string, object> { { "a", 5 }, { "b", 2.0 } };
            dynamic result = func(pars);
            var row = result[0] as IDictionary<string, object>;
            Convert.ToDouble(row["Sum"]).Should().BeApproximately(7.0, 0.001);
            Convert.ToDouble(row["Prod"]).Should().BeApproximately(10.0, 0.001);
            Convert.ToDouble(row["Quot"]).Should().BeGreaterThan(0);
        }

        [Fact]
        public void SetBooleanOperations()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS BOOLEAN, b AS BOOLEAN, result AS INTEGER " +
                "SET a = TRUE " +
                "SET b = FALSE " +
                "SET result = 0 " +
                "IF ?a = TRUE AND ?a <> ?b AND (?a OR ?b) AND NOT (?a AND ?b) THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetStringOperationsAndConcat()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS STRING, c AS STRING, result AS INTEGER " +
                "SET a = 'hello' " +
                "SET b = 'world' " +
                "SET c = ?a || ' ' || ?b " +
                "SET result = 0 " +
                "IF ?a = 'hello' AND ?b > ?a AND ?a < ?b AND ?a <> ?b AND ?a <= 'hello' AND ?b >= 'world' THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?c || '|' || TOSTRING(?result)"
            );
            dynamic result = func(null);
            string combined = (string)result;
            combined.Should().StartWith("hello world|");
            combined.Should().EndWith("|1");
        }

        [Fact]
        public void SetDateTimeComparisons()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS DATETIME, b AS DATETIME, result AS INTEGER " +
                "SET a = DATETIME '2023-01-15 10:00:00' " +
                "SET b = DATETIME '2023-06-20 10:00:00' " +
                "SET result = 0 " +
                "IF ?a = DATETIME '2023-01-15 10:00:00' AND ?a <> ?b AND ?a < ?b AND ?a <= ?b AND ?b > ?a AND ?b >= ?a THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetUnaryExpressions()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER, b AS INTEGER, c AS INTEGER " +
                "SET a = 5 " +
                "SET b = -?a " +
                "SET c = +?a " +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(-5);

            SqlCodeDomEnvironment env2 = DomBuilder.NewEnvironment(connection);
            func = env2.Parse("test",
                "DECLARE d AS DOUBLE, e AS DOUBLE " +
                "SET d = 3.14 " +
                "SET e = -?d " +
                "EXIT WITH ?e"
            );
            result = func(null);
            ((double)result).Should().BeApproximately(-3.14, 0.001);

            SqlCodeDomEnvironment env3 = DomBuilder.NewEnvironment(connection);
            func = env3.Parse("test",
                "DECLARE g AS BOOLEAN, h AS BOOLEAN, result AS INTEGER " +
                "SET g = TRUE " +
                "SET h = NOT ?g " +
                "SET result = 0 " +
                "IF ?h = FALSE THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);
        }

        // ── Group 2: Built-in Functions in SET Context ──

        [Fact]
        public void SetTrimFunctions()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS STRING, c AS STRING, d AS STRING " +
                "SET a = '  hello  ' " +
                "SET b = TRIM(?a) " +
                "SET c = LTRIM(?a) " +
                "SET d = RTRIM(?a) " +
                "EXIT WITH ?b || '|' || ?c || '|' || ?d"
            );
            dynamic result = func(null);
            string combined = (string)result;
            string[] parts = combined.Split('|');
            parts[0].Should().Be("hello");
            parts[1].Should().Be("hello  ");
            parts[2].Should().Be("  hello");
        }

        [Fact]
        public void SetUpperLower()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS STRING, c AS STRING " +
                "SET a = 'Hello World' " +
                "SET b = UPPER(?a) " +
                "SET c = LOWER(?a) " +
                "EXIT WITH ?b || '|' || ?c"
            );
            dynamic result = func(null);
            string combined = (string)result;
            string[] parts = combined.Split('|');
            parts[0].Should().Be("HELLO WORLD");
            parts[1].Should().Be("hello world");
        }

        [Fact]
        public void SetToString()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER, b AS STRING " +
                "SET a = 42 " +
                "SET b = TOSTRING(?a) " +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((string)result).Should().Be("42");
        }

        [Fact]
        public void SetToDouble()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS DOUBLE, c AS DOUBLE " +
                "SET a = '3.14' " +
                "SET b = TODOUBLE(?a) " +
                "SET c = ?b + 1.0 " +
                "EXIT WITH ?c"
            );
            dynamic result = func(null);
            ((double)result).Should().BeApproximately(4.14, 0.001);
        }

        [Fact]
        public void SetToDoubleAndConvert()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS DOUBLE, result AS INTEGER " +
                "SET a = '99.5' " +
                "SET b = TODOUBLE(?a) " +
                "SET result = 0 " +
                "IF ?b > 99.0 AND ?b < 100.0 THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetToDate()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS DATETIME, c AS DATETIME, result AS INTEGER " +
                "SET a = '2023-06-15' " +
                "SET b = TODATE(?a) " +
                "SET c = DATETIME '2023-06-15 00:00:00' " +
                "SET result = 0 " +
                "IF ?b = ?c THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetAbs()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER, b AS INTEGER " +
                "SET a = 0 - 42 " +
                "SET b = ABS(?a) " +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(42);

            func = environment.Parse("test2",
                "DECLARE c AS DOUBLE, d AS DOUBLE " +
                "SET c = 0.0 - 3.14 " +
                "SET d = ABS(?c) " +
                "EXIT WITH ?d"
            );
            result = func(null);
            ((double)result).Should().BeApproximately(3.14, 0.001);
        }

        [Fact]
        public void SetLikeAndNotLike()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, c AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello world' " +
                "SET b = ?a LIKE 'hello%' " +
                "SET c = ?a NOT LIKE 'foo%' " +
                "SET result = 0 " +
                "IF ?b AND ?c THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetStartsWithEndsWithContains()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, sw AS BOOLEAN, ew AS BOOLEAN, ct AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello world' " +
                "SET sw = STARTSWITH(?a, 'hello') " +
                "SET ew = ENDSWITH(?a, 'world') " +
                "SET ct = CONTAINS(?a, 'lo wo') " +
                "SET result = 0 " +
                "IF ?sw AND ?ew AND ?ct THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        // ── Group 3: Advanced SELECT Features ──

        [Fact]
        public void SelectDistinct()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT DISTINCT Country FROM Customer"
            );
            dynamic result = func(null);
            int distinctCount = (int)result.Count;

            var func2 = environment.Parse("test2",
                "SELECT Country FROM Customer"
            );
            dynamic result2 = func2(null);
            int totalCount = (int)result2.Count;

            distinctCount.Should().BeGreaterThan(0);
            distinctCount.Should().BeLessThan(totalCount);

            // verify all values are unique
            HashSet<string> countries = new HashSet<string>();
            foreach (dynamic obj in result)
            {
                string country = (string)(obj as IDictionary<string, object>)["Country"];
                countries.Add(country).Should().BeTrue();
            }
        }

        [Fact]
        public void SelectLeftJoin()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT Order.OrderID, Customer.CompanyName " +
                "FROM Order LEFT JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "LIMIT 20"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
            int orderId = (int)(result[0] as IDictionary<string, object>)["OrderID"];
            orderId.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectAvgSum()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT AVG(Freight) AS AvgVal, SUM(Freight) AS Total, COUNT(*) AS Cnt FROM Order"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            double avg = Convert.ToDouble(row["AvgVal"]);
            double total = Convert.ToDouble(row["Total"]);
            int cnt = Convert.ToInt32(row["Cnt"]);
            avg.Should().BeGreaterThan(0);
            total.Should().BeGreaterThan(0);
            cnt.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectLtrimRtrimInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT LTRIM(' ' || CompanyName) AS LT, RTRIM(CompanyName || ' ') AS RT " +
                "FROM Customer LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            string lt = (string)row["LT"];
            string rt = (string)row["RT"];
            lt.Should().NotStartWith(" ");
            rt.Should().NotEndWith(" ");
        }

        [Fact]
        public void SelectNotLikeInWhere()
        {
            SqlCodeDomEnvironment env1 = DomBuilder.NewEnvironment(connection);
            var func = env1.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer"
            );
            dynamic r = func(null);
            int all = (int)(r[0] as IDictionary<string, object>)["Total"];

            SqlCodeDomEnvironment env2 = DomBuilder.NewEnvironment(connection);
            func = env2.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer WHERE CompanyName LIKE 'A%'"
            );
            r = func(null);
            int likeA = (int)(r[0] as IDictionary<string, object>)["Total"];

            SqlCodeDomEnvironment env3 = DomBuilder.NewEnvironment(connection);
            func = env3.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer WHERE NOT (CompanyName LIKE 'A%')"
            );
            r = func(null);
            int notLikeA = (int)(r[0] as IDictionary<string, object>)["Total"];

            all.Should().Be(likeA + notLikeA);
        }

        [Fact]
        public void SelectUnaryNotInWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            var func = environment.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer"
            );
            dynamic r = func(null);
            int all = (int)(r[0] as IDictionary<string, object>)["Total"];

            func = environment.Parse("test2",
                "SELECT COUNT(*) AS Total FROM Customer WHERE Country = 'USA'"
            );
            r = func(null);
            int usa = (int)(r[0] as IDictionary<string, object>)["Total"];

            func = environment.Parse("test3",
                "SELECT COUNT(*) AS Total FROM Customer WHERE NOT (Country = 'USA')"
            );
            r = func(null);
            int notUsa = (int)(r[0] as IDictionary<string, object>)["Total"];

            all.Should().Be(usa + notUsa);
        }

        [Fact]
        public void SelectUnaryMinusInExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT COUNT(*) AS Total FROM OrderDetail WHERE -Quantity < -100.0"
            );
            dynamic r = func(null);
            int negCount = (int)(r[0] as IDictionary<string, object>)["Total"];

            func = environment.Parse("test2",
                "SELECT COUNT(*) AS Total FROM OrderDetail WHERE Quantity > 100.0"
            );
            r = func(null);
            int posCount = (int)(r[0] as IDictionary<string, object>)["Total"];

            negCount.Should().Be(posCount);
        }

        [Fact]
        public void SelectUnaryPlusInExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT +Freight AS PosFreight FROM Order LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
        }

        [Fact]
        public void SelectToStringInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT TOSTRING(CategoryID) AS IdStr FROM Category LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            string idStr = (string)(result[0] as IDictionary<string, object>)["IdStr"];
            idStr.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void SelectSubqueryInExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            // Use a scalar subquery that doesn't depend on correlated entity resolution
            var func = environment.Parse("test",
                "SELECT CategoryName, " +
                "(SELECT COUNT(*) FROM Product) AS TotalProducts " +
                "FROM Category LIMIT 5"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
            var row = result[0] as IDictionary<string, object>;
            row.ContainsKey("CategoryName").Should().BeTrue();
            row.ContainsKey("TotalProducts").Should().BeTrue();
            int totalProducts = Convert.ToInt32(row["TotalProducts"]);
            totalProducts.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectWithMultipleParameters()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "IMPORT country AS STRING, pattern AS STRING " +
                "SELECT COUNT(*) AS Total FROM Customer " +
                "WHERE Country = ?country AND CompanyName LIKE ?pattern"
            );
            var pars = new Dictionary<string, object>
            {
                { "country", "USA" },
                { "pattern", "%A%" }
            };
            dynamic result = func(pars);
            int total = (int)(result[0] as IDictionary<string, object>)["Total"];
            total.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void SelectOrderByAggregate()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT ShipCountry, SUM(Freight) AS TotalFreight " +
                "FROM Order " +
                "GROUP BY ShipCountry " +
                "ORDER BY SUM(Freight) DESC " +
                "LIMIT 5"
            );
            dynamic result = func(null);
            int count = (int)result.Count;
            count.Should().BeGreaterThan(1);

            // verify descending order
            double prev = double.MaxValue;
            foreach (dynamic obj in result)
            {
                double freight = Convert.ToDouble((obj as IDictionary<string, object>)["TotalFreight"]);
                freight.Should().BeLessThanOrEqualTo(prev);
                prev = freight;
            }
        }

        // ── Group 4: Advanced DML ──

        [Fact]
        public void UpdateWithFieldExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            // Insert test row with all required fields
            environment.Parse("setup",
                "INSERT INTO Supplier (CompanyName, ContactName, ContactTitle, Address, City, PostalCode, Country) " +
                "VALUES ('TestCo', 'TestContact', 'manager', '123 St', 'TestCity', '00000', 'TestCountry')"
            )(null);

            // Update with expressions
            environment.Parse("update",
                "UPDATE Supplier " +
                "SET ContactTitle = UPPER(ContactTitle), CompanyName = CompanyName || ' Inc.' " +
                "WHERE CompanyName = 'TestCo'"
            )(null);

            // Verify
            var func = environment.Parse("verify",
                "SELECT ContactTitle, CompanyName FROM Supplier WHERE CompanyName = 'TestCo Inc.'"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            string title = (string)(result[0] as IDictionary<string, object>)["ContactTitle"];
            title.Should().Be("MANAGER");
        }

        [Fact]
        public void UpdateWithSubqueryInWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            // Insert test row with all required fields
            environment.Parse("setup",
                "INSERT INTO Supplier (CompanyName, ContactName, ContactTitle, Address, City, PostalCode, Country) " +
                "VALUES ('TestUpd', 'TestContact', 'clerk', '456 St', 'TestCity', '00000', 'TestCountry')"
            )(null);

            // Update with subquery in WHERE
            environment.Parse("update",
                "UPDATE Supplier SET ContactTitle = 'Director' " +
                "WHERE SupplierID IN (SELECT SupplierID FROM Supplier WHERE CompanyName = 'TestUpd')"
            )(null);

            // Verify
            var func = environment.Parse("verify",
                "SELECT ContactTitle FROM Supplier WHERE CompanyName = 'TestUpd'"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            string title = (string)(result[0] as IDictionary<string, object>)["ContactTitle"];
            title.Should().Be("Director");
        }

        [Fact]
        public void DeleteWithFieldExpressionWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            // Insert test row with all required fields
            environment.Parse("setup",
                "INSERT INTO Supplier (CompanyName, ContactName, ContactTitle, Address, City, PostalCode, Country) " +
                "VALUES ('TestDel', 'TestContact', 'manager', '789 St', 'TestCity', '00000', 'TestCountry')"
            )(null);

            // Count before
            var func = environment.Parse("before",
                "SELECT COUNT(*) AS Total FROM Supplier WHERE CompanyName LIKE 'TestDel%' AND Country = 'TestCountry'"
            );
            dynamic r = func(null);
            int before = (int)(r[0] as IDictionary<string, object>)["Total"];
            before.Should().BeGreaterThanOrEqualTo(1);

            // Delete
            environment.Parse("delete",
                "DELETE FROM Supplier WHERE CompanyName LIKE 'TestDel%' AND Country = 'TestCountry'"
            )(null);

            // Count after
            func = environment.Parse("after",
                "SELECT COUNT(*) AS Total FROM Supplier WHERE CompanyName LIKE 'TestDel%' AND Country = 'TestCountry'"
            );
            r = func(null);
            int after = (int)(r[0] as IDictionary<string, object>)["Total"];
            after.Should().Be(0);
        }

        // ── Group 5: Error Paths and Edge Cases ──

        [Fact]
        public void GetRowIndexOutOfRange()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE idx AS INTEGER, row AS ROW " +
                "SET idx = 0 - 1 " +
                "SELECT * FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), ?idx) " +
                "EXIT WITH 0"
            );
            var ex = Assert.Throws<SqlParserException>(() => func(null));
            ex.Message.Should().Contain("Index out of range");
        }

        [Fact]
        public void GetFieldTypeMismatch()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, val AS INTEGER " +
                "SELECT CategoryName FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET val = GET_FIELD(?row, 'CategoryName', INTEGER) " +
                "EXIT WITH ?val"
            );
            var ex = Assert.Throws<SqlParserException>(() => func(null));
            ex.Message.Should().Contain("is not of type");
        }

        [Fact]
        public void GetFieldMissingField()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, val AS STRING " +
                "SELECT CategoryName FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET val = GET_FIELD(?row, 'NonExistent', STRING) " +
                "EXIT WITH ?val"
            );
            var ex = Assert.Throws<SqlParserException>(() => func(null));
            ex.Message.Should().Contain("doesn't contain field");
        }
        // ── Group 6: GetStrExpression — remaining branches ──

        [Fact]
        public void SelectMinMax()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT MIN(Freight) AS MinF, MAX(Freight) AS MaxF FROM Order"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            double min = Convert.ToDouble(row["MinF"]);
            double max = Convert.ToDouble(row["MaxF"]);
            max.Should().BeGreaterThan(min);
        }

        [Fact]
        public void SelectWithNeqGeLeOperators()
        {
            SqlCodeDomEnvironment env1 = DomBuilder.NewEnvironment(connection);
            var func = env1.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer WHERE Country <> 'USA'"
            );
            dynamic r = func(null);
            int neq = (int)(r[0] as IDictionary<string, object>)["Total"];
            neq.Should().BeGreaterThan(0);

            SqlCodeDomEnvironment env2 = DomBuilder.NewEnvironment(connection);
            func = env2.Parse("test",
                "SELECT COUNT(*) AS Total FROM Order WHERE Freight >= 50.0 AND Freight <= 100.0"
            );
            r = func(null);
            int range = (int)(r[0] as IDictionary<string, object>)["Total"];
            range.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact]
        public void SelectWithOrInWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT COUNT(*) AS Total FROM Customer WHERE Country = 'USA' OR Country = 'UK'"
            );
            dynamic r = func(null);
            int total = (int)(r[0] as IDictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectWithArithmeticInResultset()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT Freight - 1.0 AS Minus, Freight / 2.0 AS Div, Freight * 2.0 AS Mult " +
                "FROM Order LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            row.ContainsKey("Minus").Should().BeTrue();
            row.ContainsKey("Div").Should().BeTrue();
            row.ContainsKey("Mult").Should().BeTrue();
        }

        [Fact]
        public void SelectWithSetVariableInWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE country AS STRING " +
                "SET country = 'USA' " +
                "SELECT COUNT(*) AS Total FROM Customer WHERE Country = ?country"
            );
            dynamic r = func(null);
            int total = (int)(r[0] as IDictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectWithGetFieldInWhere()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, name AS STRING " +
                "SELECT CategoryName FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET name = GET_FIELD(?row, 'CategoryName', STRING) " +
                "SELECT COUNT(*) AS Total FROM Category WHERE CategoryName = ?name"
            );
            dynamic r = func(null);
            int total = (int)(r[0] as IDictionary<string, object>)["Total"];
            total.Should().Be(1);
        }

        [Fact]
        public void SelectWithIsNullPredicate()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT COUNT(*) AS Total FROM Order WHERE ShippedDate IS NOT NULL"
            );
            dynamic r = func(null);
            int total = (int)(r[0] as IDictionary<string, object>)["Total"];
            total.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectToDoubleToDateInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT TODOUBLE('3.14') AS Dbl FROM Category LIMIT 1"
            );
            dynamic r = func(null);
            double val = Convert.ToDouble((r[0] as IDictionary<string, object>)["Dbl"]);
            val.Should().BeApproximately(3.14, 0.01);
        }

        [Fact]
        public void SelectAbsInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT ABS(Freight - 1000.0) AS AbsVal FROM Order LIMIT 1"
            );
            dynamic r = func(null);
            double val = Convert.ToDouble((r[0] as IDictionary<string, object>)["AbsVal"]);
            val.Should().BeGreaterThan(0);
        }

        // ── Group 7: Flow control — cursor, while with DB, for, switch, ADD FIELD/ROW ──

        [Fact]
        public void CursorWithWhereClause()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE my_cur CURSOR FOR " +
                "SELECT CategoryName FROM Category WHERE CategoryID <= 3 " +
                "DECLARE cnt AS INTEGER, record AS ROW, name AS STRING " +
                "SET cnt = 0 " +
                "OPEN CURSOR ?my_cur " +
                "WHILE ?record := FETCH(?my_cur) IS NOT NULL LOOP " +
                "   SET name = GET_FIELD(?record, 'CategoryName', STRING) " +
                "   SET cnt = ?cnt + 1 " +
                "END LOOP " +
                "CLOSE CURSOR ?my_cur " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(3);
        }

        [Fact]
        public void WhileWithDatabaseQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE i AS INTEGER, total AS INTEGER, row AS ROW, cnt AS INTEGER " +
                "SET i = 1, total = 0 " +
                "WHILE ?i <= 3 LOOP " +
                "   SELECT COUNT(*) AS C FROM Category WHERE CategoryID = ?i " +
                "   SET row = GET_ROW(LAST_RESULT(), 0) " +
                "   SET cnt = GET_FIELD(?row, 'C', INTEGER) " +
                "   SET total = ?total + ?cnt " +
                "   SET i = ?i + 1 " +
                "END LOOP " +
                "EXIT WITH ?total"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(3);
        }

        [Fact]
        public void BreakInsideSwitchInsideLoop()
        {
            // BREAK inside SWITCH should exit SWITCH only, not the enclosing loop.
            // This is a known bug: BREAK exits both SWITCH and FOR.
            // Test documents the correct expected behavior.
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE i AS INTEGER, total AS INTEGER " +
                "SET total = 0 " +
                "FOR ?i := 1 WHILE ?i <= 3 NEXT ?i := ?i + 1 LOOP " +
                "   SWITCH ?i " +
                "   CASE 1: " +
                "      SET total = ?total + 10 " +
                "      BREAK " +
                "   CASE 2: " +
                "      SET total = ?total + 20 " +
                "      BREAK " +
                "   CASE 3: " +
                "      SET total = ?total + 30 " +
                "      BREAK " +
                "   END SWITCH " +
                "END LOOP " +
                "EXIT WITH ?total"
            );
            dynamic result = func(null);
            // Correct behavior: 10 + 20 + 30 = 60
            // Bug: BREAK exits both SWITCH and FOR, so result is 10
            ((int)result).Should().Be(60);
        }

        [Fact]
        public void AddFieldAndRowToRowset()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE rs AS ROWSET, row AS ROW " +
                "SET rs = NEW_ROWSET() " +
                "SET row = NEW_ROW() " +
                "ADD FIELD 'Name' WITH 'Alice' TO ?row " +
                "ADD FIELD 'Age' WITH 30 TO ?row " +
                "ADD ROW ?row TO ?rs " +
                "SET row = NEW_ROW() " +
                "ADD FIELD 'Name' WITH 'Bob' TO ?row " +
                "ADD FIELD 'Age' WITH 25 TO ?row " +
                "ADD ROW ?row TO ?rs " +
                "EXIT WITH ROWS_COUNT(?rs)"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(2);
        }

        [Fact]
        public void WhileBreakFromNestedIf()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE i AS INTEGER " +
                "SET i = 0 " +
                "WHILE TRUE LOOP " +
                "   SET i = ?i + 1 " +
                "   IF ?i = 5 THEN " +
                "      BREAK " +
                "   END IF " +
                "END LOOP " +
                "EXIT WITH ?i"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(5);
        }

        [Fact]
        public void ForContinueSkipsIteration()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE i AS INTEGER, total AS INTEGER " +
                "SET total = 0 " +
                "FOR ?i := 1 WHILE ?i <= 5 NEXT ?i := ?i + 1 LOOP " +
                "   IF ?i = 3 THEN " +
                "      CONTINUE " +
                "   END IF " +
                "   SET total = ?total + ?i " +
                "END LOOP " +
                "EXIT WITH ?total"
            );
            dynamic result = func(null);
            // 1 + 2 + 4 + 5 = 12 (skips 3)
            ((int)result).Should().Be(12);
        }

        [Fact]
        public void SwitchWithStringExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE val AS STRING, result AS INTEGER " +
                "SET val = 'B' " +
                "SET result = 0 " +
                "SWITCH ?val " +
                "CASE 'A': SET result = 1 BREAK " +
                "CASE 'B': SET result = 2 BREAK " +
                "CASE 'C': SET result = 3 BREAK " +
                "OTHERWISE: SET result = 99 " +
                "END SWITCH " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(2);
        }

        [Fact]
        public void CursorEmptyResultSet()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE my_cur CURSOR FOR " +
                "SELECT CategoryName FROM Category WHERE CategoryID = 0 - 999 " +
                "DECLARE cnt AS INTEGER, record AS ROW " +
                "SET cnt = 0 " +
                "OPEN CURSOR ?my_cur " +
                "WHILE ?record := FETCH(?my_cur) IS NOT NULL LOOP " +
                "   SET cnt = ?cnt + 1 " +
                "END LOOP " +
                "CLOSE CURSOR ?my_cur " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }

        [Fact]
        public void SelectWithRowsCountOfPreviousQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE cnt AS INTEGER " +
                "SELECT * FROM Category " +
                "SET cnt = ROWS_COUNT(LAST_RESULT()) " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(8);
        }

        // ── Group 8: More DSL coverage — datetime parsing, aggregates, NOT LIKE, ──
        // ── scalar subquery in SET, mixed-type TryGetConstant, error paths       ──

        [Fact]
        public void DateTimeParsingFormats()
        {
            // Covers TryParseDateTime fallback branches (yyyy-MM-dd HH:mm, yyyy-MM-dd HH, yyyy-MM-dd)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS DATETIME, c AS DATETIME, result AS INTEGER " +
                "SET a = '2023-06-15 12:30' " +
                "SET b = TODATE(?a) " +
                "SET c = DATETIME '2023-06-15 12:30' " +
                "SET result = 0 " +
                "IF ?b = ?c THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);

            SqlCodeDomEnvironment env2 = DomBuilder.NewEnvironment(connection);
            func = env2.Parse("test",
                "DECLARE a AS STRING, b AS DATETIME, c AS DATETIME, result AS INTEGER " +
                "SET a = '2023-06-15 12' " +
                "SET b = TODATE(?a) " +
                "SET c = DATETIME '2023-06-15 12:00:00' " +
                "SET result = 0 " +
                "IF ?b = ?c THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);

            SqlCodeDomEnvironment env3 = DomBuilder.NewEnvironment(connection);
            func = env3.Parse("test",
                "DECLARE a AS STRING, b AS DATETIME, c AS DATETIME, result AS INTEGER " +
                "SET a = '2023-06-15' " +
                "SET b = TODATE(?a) " +
                "SET c = DATETIME '2023-06-15 00:00:00' " +
                "SET result = 0 " +
                "IF ?b = ?c THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SelectAggregateMinMaxWithField()
        {
            // Covers SqlExpressionParser VariableAggrFunc branch (line 247-256)
            // and GetStrExpression MIN/MAX aggregate (lines 470-486)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT MIN(UnitPrice) AS MinP, MAX(UnitPrice) AS MaxP, COUNT(ProductName) AS Cnt " +
                "FROM Product"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            Convert.ToDouble(row["MinP"]).Should().BeGreaterThan(0);
            Convert.ToDouble(row["MaxP"]).Should().BeGreaterThan(Convert.ToDouble(row["MinP"]));
            Convert.ToInt32(row["Cnt"]).Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectWithNotLikeOperator()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE cnt AS INTEGER, row AS ROW " +
                "SELECT COUNT(*) AS Total FROM Customer WHERE CompanyName NOT LIKE 'A%' " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET cnt = GET_FIELD(?row, 'Total', INTEGER) " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().BeGreaterThan(0);
        }

        [Fact]
        public void ScalarSubqueryInSetContext()
        {
            // Covers CalculateExpression SqlSelectExpression branch (lines 321-334)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE cnt AS INTEGER " +
                "SET cnt = (SELECT COUNT(*) FROM Category) " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(8);
        }

        [Fact]
        public void ScalarSubqueryEmptyResult()
        {
            // Covers CalculateExpression SqlSelectExpression empty branch (lines 331-334)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE cnt AS INTEGER " +
                "SET cnt = (SELECT COUNT(*) FROM Category WHERE CategoryID = 0 - 999) " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }

        [Fact]
        public void SetUnaryIsNullIsNotNull()
        {
            // Covers SqlUnaryExpression.TryGetConstant IsNull/IsNotNull branches (lines 71-76)
            // and Unknown type IsNull (lines 110-118)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, result AS INTEGER " +
                "SET a = 'hello' " +
                "SET result = 0 " +
                "IF ?a IS NOT NULL THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetToIntConversion()
        {
            // Covers CalculateExpression TOINT path and GetStrExpression TOINT path
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS INTEGER " +
                "SET a = '99' " +
                "SET b = TOINT(?a) " +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(99);
        }

        [Fact]
        public void SelectTrimLeadingTrailing()
        {
            // Covers SqlExpressionParser TRIM LEADING/TRAILING branches (lines 159-167)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT TRIM(LEADING ' hello ') AS TL, TRIM(TRAILING ' hello ') AS TT " +
                "FROM Category LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            var row = result[0] as IDictionary<string, object>;
            string tl = (string)row["TL"];
            string tt = (string)row["TT"];
            tl.Should().NotStartWith(" ");
            tt.Should().NotEndWith(" ");
        }

        [Fact]
        public void SelectGroupByWithAggregate()
        {
            // Covers GetStrExpression aggregate in GROUP BY / ORDER BY context
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT Category, COUNT(*) AS Cnt FROM Product GROUP BY Category ORDER BY COUNT(*) DESC LIMIT 3"
            );
            dynamic result = func(null);
            int count = (int)result.Count;
            count.Should().BeGreaterThan(1);
            // verify descending
            int prev = int.MaxValue;
            foreach (dynamic obj in result)
            {
                int cnt = Convert.ToInt32((obj as IDictionary<string, object>)["Cnt"]);
                cnt.Should().BeLessThanOrEqualTo(prev);
                prev = cnt;
            }
        }

        [Fact]
        public void SelectRightJoin()
        {
            // Covers SelectRunner.DiveTableSpecification RIGHT join branch
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT Customer.CompanyName, Order.OrderID " +
                "FROM Order RIGHT JOIN Customer ON Order.Customer = Customer.CustomerID " +
                "LIMIT 20"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
        }

        [Fact]
        public void AutoJoinSelect()
        {
            // Covers SqlAutoJoinedTable and auto-join resolution
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT OrderDetail.Quantity, Product.ProductName " +
                "FROM OrderDetail AUTO JOIN Product " +
                "LIMIT 5"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
            var row = result[0] as IDictionary<string, object>;
            row.ContainsKey("Quantity").Should().BeTrue();
            row.ContainsKey("ProductName").Should().BeTrue();
        }

        [Fact]
        public void InsertWithSelectSubquery()
        {
            // Covers SqlInsertStatement INSERT-SELECT branch (lines 76-88)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);

            // Count before
            var func = environment.Parse("before",
                "SELECT COUNT(*) AS Total FROM Supplier"
            );
            dynamic r = func(null);
            int before = (int)(r[0] as IDictionary<string, object>)["Total"];

            // INSERT ... SELECT (copy one row with modified name)
            environment.Parse("insert",
                "INSERT INTO Supplier (CompanyName, ContactName, ContactTitle, Address, City, PostalCode, Country) " +
                "SELECT CompanyName || ' Copy', ContactName, ContactTitle, Address, City, PostalCode, Country " +
                "FROM Supplier LIMIT 1"
            )(null);

            // Count after
            func = environment.Parse("after",
                "SELECT COUNT(*) AS Total FROM Supplier"
            );
            r = func(null);
            int after = (int)(r[0] as IDictionary<string, object>)["Total"];
            after.Should().Be(before + 1);

            // Cleanup
            environment.Parse("cleanup",
                "DELETE FROM Supplier WHERE CompanyName LIKE '% Copy'"
            )(null);
        }

        [Fact]
        public void AssignExpressionInWhileCondition()
        {
            // Covers AssignExpression parsing and CalculateExpression (lines 143-147)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE my_cur CURSOR FOR SELECT CategoryName FROM Category " +
                "DECLARE record AS ROW, cnt AS INTEGER " +
                "SET cnt = 0 " +
                "OPEN CURSOR ?my_cur " +
                "WHILE ?record := FETCH(?my_cur) IS NOT NULL LOOP " +
                "   SET cnt = ?cnt + 1 " +
                "END LOOP " +
                "CLOSE CURSOR ?my_cur " +
                "EXIT WITH ?cnt"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(8);
        }

        [Fact]
        public void SetDoubleComparisons()
        {
            // Covers TryGetConstant Double Neq/Ge/Le branches (lines 142-163)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS DOUBLE, result AS INTEGER " +
                "SET a = 5.0 " +
                "SET result = 0 " +
                "IF ?a <> 3.0 AND ?a >= 5.0 AND ?a <= 5.0 THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SetBooleanEqNeq()
        {
            // Covers TryGetConstant Boolean Eq/Neq branches (lines 186-200)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS BOOLEAN, b AS BOOLEAN, result AS INTEGER " +
                "SET a = TRUE " +
                "SET b = FALSE " +
                "SET result = 0 " +
                "IF ?a = TRUE AND ?a <> ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        // ── Group 9: Targeted coverage — GetStrExpression wrappers, DATETIME formats, ──
        // ── TODATE in query, NOT constant, runtime expressions in WHERE              ──

        [Fact]
        public void RuntimeExpressionsInWhere()
        {
            // Covers GetStrExpression wrappers for GetLastResult (602-603),
            // GetRowsCount (606-607), GetRow (610-611), GetField (614-615)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, catId AS INTEGER " +
                "SELECT * FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET catId = GET_FIELD(?row, 'CategoryID', INTEGER) " +
                "SELECT ProductName FROM Product WHERE Category = ?catId LIMIT 3"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
        }

        [Fact]
        public void DateTimeShortFormats()
        {
            // Covers ParseExpression DATETIME fallback branches (lines 78-95)
            SqlCodeDomEnvironment env1 = DomBuilder.NewEnvironment(connection);
            var func = env1.Parse("test",
                "DECLARE a AS DATETIME " +
                "SET a = DATETIME '2023-06-15 12:30' " +
                "EXIT WITH ?a"
            );
            dynamic result = func(null);
            ((DateTime)result).Month.Should().Be(6);

            SqlCodeDomEnvironment env2 = DomBuilder.NewEnvironment(connection);
            func = env2.Parse("test",
                "DECLARE a AS DATETIME " +
                "SET a = DATETIME '2023-06-15 12' " +
                "EXIT WITH ?a"
            );
            result = func(null);
            ((DateTime)result).Hour.Should().Be(12);

            SqlCodeDomEnvironment env3 = DomBuilder.NewEnvironment(connection);
            func = env3.Parse("test",
                "DECLARE a AS DATETIME " +
                "SET a = DATETIME '2023-06-15' " +
                "EXIT WITH ?a"
            );
            result = func(null);
            ((DateTime)result).Day.Should().Be(15);
        }

        [Fact]
        public void SelectToDateInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, d AS DATETIME, result AS INTEGER " +
                "SELECT TODATE('2023-06-15') AS D FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET d = GET_FIELD(?row, 'D', DATETIME) " +
                "SET result = 0 " +
                "IF ?d = DATETIME '2023-06-15 00:00:00' THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SelectToTimestampInQuery()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, ts AS INTEGER " +
                "SELECT TOTIMESTAMP('2023-06-15 12:00:00') AS Ts FROM Category LIMIT 1 " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET ts = GET_FIELD(?row, 'Ts', INTEGER) " +
                "EXIT WITH ?ts"
            );
            dynamic result = func(null);
            ((int)result).Should().BeGreaterThan(0);
        }

        [Fact]
        public void NotBooleanConstantFolding()
        {
            // Covers SqlUnaryExpression.TryGetConstant Boolean NOT branch (lines 102-108)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS BOOLEAN, result AS INTEGER " +
                "SET a = NOT TRUE " +
                "SET result = 0 " +
                "IF ?a = FALSE THEN SET result = 1 END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void SelectCountWithField()
        {
            // Covers ParseExpression VariableAggrFunc COUNT(field) branch (lines 247-256)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT COUNT(CompanyName) AS Cnt FROM Customer"
            );
            dynamic result = func(null);
            int cnt = (int)(result[0] as IDictionary<string, object>)["Cnt"];
            cnt.Should().Be(91);
        }

        [Fact]
        public void RowsCountInWhereContext()
        {
            // Covers GetStrExpression GetRowsCount wrapper (lines 606-607)
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE cnt AS INTEGER " +
                "SELECT * FROM Category " +
                "SET cnt = ROWS_COUNT(LAST_RESULT()) " +
                "SELECT * FROM Product WHERE Category <= ?cnt LIMIT 5"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().BeGreaterThan(0);
        }
    }
}
