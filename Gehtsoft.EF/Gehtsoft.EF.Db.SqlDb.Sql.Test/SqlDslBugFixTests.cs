using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.Sql.CodeDom;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Northwind;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Gehtsoft.EF.Db.SqlDb.Sql.Test
{
    public sealed class SqlDslBugFixTests : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public SqlDslBugFixTests()
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

        // Bug 1: TOINT grammar token was matched against "TOINTEGER" — never reached

        [Fact]
        public void ToIntInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS INTEGER;" +
                "SET a = '42';" +
                "SET b = TOINT(?a);" +
                "EXIT WITH ?b"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(42);
        }

        [Fact]
        public void ToIntInSelectExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "SELECT TOINT('123') AS Val FROM Category LIMIT 1"
            );
            dynamic result = func(null);
            ((int)result.Count).Should().Be(1);
            int val = (int)(result[0] as IDictionary<string, object>)["Val"];
            val.Should().Be(123);
        }

        // Bug 2: STARTSWITH/ENDSWITH/CONTAINS returned !result in CalculateExpression

        [Fact]
        public void StartsWithInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello world' " +
                "SET b = STARTSWITH(?a, 'hello') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void EndsWithInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello world' " +
                "SET b = ENDSWITH(?a, 'world') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void ContainsInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello world' " +
                "SET b = CONTAINS(?a, 'lo wo') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void StartsWithNegativeCase()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'hello' " +
                "SET b = STARTSWITH(?a, 'xyz') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }

        // Bug 3: IN-list evaluated LeftOperand instead of list item

        [Fact]
        public void InListInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'USA' " +
                "SET b = ?a IN ('UK', 'France', 'USA') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void InListNegativeCase()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'Japan' " +
                "SET b = ?a IN ('UK', 'France', 'USA') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }

        // Bug 4: IN-SELECT cast ExpandoObject to Dictionary<string,object> — always null

        [Fact]
        public void InSelectInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE row AS ROW, id AS INTEGER, found AS BOOLEAN, result AS INTEGER " +
                "SELECT CategoryID FROM Category " +
                "SET row = GET_ROW(LAST_RESULT(), 0) " +
                "SET id = GET_FIELD(?row, 'CategoryID', INTEGER) " +
                "SET found = ?id IN (SELECT CategoryID FROM Category) " +
                "SET result = 0 " +
                "IF ?found THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void InSelectNegativeCase()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE id AS INTEGER, found AS BOOLEAN, result AS INTEGER " +
                "SET id = 0 - 999 " +
                "SET found = ?id IN (SELECT CategoryID FROM Category) " +
                "SET result = 0 " +
                "IF ?found THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }
        // Bug fix: NOT IN not handled in CalculateExpression

        [Fact]
        public void NotInListInSetContext()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'Japan' " +
                "SET b = ?a NOT IN ('UK', 'France', 'USA') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void NotInListNegativeCase()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS STRING, b AS BOOLEAN, result AS INTEGER " +
                "SET a = 'USA' " +
                "SET b = ?a NOT IN ('UK', 'France', 'USA') " +
                "SET result = 0 " +
                "IF ?b THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }

        // Bug fix: EXIT WITH inside IF block causes Stack empty

        [Fact]
        public void ExitWithInsideIfBlock()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER " +
                "SET a = 5 " +
                "IF ?a = 5 THEN " +
                "   EXIT WITH 1 " +
                "END IF " +
                "EXIT WITH 0"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(1);
        }

        [Fact]
        public void ExitWithInsideElseBlock()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE a AS INTEGER " +
                "SET a = 99 " +
                "IF ?a = 5 THEN " +
                "   EXIT WITH 1 " +
                "ELSE " +
                "   EXIT WITH 2 " +
                "END IF " +
                "EXIT WITH 0"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(2);
        }

        // Bug fix: Unary minus before IN expression causes parse failure

        [Fact]
        public void UnaryMinusBeforeInExpression()
        {
            SqlCodeDomEnvironment environment = DomBuilder.NewEnvironment(connection);
            var func = environment.Parse("test",
                "DECLARE found AS BOOLEAN, result AS INTEGER " +
                "SET found = -999 IN (SELECT CategoryID FROM Category) " +
                "SET result = 0 " +
                "IF ?found THEN " +
                "   SET result = 1 " +
                "END IF " +
                "EXIT WITH ?result"
            );
            dynamic result = func(null);
            ((int)result).Should().Be(0);
        }
    }
}
