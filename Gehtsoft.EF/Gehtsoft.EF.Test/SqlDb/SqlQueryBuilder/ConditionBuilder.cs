using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Moq;
using Xunit;

#pragma warning disable CA1822 // Mark members as static

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class Condition
    {
        protected readonly TableDescriptor mTable1, mTable2;
        protected readonly IConditionBuilderInfoProvider mProvider;

        public Condition()
        {
            Mock<IConditionBuilderInfoProvider> mockProvider = new Mock<IConditionBuilderInfoProvider>();
            mockProvider.Setup(m => m.Specifics).Returns(new Sql92LanguageSpecifics());
            mockProvider.Setup(m => m.GetAlias(It.IsAny<TableDescriptor.ColumnInfo>(), It.IsAny<QueryBuilderEntity>()))
                .Returns<TableDescriptor.ColumnInfo, QueryBuilderEntity>((c, e) =>
                {
                    var alias = (e?.Alias) ?? c.Table.Name;
                    return $"{alias}.{c.Name}";
                });

            mProvider = mockProvider.Object;

            mTable1 = new TableDescriptor()
            {
                Name = "table1",
            };

            mTable1.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
            });

            mTable1.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col2",
            });

            mTable2 = new TableDescriptor()
            {
                Name = "table1",
            };

            mTable2.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "cola",
            });

            mTable2.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "colb",
            });
        }

        [Fact]
        public void Property()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(mTable1[0]);
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("FIELD");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/FIELD/*", 1)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("table1");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/FIELD/*", 2)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("col1");
        }

        [Fact]
        public void PropertyWithEntity()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(new QueryBuilderEntity(mProvider), mTable1[0]);
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("FIELD");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/FIELD/*", 1)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("table1");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/FIELD/*", 2)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("col1");
        }

        [Theory]
        [InlineData(AggFn.Avg, "AVG")]
        [InlineData(AggFn.Max, "MAX")]
        [InlineData(AggFn.Min, "MIN")]
        [InlineData(AggFn.Sum, "SUM")]
        public void Property_AggFn(AggFn aggFn, string expected)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(aggFn, mTable1[0]);
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("AGGR_FUNC");

            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AGGR_FUNC/*", 1)
                .Should().HaveSymbol(expected);

            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AGGR_FUNC/*", 2)
                .Should().HaveSymbol("FIELD");

            var f = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AGGR_FUNC/*", 2);

            f.SelectNode("/*", 1)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("table1");
            f.SelectNode("/*", 2)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("col1");
        }

        [Fact]
        public void Parameter()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Parameter("a");
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("PARAM");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/PARAM/*", 1)
                .Should().HaveSymbol("IDENTIFIER")
                .And.HaveValue("a");
        }

        [Fact]
        public void Parameters()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(mTable1[0]).In().Parameters(new string[] { "a", "b", "c" });
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("EXACT_IN_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/EXACT_IN_OP/*", 1)
                .Should().HaveSymbol("FIELD");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/EXACT_IN_OP/*", 2)
                .Should().HaveSymbol("IN_VALUE_ARGS")
                .And.Subject.Select("/PARAM")
                    .Should().HaveCount(3)
                    .And.HaveElementMatching(m => m.SelectNode("*", 1).Value == "a")
                    .And.HaveElementMatching(m => m.SelectNode("*", 1).Value == "b")
                    .And.HaveElementMatching(m => m.SelectNode("*", 1).Value == "c");
        }

        [Theory]
        [InlineData(CmpOp.Eq, "EQ_OP")]
        [InlineData(CmpOp.Neq, "NEQ_OP")]
        [InlineData(CmpOp.Ls, "LT_OP")]
        [InlineData(CmpOp.Le, "LE_OP")]
        [InlineData(CmpOp.Gt, "GT_OP")]
        [InlineData(CmpOp.Ge, "GE_OP")]
        [InlineData(CmpOp.Like, "EXACT_LIKE_OP")]
        public void BinaryOp(CmpOp op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Raw("1").Is(op).Raw("2");
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/INT")
                    .Should().HaveCount(2)
                    .And.HaveElementMatchingAt(0, e => e.Symbol == "INT" && e.Value == "1")
                    .And.HaveElementMatchingAt(1, e => e.Symbol == "INT" && e.Value == "2");
        }

        [Theory]
        [InlineData(nameof(CmpOp.Eq), "EQ_OP")]
        [InlineData(nameof(CmpOp.Neq), "NEQ_OP")]
        [InlineData(nameof(CmpOp.Ls), "LT_OP")]
        [InlineData(nameof(CmpOp.Le), "LE_OP")]
        [InlineData(nameof(CmpOp.Gt), "GT_OP")]
        [InlineData(nameof(CmpOp.Ge), "GE_OP")]
        [InlineData(nameof(CmpOp.Like), "EXACT_LIKE_OP")]
        public void BinaryOp_ByExtension(string op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            var sb = builder.Raw("1");
            var m = typeof(SingleConditionBuilderExtension).GetMethod(op);
            m.Should().NotBeNull()
                .And.Subject.GetParameters()
                    .Should().HaveCount(1)
                    .And.Subject.As<ParameterInfo[]>()[0].ParameterType
                        .Should().Be(typeof(SingleConditionBuilder));

            m.Invoke(null, new object[] { sb });
            sb.Raw("2");

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/INT")
                    .Should().HaveCount(2)
                    .And.HaveElementMatchingAt(0, e => e.Symbol == "INT" && e.Value == "1")
                    .And.HaveElementMatchingAt(1, e => e.Symbol == "INT" && e.Value == "2");
        }

        [Theory]
        [InlineData(CmpOp.IsNull, "EXACT_NULL_OP")]
        [InlineData(CmpOp.NotNull, "NOT_NULL_OP")]
        public void Unart(CmpOp op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Raw("1").Is(op);
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/INT")
                    .Should().HaveCount(1)
                    .And.HaveElementMatchingAt(0, e => e.Symbol == "INT" && e.Value == "1");
        }

        [Theory]
        [InlineData(nameof(CmpOp.IsNull), "EXACT_NULL_OP")]
        [InlineData(nameof(CmpOp.NotNull), "NOT_NULL_OP")]
        public void Unart_ByExension(string op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            var sb = builder.Raw("1");

            var m = typeof(SingleConditionBuilderExtension).GetMethod(op);
            m.Should().NotBeNull()
                .And.Subject.GetParameters()
                    .Should().HaveCount(1)
                    .And.Subject.As<ParameterInfo[]>()[0].ParameterType
                        .Should().Be(typeof(SingleConditionBuilder));

            m.Invoke(null, new object[] { sb });

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/INT")
                    .Should().HaveCount(1)
                    .And.HaveElementMatchingAt(0, e => e.Symbol == "INT" && e.Value == "1");
        }

        [Theory]
        [InlineData(CmpOp.In, true, "EXACT_IN_OP")]
        [InlineData(CmpOp.NotIn, true, "NOT_IN_OP")]
        [InlineData(CmpOp.Exists, false, "EXACT_EXISTS_OP")]
        [InlineData(CmpOp.NotExists, false, "NOT_EXISTS_OP")]
        public void QueryOp(CmpOp op, bool hasLeft, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            var select = connection.GetSelectQueryBuilder(mTable2);
            select.AddToResultset(mTable2[0]);

            var builder = new ConditionBuilder(mProvider);
            builder.Add(hasLeft ? "1" : null, op, builder.Query(select));
            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/*")
                    .Should().HaveCount(hasLeft ? 2 : 1)
                    .And.HaveElementMatchingAt(hasLeft ? 1 : 0, e => e.Symbol == "SELECT");
        }

        [Theory]
        [InlineData(nameof(CmpOp.In), true, "EXACT_IN_OP")]
        [InlineData(nameof(CmpOp.NotIn), true, "NOT_IN_OP")]
        [InlineData(nameof(CmpOp.Exists), false, "EXACT_EXISTS_OP")]
        [InlineData(nameof(CmpOp.NotExists), false, "NOT_EXISTS_OP")]
        public void QueryOp_ByExtension(string op, bool hasLeft, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            var select = connection.GetSelectQueryBuilder(mTable2);
            select.AddToResultset(mTable2[0]);

            var builder = new ConditionBuilder(mProvider);
            SingleConditionBuilder sb;

            if (hasLeft)
                sb = builder.Raw("1");
            else
                sb = builder.And();

            var m = typeof(SingleConditionBuilderExtension).GetMethod(op);

            m.Should().NotBeNull()
              .And.Subject.GetParameters()
                  .Should().HaveCount(1)
                  .And.Subject.As<ParameterInfo[]>()[0].ParameterType
                      .Should().Be(typeof(SingleConditionBuilder));

            m.Invoke(null, new[] { sb });

            sb.Query(select);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol(expectedOp)
                .And.Subject.Select("/*")
                    .Should().HaveCount(hasLeft ? 2 : 1)
                    .And.HaveElementMatchingAt(hasLeft ? 1 : 0, e => e.Symbol == "SELECT");
        }

        [Theory]
        [InlineData(SqlFunctionId.Abs, "ABS")]
        [InlineData(SqlFunctionId.Upper, "UPPER")]
        [InlineData(SqlFunctionId.Lower, "LOWER")]
        [InlineData(SqlFunctionId.Max, "MAX")]
        [InlineData(SqlFunctionId.Min, "MIN")]
        [InlineData(SqlFunctionId.Avg, "AVG")]
        [InlineData(SqlFunctionId.Count, "COUNT")]
        public void WrapLeft(SqlFunctionId function, string expectedFunction)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.And()
                .Property(mTable1[1]).Wrap(function)
                .Gt().Value(2);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol("GT_OP");

            var a1 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 1);

            a1.Should().Match(n => n.Symbol.EndsWith("CALL") || n.Symbol == "AGGR_FUNC");

            a1.SelectNode("/*", 1)
                .Should().HaveSymbol(expectedFunction);

            var f = a1.SelectNode("/*", 2);
            f.Should().Exist()
                .And.HaveSymbol("FIELD");

            f.SelectNode("/*", 1)
                .Should().HaveValue("table1");
            f.SelectNode("/*", 2)
                .Should().HaveValue("col2");

            var a2 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 2);
            a2.Should().Exist()
                .And.HaveSymbol("INT")
                .And.HaveValue("2");
        }

        [Theory]
        [InlineData(SqlFunctionId.Abs, "ABS")]
        [InlineData(SqlFunctionId.Upper, "UPPER")]
        [InlineData(SqlFunctionId.Lower, "LOWER")]
        [InlineData(SqlFunctionId.Max, "MAX")]
        [InlineData(SqlFunctionId.Min, "MIN")]
        [InlineData(SqlFunctionId.Avg, "AVG")]
        [InlineData(SqlFunctionId.Sum, "SUM")]
        [InlineData(SqlFunctionId.Count, "COUNT")]
        public void WrapRight(SqlFunctionId function, string expectedFunction)
        {
            var builder = new ConditionBuilder(mProvider);
            SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;
            using var delayed = new DelayedAction(() => SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = true);

            builder.And()
                .Value("a")
                .Gt()
                .Property(mTable1[1]).Wrap(function);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol("GT_OP");

            var a1 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 2);

            a1.Should().Match(n => n.Symbol.EndsWith("CALL") || n.Symbol == "AGGR_FUNC");

            a1.SelectNode("/*", 1)
                .Should().HaveSymbol(expectedFunction);

            var f = a1.SelectNode("/*", 2);
            f.Should().Exist()
                .And.HaveSymbol("FIELD");

            f.SelectNode("/*", 1)
                .Should().HaveValue("table1");
            f.SelectNode("/*", 2)
                .Should().HaveValue("col2");

            var a2 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 1);
            a2.Should().Exist()
                .And.HaveSymbol("STRINGDQ")
                .And.HaveValue("\"a\"");
        }

        [Fact]
        public void CountLeft()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.And()
                .Count()
                .Gt().Value(2);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol("GT_OP");

            var a1 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 1);

            a1.Should().HaveSymbol("AGGR_COUNT_ALL");

            var a2 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 2);
            a2.Should().Exist()
                .And.HaveSymbol("INT")
                .And.HaveValue("2");
        }

        [Fact]
        public void CountRight()
        {
            var builder = new ConditionBuilder(mProvider);
            SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = false;
            using var delayed = new DelayedAction(() => SqlInjectionProtectionPolicy.Instance.ProtectFromScalarsInQueries = true);

            builder.And()
                .Value("a")
                .Gt()
                .Count();

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1)
                .Should().HaveSymbol("GT_OP");

            var a1 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 2);

            a1.Should().HaveSymbol("AGGR_COUNT_ALL");

            var a2 = ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/GT_OP/*", 1);
            a2.Should().Exist()
                .And.HaveSymbol("STRINGDQ")
                .And.HaveValue("\"a\"");
        }

        private void Invoke(string name, ConditionBuilder builder)
        {
            var method = this.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            method.Should()
                .NotBeNull()
                .And.Subject.GetParameters().Should()
                    .HaveCount(1)
                    .And.Subject.As<ParameterInfo[]>()[0].ParameterType
                        .Should().Be(typeof(ConditionBuilder));
            method.Invoke(this, new object[] { builder });
        }

        private void And_IsDefault_Direct(ConditionBuilder builder) => builder.Add("a", CmpOp.Gt, "b");
        private void And_IsDefault_Extension(ConditionBuilder builder) => builder.Raw("a").Is(CmpOp.Gt).Raw("b");
        [Theory]
        [InlineData(nameof(And_IsDefault_Direct))]
        [InlineData(nameof(And_IsDefault_Extension))]
        public void LogOp_And_IsDefault(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("GT_OP");

            builder.Add("c", CmpOp.Le, "d");
            ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("AND_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 1).Should().HaveSymbol("GT_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 2).Should().HaveSymbol("LE_OP");
        }

        private void FirstAnd_ShouldBeIgnored_Direct(ConditionBuilder builder) => builder.Add(LogOp.And, "TRUE");
        private void FirstAnd_ShouldBeIgnored_Extension(ConditionBuilder builder) => builder.And().Raw("TRUE");
        private void FirstOr_ShouldBeIgnored_Direct(ConditionBuilder builder) => builder.Add(LogOp.Or, "TRUE");
        private void FirstOr_ShouldBeIgnored_Extension(ConditionBuilder builder) => builder.Or().Raw("TRUE");

        [Theory]
        [InlineData(nameof(FirstAnd_ShouldBeIgnored_Direct))]
        [InlineData(nameof(FirstAnd_ShouldBeIgnored_Extension))]
        [InlineData(nameof(FirstOr_ShouldBeIgnored_Direct))]
        [InlineData(nameof(FirstOr_ShouldBeIgnored_Extension))]
        public void First_ShouldBeIgnored(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("BOOLEAN_TRUE");
        }

        private void SecondAnd_ShouldBeAdded_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.And, "FALSE");
            builder.Add(LogOp.And, "TRUE");
        }

        private void SecondAnd_ShouldBeAdded_Extension(ConditionBuilder builder)
        {
            builder.And().Raw("FALSE");
            builder.And().Raw("TRUE");
        }

        [Theory]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Direct))]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Extension))]
        public void SecondAnd_ShouldBeAdded(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("AND_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 1).Should().HaveSymbol("BOOLEAN_FALSE");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 2).Should().HaveSymbol("BOOLEAN_TRUE");
        }

        private void SecondOr_ShouldBeAdded_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.Or, "FALSE");
            builder.Add(LogOp.Or, "TRUE");
        }

        private void SecondOr_ShouldBeAdded_Extension(ConditionBuilder builder)
        {
            builder.Or().Raw("FALSE");
            builder.Or().Raw("TRUE");
        }

        [Theory]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Direct))]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Extension))]
        public void SecondOr_ShouldBeAdded(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("OR_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/OR_OP/*", 1).Should().HaveSymbol("BOOLEAN_FALSE");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/OR_OP/*", 2).Should().HaveSymbol("BOOLEAN_TRUE");
        }

        private void Not_FirstArg1_Direct(ConditionBuilder builder) => builder.Add(LogOp.Not, "TRUE");
        private void Not_FirstArg1_Extension(ConditionBuilder builder) => builder.Not().Raw("TRUE");
        private void Not_FirstArg1_ViaGroup(ConditionBuilder builder) => builder.Add(LogOp.Not, g => g.Raw("TRUE"));
        private void Not_FirstArg2_Direct(ConditionBuilder builder) => builder.Add(LogOp.Not | LogOp.And, "TRUE");
        private void Not_FirstArg2_Extension(ConditionBuilder builder) => builder.AndNot().Raw("TRUE");

        [Theory]
        [InlineData(nameof(Not_FirstArg1_Direct))]
        [InlineData(nameof(Not_FirstArg1_Extension))]
        [InlineData(nameof(Not_FirstArg1_ViaGroup))]
        [InlineData(nameof(Not_FirstArg2_Direct))]
        [InlineData(nameof(Not_FirstArg2_Extension))]

        public void Not_FirstArg1(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("NOT_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/NOT_OP/*", 1).Should().HaveSymbol("BOOLEAN_TRUE");
        }

        private void Not_FirstArg3_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.And | LogOp.Not, "TRUE");
            builder.Add(LogOp.And, "FALSE");
        }

        private void Not_FirstArg3_Extension(ConditionBuilder builder)
        {
            builder.Not().Raw("TRUE")
                .And().Raw("FALSE");
        }

        [Theory]
        [InlineData(nameof(Not_FirstArg3_Direct))]
        [InlineData(nameof(Not_FirstArg3_Extension))]
        public void LogOp_Not_FirstArg3(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var ast = ("DEBUG " + builder.ToString()).ParseSql();
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol("AND_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 1).Should().HaveSymbol("NOT_OP");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*[1]/*", 1).Should().HaveSymbol("BOOLEAN_TRUE");
            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/AND_OP/*", 2).Should().HaveSymbol("BOOLEAN_FALSE");
        }

        private void Not_SecondArg1_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.And, "TRUE");
            builder.Add(LogOp.And | LogOp.Not, "FALSE");
        }

        private void Not_SecondArg1_Extension(ConditionBuilder builder)
        {
            builder.Raw("TRUE")
                .AndNot().Raw("FALSE");
        }

        private void Not_SecondArg2_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.And, "TRUE");
            builder.Add(LogOp.Or | LogOp.Not, "FALSE");
        }

        private void Not_SecondArg2_Extension(ConditionBuilder builder)
        {
            builder.Raw("TRUE")
                .OrNot().Raw("FALSE");
        }

        private void Not_SecondArg2_ViaGroup(ConditionBuilder builder)
        {
            builder.Raw("TRUE")
                .AndNot(g => g.Raw("FALSE"));
        }

        [Theory]
        [InlineData(nameof(Not_SecondArg1_Direct), "AND_OP")]
        [InlineData(nameof(Not_SecondArg1_Extension), "AND_OP")]
        [InlineData(nameof(Not_SecondArg2_ViaGroup), "AND_OP")]
        [InlineData(nameof(Not_SecondArg2_Direct), "OR_OP")]
        [InlineData(nameof(Not_SecondArg2_Extension), "OR_OP")]
        public void Not_SecondArg1(string method, string op)
        {
            var builder = new ConditionBuilder(mProvider);
            Invoke(method, builder);
            var ast = ("DEBUG " + builder.ToString()).ParseSql();

            ast.SelectNode("/STATEMENT[1]/DEBUG_EXPR/*", 1).Should().HaveSymbol(op);
            ast.SelectNode($"/STATEMENT[1]/DEBUG_EXPR/{op}/*", 2).Should().HaveSymbol("NOT_OP");
            ast.SelectNode($"/STATEMENT[1]/DEBUG_EXPR/{op}/*[2]/*", 1).Should().HaveSymbol("BOOLEAN_FALSE");
            ast.SelectNode($"/STATEMENT[1]/DEBUG_EXPR/{op}/*", 1).Should().HaveSymbol("BOOLEAN_TRUE");
        }

        [Fact]
        public void ClosingEmptyGroup()
        {
            var builder = new ConditionBuilder(mProvider);
            ((Action)(() => builder.And(g => { }))).Should().Throw<EfSqlException>();
        }

        [Fact]
        public void Terminate_SingleBuilder_ByQueryDirectUsage()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetSelectQueryBuilder(mTable1);
            builder.Where.Raw("TRUE");

            builder.PrepareQuery();
            var ast = (builder.Query).ParseSql();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE")
                .Should().Exist()
                .And.Subject.SelectNode("*", 1)
                    .Should().HaveSymbol("BOOLEAN_TRUE");
        }

        [Fact]
        public void Terminate_SingleBuilder_BySubQueryUsage()
        {
            using var connection = new DummySqlConnection();
            var builder1 = connection.GetSelectQueryBuilder(mTable1);
            builder1.Where.Raw("TRUE");
            var builder2 = connection.GetSelectQueryBuilder(mTable2);
            builder2.Where.Exists().Query(builder1);

            builder2.PrepareQuery();
            var ast = (builder2.Query).ParseSql();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE")
                .Should().Exist()
                .And.Subject.SelectNode("*", 1)
                    .Should().HaveSymbol("EXACT_EXISTS_OP")
                    .And.Subject.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE/*", 1)
                        .Should().Exist()
                        .And.HaveSymbol("BOOLEAN_TRUE");
        }

        [Fact]
        public void Terminate_SingleBuilder_ByAnotherCondition()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetSelectQueryBuilder(mTable1);

            builder.Where.Raw("TRUE")
                .And().Raw("FALSE");

            builder.PrepareQuery();
            var ast = (builder.Query).ParseSql();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE")
                .Should().Exist();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE/*", 1)
                .Should().Exist()
                .And.HaveSymbol("AND_OP")
                .And.Subject.Should()
                    .Match(n => n.SelectNode("/*", 1).Symbol == "BOOLEAN_TRUE")
                    .And.Match(n => n.SelectNode("/*", 2).Symbol == "BOOLEAN_FALSE");
        }

        [Fact]
        public void Terminate_SingleBuilder_ByGroups()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetSelectQueryBuilder(mTable1);
            builder.Where.Raw("TRUE")
                .And(g => g.Raw("FALSE"));

            builder.PrepareQuery();
            var ast = (builder.Query).ParseSql();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE")
                .Should().Exist();
            ast.SelectNode("/SELECT/TABLE_EXPRESSION/WHERE_CLAUSE/*", 1)
                .Should().Exist()
                .And.HaveSymbol("AND_OP")
                .And.Subject.Should()
                    .Match(n => n.SelectNode("/*", 1).Symbol == "BOOLEAN_TRUE")
                    .And.Match(n => n.SelectNode("/*", 2).Symbol == "BOOLEAN_FALSE");
        }
    }
}

