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
using Hime.SDK.Grammars.LR;
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col1");
        }

        [Fact]
        public void PropertyWithEntity()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(new QueryBuilderEntity(mProvider), mTable1[0]);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col1");
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeCallExpression(expected);

            expr.ExprFnCallArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col1");
        }

        [Fact]
        public void Parameter()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Parameter("a");

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeParamExpression()
                .And.HaveParamName("a");
        }

        [Fact]
        public void Parameters()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Property(mTable1[0]).In().Parameters(new string[] { "a", "b", "c" });

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeOpExpression("IN_OP");

            expr.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col1");

            var l = expr.ExprOpArg(1).Children;
            l.Should().HaveCount(3);

            l[0].Should().BeParamExpression().And.HaveParamName("a");
            l[1].Should().BeParamExpression().And.HaveParamName("b");
            l[2].Should().BeParamExpression().And.HaveParamName("c");
        }

        [Theory]
        [InlineData(CmpOp.Eq, "EQ_OP")]
        [InlineData(CmpOp.Neq, "NEQ_OP")]
        [InlineData(CmpOp.Ls, "LT_OP")]
        [InlineData(CmpOp.Le, "LE_OP")]
        [InlineData(CmpOp.Gt, "GT_OP")]
        [InlineData(CmpOp.Ge, "GE_OP")]
        [InlineData(CmpOp.Like, "LIKE_OP")]
        public void BinaryOp(CmpOp op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Raw("1").Is(op).Raw("2");

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().BeConstant(1);
            expr.ExprOpArg(1).Should().BeConstant(2);
        }

        [Theory]
        [InlineData(nameof(CmpOp.Eq), "EQ_OP")]
        [InlineData(nameof(CmpOp.Neq), "NEQ_OP")]
        [InlineData(nameof(CmpOp.Ls), "LT_OP")]
        [InlineData(nameof(CmpOp.Le), "LE_OP")]
        [InlineData(nameof(CmpOp.Gt), "GT_OP")]
        [InlineData(nameof(CmpOp.Ge), "GE_OP")]
        [InlineData(nameof(CmpOp.Like), "LIKE_OP")]
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression(expectedOp)
                .And.BeBinaryOp();

            expr.ExprOpArg(0).Should().BeConstant(1);
            expr.ExprOpArg(1).Should().BeConstant(2);
        }

        [Theory]
        [InlineData(CmpOp.IsNull, "NULL_OP")]
        [InlineData(CmpOp.NotNull, "NOT_NULL_OP")]
        public void Unart(CmpOp op, string expectedOp)
        {
            var builder = new ConditionBuilder(mProvider);
            builder.Raw("1").Is(op);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeOpExpression(expectedOp)
                .And.BeUnaryOp();

            expr.ExprOpArg(0).Should().BeConstant(1);
        }

        [Theory]
        [InlineData(nameof(CmpOp.IsNull), "NULL_OP")]
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeOpExpression(expectedOp)
                .And.BeUnaryOp();

            expr.ExprOpArg(0).Should().BeConstant(1);
        }

        [Theory]
        [InlineData(CmpOp.In, true, "IN_OP")]
        [InlineData(CmpOp.NotIn, true, "NOT_IN_OP")]
        [InlineData(CmpOp.Exists, false, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, false, "NOT_EXISTS_OP")]
        public void QueryOp(CmpOp op, bool hasLeft, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            var select = connection.GetSelectQueryBuilder(mTable2);
            select.AddToResultset(mTable2[0]);

            var builder = new ConditionBuilder(mProvider);
            builder.Add(hasLeft ? "1" : null, op, builder.Query(select));

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression(expectedOp);

            var arg = expr.ExprOpArg(hasLeft ? 1 : 0);
            arg.Should().HaveSymbol("SELECT");
        }

        [Theory]
        [InlineData(nameof(CmpOp.In), true, "IN_OP")]
        [InlineData(nameof(CmpOp.NotIn), true, "NOT_IN_OP")]
        [InlineData(nameof(CmpOp.Exists), false, "EXISTS_OP")]
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression(expectedOp);

            var arg = expr.ExprOpArg(hasLeft ? 1 : 0);
            arg.Should().HaveSymbol("SELECT");
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.ExprOp().Should().Be("GT_OP");

            var l = expr.ExprOpArg(0);

            l.Should().BeCallExpression(expectedFunction);

            var a = l.ExprFnCallArg(0);

            a.Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col2");

            expr.ExprOpArg(1)
                .Should().BeConstant(2);
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("GT_OP")
                .And.ItsParameter(0, p => p.Should().BeConstant("a"))
                .And.ItsParameter(1, p =>
                    p.Should().BeCallExpression(expectedFunction)
                              .And.ItsParameter(0, p =>
                                    p.Should().BeFieldExpression()
                                              .And.HaveFieldAlias("table1")
                                              .And.HaveFieldName("col2")));
        }

        [Theory]
        [InlineData(nameof(SqlFunctionId.Abs), "ABS")]
        [InlineData(nameof(SqlFunctionId.Upper), "UPPER")]
        [InlineData(nameof(SqlFunctionId.Lower), "LOWER")]
        [InlineData(nameof(SqlFunctionId.Max), "MAX")]
        [InlineData(nameof(SqlFunctionId.Min), "MIN")]
        [InlineData(nameof(SqlFunctionId.Avg), "AVG")]
        [InlineData(nameof(SqlFunctionId.Sum), "SUM")]
        public void WrapViaExension(string function, string expectedFunction)
        {
            var builder = new ConditionBuilder(mProvider);

            var m = typeof(SingleConditionBuilderExtension).GetMethod(function);
            m.Should().NotBeNull()
                .And.Subject.GetParameters()
                    .Should().HaveCount(1)
                    .And.Subject.As<ParameterInfo[]>()[0].ParameterType
                        .Should().Be(typeof(SingleConditionBuilder));

            var sb = builder.And().Property(mTable1[1]);
            sb = (SingleConditionBuilder)m.Invoke(null, new object[] { sb });
            sb.Gt().Value(2);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.ExprOp().Should().Be("GT_OP");

            var l = expr.ExprOpArg(0);

            l.Should().BeCallExpression(expectedFunction);

            var a = l.ExprFnCallArg(0);

            a.Should().BeFieldExpression()
                .And.HaveFieldAlias("table1")
                .And.HaveFieldName("col2");

            expr.ExprOpArg(1)
                .Should().BeConstant(2);
        }

        [Fact]
        public void CountLeft()
        {
            var builder = new ConditionBuilder(mProvider);
            builder.And()
                .Count()
                .Gt().Value(2);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeOpExpression("GT_OP")
                .And.ItsParameter(0, p => p.Should().BeCountAllCall())
                .And.ItsParameter(1, p => p.Should().BeConstant(2));
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should()
                .BeOpExpression("GT_OP")
                .And.ItsParameter(0, p => p.Should().BeConstant("a"))
                .And.ItsParameter(1, p => p.Should().BeCountAllCall());
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.ExprOp().Should().Be("GT_OP");

            builder.Add("c", CmpOp.Le, "d");
            expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("AND_OP")
                .And.ItsParameter(0, p =>
                    p.Should().BeOpExpression("GT_OP")
                        .And.ItsParameter(0, p => p.Should().BeFieldExpression().And.HaveFieldName("a"))
                        .And.ItsParameter(1, p => p.Should().BeFieldExpression().And.HaveFieldName("b")))
                .And.ItsParameter(1, p =>
                    p.Should().BeOpExpression("LE_OP")
                        .And.ItsParameter(0, p => p.Should().BeFieldExpression().And.HaveFieldName("c"))
                        .And.ItsParameter(1, p => p.Should().BeFieldExpression().And.HaveFieldName("d")));
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeConstant(true);
        }

        private void SecondAnd_ShouldBeAdded_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.And, "FALSE");
            builder.Add(LogOp.And, "TRUE");
        }

        private void SecondAnd_ShouldBeAdded_Extension1(ConditionBuilder builder)
        {
            builder.And().Raw("FALSE");
            builder.And().Raw("TRUE");
        }

        private void SecondAnd_ShouldBeAdded_Extension2(ConditionBuilder builder)
        {
            builder.And()
                .Raw("FALSE")
                .And().Raw("TRUE");
        }

        private void SecondAnd_ShouldBeAdded_Extension3(ConditionBuilder builder)
        {
            builder.And()
                .Raw("FALSE")
                .And(g => g.Raw("TRUE"));
        }

        [Theory]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Direct))]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Extension1))]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Extension2))]
        [InlineData(nameof(SecondAnd_ShouldBeAdded_Extension3))]
        public void SecondAnd_ShouldBeAdded(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("AND_OP")
                .And.ItsParameter(0, p => p.Should().BeConstant(false))
                .And.ItsParameter(1, p => p.Should().BeConstant(true));
        }

        private void SecondOr_ShouldBeAdded_Direct(ConditionBuilder builder)
        {
            builder.Add(LogOp.Or, "FALSE");
            builder.Add(LogOp.Or, "TRUE");
        }

        private void SecondOr_ShouldBeAdded_Extension1(ConditionBuilder builder)
        {
            builder.Or().Raw("FALSE");
            builder.Or().Raw("TRUE");
        }

        private void SecondOr_ShouldBeAdded_Extension2(ConditionBuilder builder)
        {
            builder.Or().Raw("FALSE")
                .Or().Raw("TRUE");
        }

        private void SecondOr_ShouldBeAdded_Extension3(ConditionBuilder builder)
        {
            builder.Or().Raw("FALSE")
                .Or(g => g.Raw("TRUE"));
        }

        [Theory]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Direct))]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Extension1))]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Extension2))]
        [InlineData(nameof(SecondOr_ShouldBeAdded_Extension3))]
        public void SecondOr_ShouldBeAdded(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression("OR_OP")
                .And.ItsParameter(0, p => p.Should().BeConstant(false))
                .And.ItsParameter(1, p => p.Should().BeConstant(true));
        }

        private void Not_FirstArg1_Direct(ConditionBuilder builder) => builder.Add(LogOp.Not, "TRUE");
        private void Not_FirstArg1_Extension(ConditionBuilder builder) => builder.Not().Raw("TRUE");
        private void Not_FirstArg1_ViaGroup(ConditionBuilder builder) => builder.Add(LogOp.Not, g => g.Raw("TRUE"));
        private void Not_FirstArg2_Direct(ConditionBuilder builder) => builder.Add(LogOp.Not | LogOp.And, "TRUE");
        private void Not_FirstArg2_Extension1(ConditionBuilder builder) => builder.AndNot().Raw("TRUE");
        private void Not_FirstArg2_Extension2(ConditionBuilder builder) => builder.OrNot().Raw("TRUE");

        [Theory]
        [InlineData(nameof(Not_FirstArg1_Direct))]
        [InlineData(nameof(Not_FirstArg1_Extension))]
        [InlineData(nameof(Not_FirstArg1_ViaGroup))]
        [InlineData(nameof(Not_FirstArg2_Direct))]
        [InlineData(nameof(Not_FirstArg2_Extension1))]
        [InlineData(nameof(Not_FirstArg2_Extension2))]

        public void Not_FirstArg1(string method)
        {
            var builder = new ConditionBuilder(mProvider);

            Invoke(method, builder);

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("NOT_OP")
                .And.ItsParameter(0, p => p.Should().BeConstant(true));
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("AND_OP")
                .And.ItsParameter(0, p =>
                        p.Should().BeOpExpression("NOT_OP")
                                  .And.ItsParameter(0, p => p.Should().BeConstant(true)))
                .And.ItsParameter(1, p => p.Should().BeConstant(false));
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

            var expr = ("DEBUG " + builder.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression(op)
                .And.ItsParameter(0, p => p.Should().BeConstant(true))
                .And.ItsParameter(1, p =>
                    p.Should().BeOpExpression("NOT_OP")
                              .And.ItsParameter(0, p => p.Should().BeConstant(false)));
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
            var select = (builder.Query).ParseSql().SelectStatement();
            select.SelectWhere().Should().NotBeNull();
            select.SelectWhere().ClauseCondition().ExprIsTrue().Should().BeTrue();
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

            var select = (builder2.Query).ParseSql().SelectStatement();
            select.SelectWhere().Should().NotBeNull();

            select.SelectWhere().Should().NotBeNull();
            select.SelectWhere().ClauseCondition().ExprOp().Should().Be("EXISTS_OP");

            var subselect = select.SelectWhere().ClauseCondition().ExprOpArg(0);
            subselect.SelectWhere().ClauseCondition().ExprIsTrue().Should().BeTrue();
        }

        [Fact]
        public void Terminate_SingleBuilder_ByAnotherCondition()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetSelectQueryBuilder(mTable1);

            builder.Where.Raw("TRUE")
                .And().Raw("FALSE");

            builder.PrepareQuery();
            var select = (builder.Query).ParseSql().SelectStatement();

            select.SelectWhere().Should().NotBeNull();
            var expr = select.SelectWhere().ClauseCondition();

            expr.ExprOp().Should().Be("AND_OP");
            expr.ExprOpArg(0).ExprIsTrue().Should().BeTrue();
            expr.ExprOpArg(1).ExprIsFalse().Should().BeTrue();
        }

        [Fact]
        public void Terminate_SingleBuilder_ByGroups()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetSelectQueryBuilder(mTable1);
            builder.Where.Raw("TRUE")
                .OrNot(g => g.Raw("FALSE"));
            builder.PrepareQuery();
            var select = (builder.Query).ParseSql().SelectStatement();

            select.SelectWhere().Should().NotBeNull();
            var expr = select.SelectWhere().ClauseCondition();

            expr.ExprOp().Should().Be("OR_OP");
            expr.ExprOpArg(0).ExprIsTrue().Should().BeTrue();
            expr.ExprOpArg(1).ExprOp().Should().Be("NOT_OP");
            expr.ExprOpArg(1).ExprOpArg(0).ExprIsFalse().Should().BeTrue();
        }
    }
}


