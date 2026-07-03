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
    /// <summary>
    /// Characterization tests pinning the CURRENT behavior of poorly-covered, high-risk
    /// walkers before the Hime→ANTLR migration (see CLAUDE/ANTLR_MIGRATION.md).
    ///
    /// These target the type-validation error branches in the CodeDom constructors that
    /// existing happy-path tests never exercise (SqlQualifiedJoinedTable, GET_ROW/GET_FIELD/
    /// FETCH, ADD FIELD/ADD ROW, cursors, IN). They must pass on the Hime build AND after
    /// the native ANTLR rewrite.
    ///
    /// Assertions deliberately match only STABLE message prefixes — never the interpolated
    /// node Symbol.Name/Value, which changes when the parse tree switches from Hime to ANTLR.
    /// </summary>
    public sealed class CharacterizationTests : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public CharacterizationTests()
        {
            connectionFactory = new SqlDbUniversalConnectionFactory(UniversalSqlDbFactory.SQLITE, "Data Source=:memory:");
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

        private SqlCodeDomEnvironment Env() => DomBuilder.NewEnvironment(connection);

        private static SqlParserException ParseThrows(SqlCodeDomEnvironment env, string sql)
            => Assert.Throws<SqlParserException>(() => env.Parse("test", sql));

        private static SqlParserException RunThrows(Func<IDictionary<string, object>, dynamic> func)
            => Assert.Throws<SqlParserException>(() => func(null));

        // ─────────────────────────── GET_FIELD ───────────────────────────

        [Fact]
        public void GetField_FirstArgNotRow_Throws()
        {
            var ex = ParseThrows(Env(), "EXIT WITH GET_FIELD('notarow', 'x', STRING)");
            ex.Message.Should().Contain("No ROW parameter in GET_FIELD");
        }

        [Fact]
        public void GetField_NameArgNotString_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE r AS ROW EXIT WITH GET_FIELD(?r, 5, STRING)");
            ex.Message.Should().Contain("No valid Name parameter in GET_FIELD");
        }

        // ─────────────────────────── GET_ROW ───────────────────────────

        [Fact]
        public void GetRow_FirstArgNotRowSet_Throws()
        {
            var ex = ParseThrows(Env(), "EXIT WITH GET_ROW('notarowset', 0)");
            ex.Message.Should().Contain("No ROWSET parameter in GET_ROW");
        }

        [Fact]
        public void GetRow_IndexArgNotInteger_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE rs AS ROWSET EXIT WITH GET_ROW(?rs, 'x')");
            ex.Message.Should().Contain("No index parameter in GET_ROW");
        }

        // ─────────────────────────── FETCH ───────────────────────────

        [Fact]
        public void Fetch_ArgNotCursor_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE x AS INTEGER EXIT WITH FETCH(?x)");
            ex.Message.Should().Contain("No cursor parameter in FETCH");
        }

        // ─────────────────────────── ADD FIELD ───────────────────────────

        [Fact]
        public void AddField_NameNotString_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE r AS ROW ADD FIELD 123 WITH 'v' TO ?r");
            ex.Message.Should().Contain("should have STRING type");
        }

        [Fact]
        public void AddField_TargetNotRow_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE r AS INTEGER ADD FIELD 'f' WITH 'v' TO ?r");
            ex.Message.Should().Contain("Row expression in ADD FIELD statement should have ROW type");
        }

        // ─────────────────────────── ADD ROW ───────────────────────────

        [Fact]
        public void AddRow_ValueNotRow_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE rs AS ROWSET ADD ROW 'notarow' TO ?rs");
            ex.Message.Should().Contain("Row expression in ADD ROW statement should have ROW type");
        }

        [Fact]
        public void AddRow_TargetNotRowSet_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE r AS ROW, x AS INTEGER ADD ROW ?r TO ?x");
            ex.Message.Should().Contain("RowSet expression in ADD ROW statement should have ROWSET type");
        }

        // ─────────────────────────── OPEN / CLOSE CURSOR ───────────────────────────

        [Fact]
        public void OpenCursor_ArgNotCursor_Throws()
        {
            var ex = ParseThrows(Env(), "DECLARE x AS INTEGER OPEN CURSOR ?x");
            ex.Message.Should().Contain("Parameter of OPEN CURSOR is not declared as CURSOR");
        }

        [Fact]
        public void CloseCursor_ArgNotCursor_Throws()
        {
            // NOTE: current message wrongly says "OPEN CURSOR" (see KNOWN_BUGS.md #2);
            // assert only the shared, bug-agnostic tail.
            var ex = ParseThrows(Env(), "DECLARE x AS INTEGER CLOSE CURSOR ?x");
            ex.Message.Should().Contain("not declared as CURSOR");
        }

        // ─────────────────────────── IN predicate ───────────────────────────

        [Fact]
        public void InList_TypeMismatch_Throws()
        {
            var ex = ParseThrows(Env(), "SELECT * FROM Order WHERE ShipCountry IN (1, 2)");
            ex.Message.Should().Contain("Incorrect type of operand");
        }

        [Fact]
        public void InSubquery_MultipleColumns_Throws()
        {
            var ex = ParseThrows(Env(),
                "SELECT * FROM OrderDetail WHERE Order IN (SELECT OrderID, ShipCountry FROM Order)");
            ex.Message.Should().Contain("Expected 1 column in inner SELECT");
        }

        [Fact]
        public void InSubquery_TypeMismatch_Throws()
        {
            var ex = ParseThrows(Env(),
                "SELECT * FROM OrderDetail WHERE Order IN (SELECT ShipCountry FROM Order)");
            ex.Message.Should().Contain("Incorrect type of operand");
        }

        // ─────────────────────────── JOIN ON (deferred to run-time) ───────────────────────────

        [Fact]
        public void JoinOn_NotBoolean_ThrowsAtRun()
        {
            var func = Env().Parse("test",
                "SELECT OrderDetail.Quantity FROM OrderDetail INNER JOIN Order ON 1");
            var ex = RunThrows(func);
            ex.Message.Should().Contain("Result of ON should be boolean");
        }

        [Fact]
        public void JoinOn_ContainsAggregate_ThrowsAtRun()
        {
            var func = Env().Parse("test",
                "SELECT OrderDetail.Quantity FROM OrderDetail INNER JOIN Order ON COUNT(*) = 1");
            var ex = RunThrows(func);
            ex.Message.Should().Contain("ON expression should not contain calls of aggregate functions");
        }

        // ───────────────── JOIN types (pins JoinType parsing — migration risk #1) ─────────────────

        [Theory]
        [InlineData("INNER JOIN")]
        [InlineData("LEFT JOIN")]
        [InlineData("LEFT OUTER JOIN")]
        [InlineData("RIGHT JOIN")]
        [InlineData("RIGHT OUTER JOIN")]
        [InlineData("FULL JOIN")]
        [InlineData("FULL OUTER JOIN")]
        public void QualifiedJoinType_ParsesAndExecutes(string joinType)
        {
            var func = Env().Parse("test",
                $"SELECT OrderDetail.Quantity FROM OrderDetail {joinType} Order " +
                "ON OrderDetail.Order = Order.OrderID LIMIT 3");
            dynamic result = func(null);
            ((int)result.Count).Should().Be(3);
        }

        [Fact]
        public void PlainJoin_DefaultsToInner()
        {
            // Fixed in the ANTLR migration: a bare JOIN (no explicit type) now defaults to
            // INNER (KNOWN_BUGS.md #3), via the typed joinType() accessor in SqlQualifiedJoinedTable.
            var func = Env().Parse("test",
                "SELECT OrderDetail.Quantity FROM OrderDetail JOIN Order " +
                "ON OrderDetail.Order = Order.OrderID LIMIT 3");
            dynamic result = func(null);
            ((int)result.Count).Should().Be(3);
        }

        [Fact]
        public void NestedQualifiedJoin_AsLeftOfAutoJoin_Parses()
        {
            // Pins the DOM-construction branch where the left side of an AUTO JOIN is itself a
            // qualified join (SqlAutoJoinedTable:35-37) — exercised during Parse.
            // NOTE: executing this currently generates invalid SQL ("no such column: INNER");
            // see KNOWN_BUGS.md #4. That is a SelectRunner SQL-generation defect, out of scope
            // for the walker rewrite, so we only assert that Code-DOM construction succeeds.
            var act = () => Env().Parse("test",
                "SELECT OrderDetail.Quantity FROM OrderDetail " +
                "INNER JOIN Order ON OrderDetail.Order = Order.OrderID " +
                "AUTO JOIN Customer LIMIT 3");
            act.Should().NotThrow();
        }

        // ───────────── Entity / field resolution errors (parse-time) ─────────────

        [Fact]
        public void UnknownTable_Throws()
        {
            var ex = ParseThrows(Env(), "SELECT * FROM NoSuchTable");
            ex.Message.Should().Contain("Not found entity with name 'NoSuchTable'");
        }

        [Fact]
        public void UnknownField_Throws()
        {
            var ex = ParseThrows(Env(), "SELECT NoSuchField FROM Category");
            ex.Message.Should().Contain("Not found field");
        }

        [Fact]
        public void UnknownFieldPrefix_Throws()
        {
            var ex = ParseThrows(Env(), "SELECT bad.CategoryName FROM Category");
            ex.Message.Should().Contain("Not found entity 'bad'");
        }

        // ───────────── Scalar subquery arity (parse-time) ─────────────

        [Fact]
        public void ScalarSubquery_MultipleColumns_Throws()
        {
            var ex = ParseThrows(Env(), "EXIT WITH (SELECT OrderID, ShipCountry FROM Order)");
            ex.Message.Should().Contain("Expected 1 column in inner SELECT");
        }

        // ───────────── Assignment operand-type mismatch (parse-time) ─────────────

        [Fact]
        public void Assign_ReassignIncompatibleType_Throws()
        {
            var ex = ParseThrows(Env(), "?x := 5 ?x := 'str'");
            ex.Message.Should().Contain("Types of operands don't match");
        }
    }
}
