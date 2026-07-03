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
    /// Characterization tests for the expression core (SqlExpressionParser, SqlBinaryExpression,
    /// SqlUnaryExpression) ahead of the Hime→ANTLR migration. Pins:
    ///   • constant folding results (arithmetic, comparison, concat, boolean, unary), and
    ///   • function-argument type-validation errors.
    /// This is the hardest-to-rewrite code (operator-rooted expression dispatch), so its
    /// behavior is nailed down before the native ANTLR rewrite. Assertions match only stable
    /// message prefixes so they survive the parse-tree change.
    /// </summary>
    public sealed class CharacterizationExpressionTests : IDisposable
    {
        private SqlCodeDomBuilder DomBuilder { get; }
        private readonly ISqlDbConnectionFactory connectionFactory;
        private readonly SqlDbConnection connection;

        public CharacterizationExpressionTests()
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

        private dynamic Eval(string expr)
            => DomBuilder.NewEnvironment(connection).Parse("test", $"EXIT WITH {expr}")(null);

        private SqlParserException ParseThrows(string sql)
            => Assert.Throws<SqlParserException>(() => DomBuilder.NewEnvironment(connection).Parse("test", sql));

        // ───────────── Numeric constant folding (note: +,-,*,/ fold to Double) ─────────────

        [Fact] public void Fold_Add() => ((double)Eval("2 + 3")).Should().Be(5.0);
        [Fact] public void Fold_Sub() => ((double)Eval("10 - 4")).Should().Be(6.0);
        [Fact] public void Fold_Mul() => ((double)Eval("3 * 4")).Should().Be(12.0);
        [Fact] public void Fold_Div() => ((double)Eval("12 / 4")).Should().Be(3.0);

        [Fact] public void Fold_Lt() => ((bool)Eval("2 < 3")).Should().BeTrue();
        [Fact] public void Fold_Gt() => ((bool)Eval("5 > 2")).Should().BeTrue();
        [Fact] public void Fold_Ge() => ((bool)Eval("3 >= 3")).Should().BeTrue();
        [Fact] public void Fold_Le() => ((bool)Eval("2 <= 2")).Should().BeTrue();
        [Fact] public void Fold_Eq() => ((bool)Eval("5 = 5")).Should().BeTrue();
        [Fact] public void Fold_Neq() => ((bool)Eval("5 <> 4")).Should().BeTrue();

        // ───────────── String / boolean constant folding ─────────────

        [Fact] public void Fold_Concat() => ((string)Eval("'a' || 'b'")).Should().Be("ab");
        [Fact] public void Fold_And() => ((bool)Eval("TRUE AND FALSE")).Should().BeFalse();
        [Fact] public void Fold_Or() => ((bool)Eval("TRUE OR FALSE")).Should().BeTrue();

        // ───────────── Unary constant folding ─────────────

        [Fact] public void Fold_UnaryMinus() => ((int)Eval("-5")).Should().Be(-5);
        [Fact] public void Fold_UnaryPlus() => ((int)Eval("+5")).Should().Be(5);
        [Fact] public void Fold_UnaryMinusDouble() => ((double)Eval("-5.5")).Should().Be(-5.5);
        [Fact] public void Fold_UnaryPlusDouble() => ((double)Eval("+5.5")).Should().Be(5.5);
        [Fact] public void Fold_Not() => ((bool)Eval("NOT TRUE")).Should().BeFalse();
        [Fact] public void Fold_IsNull_False() => ((bool)Eval("5 IS NULL")).Should().BeFalse();
        [Fact] public void Fold_IsNotNull_True() => ((bool)Eval("5 IS NOT NULL")).Should().BeTrue();

        // ───────────── Binary operand-type errors (parse-time) ─────────────

        [Fact]
        public void Binary_InvalidOperationForType_Throws()
        {
            var ex = ParseThrows("EXIT WITH TRUE + FALSE");
            ex.Message.Should().Contain("Incorrect type of operation");
        }

        [Fact]
        public void Binary_MismatchedOperandTypes_Throws()
        {
            var ex = ParseThrows("EXIT WITH 'a' + 1");
            ex.Message.Should().Contain("Types of operands don't match");
        }

        // ───────────── Unary operand-type error (parse-time) ─────────────

        [Fact]
        public void Unary_NotOnNonBoolean_Throws()
        {
            var ex = ParseThrows("EXIT WITH NOT 5");
            ex.Message.Should().Contain("Type of operand doesn't match the operation");
        }

        // ───────────── Function-argument type validation (parse-time) ─────────────

        [Fact]
        public void Trim_NonStringArg_Throws()
        {
            var ex = ParseThrows("EXIT WITH TRIM(123)");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        [Fact]
        public void Abs_NonNumericArg_Throws()
        {
            var ex = ParseThrows("EXIT WITH ABS('x')");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        [Fact]
        public void Like_LeftNotString_Throws()
        {
            var ex = ParseThrows("SELECT CategoryName FROM Category WHERE 5 LIKE 'a'");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        [Fact]
        public void Like_RightNotString_Throws()
        {
            var ex = ParseThrows("SELECT CategoryName FROM Category WHERE CategoryName LIKE 5");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        [Fact]
        public void Contains_FirstArgNotString_Throws()
        {
            var ex = ParseThrows("EXIT WITH CONTAINS(1, 'x')");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        [Fact]
        public void Contains_SecondArgNotString_Throws()
        {
            var ex = ParseThrows("EXIT WITH CONTAINS('x', 2)");
            ex.Message.Should().Contain("Incorrect type of parameter");
        }

        // ───────────── Malformed date/datetime literals (parse-time) ─────────────

        [Fact]
        public void Date_Malformed_Throws()
        {
            var ex = ParseThrows("EXIT WITH DATE '99'");
            ex.Message.Should().Contain("Incorrect DateTime");
        }

        [Fact]
        public void Datetime_Malformed_Throws()
        {
            var ex = ParseThrows("EXIT WITH DATETIME 'not-a-date'");
            ex.Message.Should().Contain("Incorrect DateTime");
        }
    }
}
