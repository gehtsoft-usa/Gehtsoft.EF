using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils;
using Gehtsoft.EF.Test.Utils.DummyDb;
using FluentAssertions;
using Xunit;
using Hime.SDK.Grammars.LR;
using System.Data;
using System.Reflection;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class ConditionBuilderTest
    {
        [Entity(Scope = "condition_builder", Table = "dict1")]
        public class Dict1
        {
            [AutoId]
            public int Id { get; set; }
            [EntityProperty(Field = "n")]
            public int N { get; set; }
        }

        [Entity(Scope = "condition_builder", Table = "table1")]
        public class Entity1
        {
            [AutoId]
            public int Id { get; set; }
            [EntityProperty(Field = "a")]
            public int A { get; set; }
            [EntityProperty(Field = "b")]
            public int B { get; set; }
            [ForeignKey(Field = "d")]
            public Dict1 D { get; set; }
            [ForeignKey(Field = "d1")]
            public Dict1 D1 { get; set; }
        }

        [Fact]
        public void Argument_Raw_CompleteExpression()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Add().Raw($"{alias1}.a = {alias1}.b");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");
        }

        [Fact]
        public void Argument_Raw_Arguments()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Add().Raw($"{alias1}.a").Eq().Raw($"{alias1}.b");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");
        }

        [Fact]
        public void Argument_Error_InjectionProtection()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Raw($"{alias1}.a = 'quoted'"))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Argument_Error_TryToSetTwoLeftArgs()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Raw($"{alias1}.a")
                                             .Raw($"{alias1}.b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Argument_Error_TryToSetTwoOps()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Raw($"{alias1}.a")
                                             .Is(CmpOp.Eq)
                                             .Is(CmpOp.Neq)
                                             .Raw($"{alias1}.b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Argument_Error_TryToSetTwoRightArgs()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Raw($"{alias1}.a")
                                             .Is(CmpOp.Eq)
                                             .Raw($"{alias1}.b")
                                             .Raw($"{alias1}.b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Argument_Error_ValueAtLeft()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Value(1)
                                             .Is(CmpOp.Eq)
                                             .Raw($"{alias1}.b").Raw($"{alias1}.b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Argument_Error_ValuesAtLeft()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            ((Action)(() => query.Where.Add().Value(new object[] { 1, 2 })
                                             .Is(CmpOp.Eq)
                                             .Raw($"{alias1}.b").Raw($"{alias1}.b"))).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Argument_Property_ByPath()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;
            var alias2 = (query.Builder as QueryWithWhereBuilder).Entities[1].Alias;

            query.Where.Property("A").Eq().Property("D.N");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("n");
        }

        [Fact]
        public void Argument_Property_ByReference()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;
            var alias3 = (query.Builder as QueryWithWhereBuilder).Entities[2].Alias;

            var r1 = query.GetReference(typeof(Entity1), "A");
            var r2 = query.GetReference(typeof(Dict1), 1, "N");

            query.Where.Add().Reference(r1).Eq().Reference(r2);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("n");
        }

        [Fact]
        public void Argument_Property_ByType()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;
            var alias2 = (query.Builder as QueryWithWhereBuilder).Entities[1].Alias;

            query.Where.PropertyOf("A", typeof(Entity1)).Eq().PropertyOf<Dict1>("N");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("n");
        }

        [Fact]
        public void Argument_Property_ByTypeAndOccurrence()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias2 = (query.Builder as QueryWithWhereBuilder).Entities[1].Alias;
            var alias3 = (query.Builder as QueryWithWhereBuilder).Entities[2].Alias;

            query.Where.PropertyOf("N", typeof(Dict1), 0).Eq().PropertyOf<Dict1>("N", 1);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("n");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("n");
        }

        [Fact]
        public void Argument_Parameter()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("B").Eq().Parameter("P");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");

            expr.ExprOpArg(1).Should().BeParamExpression()
                .And.HaveParamName("P");
        }

        [Fact]
        public void Argument_ParameterGroup()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("B").In().Parameters(new string[] { "P1", "P2" });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("IN_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");

            var l = expr.ExprOpArg(1).Children;

            l.Should().HaveCount(2);

            l[0].Should().BeParamExpression().And.HaveParamName("P1");
            l[1].Should().BeParamExpression().And.HaveParamName("P2");
        }

        [Fact]
        public void Argument_Value()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("B").Eq().Value("Hello");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");

            expr.ExprOpArg(1).Should().BeParamExpression();

            var paramName = expr.ExprOpArg(1).ExprParamName();

            query.GetParamValue<string>(paramName).Should().Be("Hello");
        }

        [Fact]
        public void Argument_ValueNull()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("B").Eq().Value(null, DbType.Int32);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");

            expr.ExprOpArg(1).Should().BeParamExpression();

            var paramName = expr.ExprOpArg(1).ExprParamName();

            query.GetParamValue<int?>(paramName).Should().BeNull();
        }

        [Fact]
        public void Argument_Values()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("B").In().Values(1, "2", 3.5);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("IN_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("b");

            var l = expr.ExprOpArg(1).Children;

            l.Should().HaveCount(3);

            l[0].Should().BeParamExpression();
            l[1].Should().BeParamExpression();
            l[2].Should().BeParamExpression();

            query.GetParamValue<object>(l[0].ExprParamName()).Should().Be(1);
            query.GetParamValue<object>(l[1].ExprParamName()).Should().Be("2");
            query.GetParamValue<object>(l[2].ExprParamName()).Should().Be(3.5);
        }

        [Theory]
        [InlineData(CmpOp.Eq, "EQ_OP")]
        [InlineData(CmpOp.Neq, "NEQ_OP")]
        [InlineData(CmpOp.Ls, "LT_OP")]
        [InlineData(CmpOp.Le, "LE_OP")]
        [InlineData(CmpOp.Gt, "GT_OP")]
        [InlineData(CmpOp.Ge, "GE_OP")]
        [InlineData(CmpOp.Like, "LIKE_OP")]
        public void BinaryOperator_Directly(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            query.Where.Property("A").Is(op).Property("B");
            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldName("b");
        }

        [Theory]
        [InlineData(CmpOp.Eq, "EQ_OP")]
        [InlineData(CmpOp.Neq, "NEQ_OP")]
        [InlineData(CmpOp.Ls, "LT_OP")]
        [InlineData(CmpOp.Le, "LE_OP")]
        [InlineData(CmpOp.Gt, "GT_OP")]
        [InlineData(CmpOp.Ge, "GE_OP")]
        [InlineData(CmpOp.Like, "LIKE_OP")]
        public void BinaryOperator_Extension1(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder) });
            m.Should().NotBeNull();

            var b = query.Where.Property("A");
            m.Invoke(null, new object[] { b });
            b.Property("B");
            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldName("b");
        }

        [Theory]
        [InlineData(CmpOp.Eq, "EQ_OP", 5)]
        [InlineData(CmpOp.Neq, "NEQ_OP", 1.5)]
        [InlineData(CmpOp.Ls, "LT_OP", (short)4)]
        [InlineData(CmpOp.Le, "LE_OP", 12345678)]
        [InlineData(CmpOp.Gt, "GT_OP", 1)]
        [InlineData(CmpOp.Ge, "GE_OP", -10)]
        [InlineData(CmpOp.Like, "LIKE_OP", "abcd", typeof(string))]
        public void BinaryOperator_Extension2(CmpOp op, string expectedOp, object arg, Type argType = null)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder), argType ?? typeof(object) });
            m.Should().NotBeNull();

            var b = query.Where.Property("A");
            m.Invoke(null, new object[] { b, arg });
            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(1).Should().BeParamExpression();

            var paramName = expr.ExprOpArg(1).ExprParamName();

            query.GetParamValue<object>(paramName).Should().Be(arg);

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeParamExpression();
        }

        [Theory]
        [InlineData(CmpOp.IsNull, "NULL_OP")]
        [InlineData(CmpOp.NotNull, "NOT_NULL_OP")]
        public void UnaryPostfixOperator_Directly(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            query.Where.Property("A").Is(op);
            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");
        }

        [Theory]
        [InlineData(CmpOp.IsNull, "NULL_OP")]
        [InlineData(CmpOp.NotNull, "NOT_NULL_OP")]
        public void UnaryPostfixOperator_Extension(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder) });
            m.Should().NotBeNull();

            var b = query.Where.Property("A");
            m.Invoke(null, new object[] { b });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");
        }

        [Theory]
        [InlineData(CmpOp.Exists, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, "NOT_EXISTS_OP")]
        public void UnaryPrefixOperator_Directly(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            query.Where.Is(op).Query(subquery);
            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(0);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.Exists, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, "NOT_EXISTS_OP")]
        public void UnaryPrefixOperator_Extension1(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder) });
            m.Should().NotBeNull();

            var sb = query.Where.Add();
            m.Invoke(null, new object[] { sb });
            sb.Query(subquery);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(0);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.Exists, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, "NOT_EXISTS_OP")]
        public void UnaryPrefixOperator_Extension2(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(EntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(EntityQueryConditionBuilder), typeof(SelectEntitiesQueryBase) });
            m.Should().NotBeNull();

            var sb = m.Invoke(null, new object[] { query.Where, subquery }) as SingleEntityQueryConditionBuilder;
            sb.Should().NotBeNull();

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(0);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.Exists, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, "NOT_EXISTS_OP")]
        public void UnaryPrefixOperator_Extension3(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder), typeof(SelectEntitiesQueryBase) });
            m.Should().NotBeNull();

            var sb = query.Where.Add();
            m.Invoke(null, new object[] { sb, subquery });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(0);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.Exists, "EXISTS_OP")]
        [InlineData(CmpOp.NotExists, "NOT_EXISTS_OP")]
        public void UnaryPrefixOperator_Extension4(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var td = AllEntities.Get<Dict1>().TableDescriptor;
            var subquery = connection.GetSelectQueryBuilder(td);
            subquery.AddToResultset(td[0]);

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder), typeof(AQueryBuilder) });
            m.Should().NotBeNull();

            var sb = query.Where.Add();
            m.Invoke(null, new object[] { sb, subquery });
            sb.Should().NotBeNull();

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(0).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(0);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.In, "IN_OP")]
        [InlineData(CmpOp.NotIn, "NOT_IN_OP")]
        public void BinaryGroupOperator_Extension1(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder) });
            m.Should().NotBeNull();

            var sb = query.Where.Add().Property("A");
            m.Invoke(null, new object[] { sb });
            sb.Query(subquery);

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(1).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(1);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Theory]
        [InlineData(CmpOp.In, "IN_OP")]
        [InlineData(CmpOp.NotIn, "NOT_IN_OP")]
        public void BinaryGroupOperator_Extension2(CmpOp op, string expectedOp)
        {
            using var connection = new DummySqlConnection();
            using var subquery = connection.GetSelectEntitiesQuery<Dict1>();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension).GetMethod(op.ToString(), new Type[] { typeof(SingleEntityQueryConditionBuilder), typeof(SelectEntitiesQueryBase) });
            m.Should().NotBeNull();

            var sb = query.Where.Add().Property("A");
            m.Invoke(null, new object[] { sb, subquery });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();
            expr.Should().BeOpExpression(expectedOp);

            expr.ExprOpArg(1).Should().HaveSymbol("SELECT");

            var select = expr.ExprOpArg(1);

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0).Should()
                .HaveTableName("dict1")
                .And.NotBeJoin();
        }

        private static void LogOp_First_Ignored_Add_Default(EntityQueryConditionBuilder builder)
            => builder.Add().Raw("TRUE");

        private static void LogOp_First_Ignored_Add_And(EntityQueryConditionBuilder builder)
            => builder.Add(LogOp.And).Raw("TRUE");

        private static void LogOp_First_Ignored_Add_AndNot(EntityQueryConditionBuilder builder)
            => builder.Add(LogOp.And | LogOp.Not).Raw("TRUE");

        private static void LogOp_First_Ignored_Add_Or(EntityQueryConditionBuilder builder)
            => builder.Add(LogOp.Or).Raw("TRUE");

        private static void LogOp_First_Ignored_Add_OrNot(EntityQueryConditionBuilder builder)
            => builder.Add(LogOp.Or | LogOp.Not).Raw("TRUE");

        private static void LogOp_First_Ignored_Ext_And(EntityQueryConditionBuilder builder)
            => builder.And().Raw("TRUE");

        private static void LogOp_First_Ignored_Ext_AndNot(EntityQueryConditionBuilder builder)
            => builder.AndNot().Raw("TRUE");

        private static void LogOp_First_Ignored_Ext_Or(EntityQueryConditionBuilder builder)
            => builder.Or().Raw("TRUE");

        private static void LogOp_First_Ignored_Ext_OrNot(EntityQueryConditionBuilder builder)
            => builder.OrNot().Raw("TRUE");

        private static void LogOp_First_Ignored_Grp_And(EntityQueryConditionBuilder builder)
            => builder.And(g => g.Raw("TRUE"));

        private static void LogOp_First_Ignored_Grp_AndNot(EntityQueryConditionBuilder builder)
            => builder.AndNot(g => g.Raw("TRUE"));

        private static void LogOp_First_Ignored_Grp_Or(EntityQueryConditionBuilder builder)
            => builder.Or(g => g.Raw("TRUE"));

        private static void LogOp_First_Ignored_Grp_OrNot(EntityQueryConditionBuilder builder)
            => builder.OrNot(g => g.Raw("TRUE"));

        [Theory]
        [InlineData(nameof(LogOp_First_Ignored_Add_Default), false)]
        [InlineData(nameof(LogOp_First_Ignored_Add_And), false)]
        [InlineData(nameof(LogOp_First_Ignored_Add_Or), false)]
        [InlineData(nameof(LogOp_First_Ignored_Add_AndNot), true)]
        [InlineData(nameof(LogOp_First_Ignored_Add_OrNot), true)]
        [InlineData(nameof(LogOp_First_Ignored_Ext_And), false)]
        [InlineData(nameof(LogOp_First_Ignored_Ext_Or), false)]
        [InlineData(nameof(LogOp_First_Ignored_Ext_AndNot), true)]
        [InlineData(nameof(LogOp_First_Ignored_Ext_OrNot), true)]
        [InlineData(nameof(LogOp_First_Ignored_Grp_And), false)]
        [InlineData(nameof(LogOp_First_Ignored_Grp_Or), false)]
        [InlineData(nameof(LogOp_First_Ignored_Grp_AndNot), true)]
        [InlineData(nameof(LogOp_First_Ignored_Grp_OrNot), true)]
        public void LogOp_First_Ignored(string stageMethod, bool hasNot)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = this.GetType().GetMethod(stageMethod, BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();
            m.Invoke(null, new object[] { query.Where });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            if (hasNot)
            {
                expr.Should().BeUnaryOp("NOT_OP");
                expr.ExprOpArg(0).Should().BeConstant(true);
            }
            else
            {
                expr.Should().BeConstant(true);
            }
        }

        private static void LogOp_Second_Ignored_Add_Default(EntityQueryConditionBuilder builder)
        {
            builder.Raw("TRUE");
            builder.Add().Raw("FALSE");
        }

        private static void LogOp_Second_Ignored_Add_And(EntityQueryConditionBuilder builder)
        {
            builder.Raw("TRUE");
            builder.Add(LogOp.And).Raw("FALSE");
        }

        private static void LogOp_Second_Ignored_Add_AndNot(EntityQueryConditionBuilder builder)
        {
            builder.Raw("TRUE");
            builder.Add(LogOp.And | LogOp.Not).Raw("FALSE");
        }

        private static void LogOp_Second_Ignored_Add_Or(EntityQueryConditionBuilder builder)
        {
            builder.Raw("TRUE");
            builder.Add(LogOp.Or).Raw("FALSE");
        }

        private static void LogOp_Second_Ignored_Add_OrNot(EntityQueryConditionBuilder builder)
        {
            builder.Raw("TRUE");
            builder.Add(LogOp.Or | LogOp.Not).Raw("FALSE");
        }

        private static void LogOp_Second_Ignored_Ext_And(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").And().Raw("FALSE");

        private static void LogOp_Second_Ignored_Ext_AndNot(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").AndNot().Raw("FALSE");

        private static void LogOp_Second_Ignored_Ext_Or(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").Or().Raw("FALSE");

        private static void LogOp_Second_Ignored_Ext_OrNot(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").OrNot().Raw("FALSE");

        private static void LogOp_Second_Ignored_Grp_And(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").And(g => g.Raw("FALSE"));

        private static void LogOp_Second_Ignored_Grp_AndNot(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").AndNot(g => g.Raw("FALSE"));

        private static void LogOp_Second_Ignored_Grp_Or(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").Or(g => g.Raw("FALSE"));

        private static void LogOp_Second_Ignored_Grp_OrNot(EntityQueryConditionBuilder builder)
            => builder.Raw("TRUE").OrNot(g => g.Raw("FALSE"));

        [Theory]
        [InlineData(nameof(LogOp_Second_Ignored_Add_Default), "AND_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Add_And), "AND_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Add_Or), "OR_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Add_AndNot), "AND_OP", true)]
        [InlineData(nameof(LogOp_Second_Ignored_Add_OrNot), "OR_OP", true)]
        [InlineData(nameof(LogOp_Second_Ignored_Ext_And), "AND_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Ext_Or), "OR_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Ext_AndNot), "AND_OP", true)]
        [InlineData(nameof(LogOp_Second_Ignored_Ext_OrNot), "OR_OP", true)]
        [InlineData(nameof(LogOp_Second_Ignored_Grp_And), "AND_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Grp_Or), "OR_OP", false)]
        [InlineData(nameof(LogOp_Second_Ignored_Grp_AndNot), "AND_OP", true)]
        [InlineData(nameof(LogOp_Second_Ignored_Grp_OrNot), "OR_OP", true)]
        public void LogOp_Second_Handled(string stageMethod, string op, bool hasNot)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = this.GetType().GetMethod(stageMethod, BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull();
            m.Invoke(null, new object[] { query.Where });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeBinaryOp(op);
            expr.ExprOpArg(0).Should().BeConstant(true);
            if (hasNot)
            {
                expr.ExprOpArg(1).Should().BeUnaryOp("NOT_OP");
                expr.ExprOpArg(1).ExprOpArg(0).Should().BeConstant(false);
            }
            else
                expr.ExprOpArg(1).Should().BeConstant(false);
        }

        [Fact]
        public void Function_Count_At_Left()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Add().Count().Eq().Property("A");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeCountAllCall();

            expr.ExprOpArg(1).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");
        }

        [Fact]
        public void Function_WrapTwice()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            query.Where.Add().Property("A").Trim().ToLower().Eq().Value("A");

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeCallExpression("LOWER");
            expr.ExprOpArg(0).ExprFnCallArg(0).Should().BeCallExpression("TRIM");
            expr.ExprOpArg(0).ExprFnCallArg(0).ExprFnCallArg(0).Should().BeFieldExpression().And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeParamExpression();
        }

        [Fact]
        public void Function_Count_At_Right()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();
            var alias1 = (query.Builder as QueryWithWhereBuilder).Entities[0].Alias;

            query.Where.Property("A").Eq().Count();

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeCountAllCall();
        }

        [Theory]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Sum), "SUM")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Avg), "AVG")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Min), "MIN")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Max), "MAX")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Count), "COUNT")]

        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Abs), "ABS")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Round), "ROUND", 1)]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Left), "LEFT", 1)]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Trim), "TRIM")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.ToLower), "LOWER")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.ToUpper), "UPPER")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Year), "YEAR")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Month), "MONTH")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Day), "DAY")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Hour), "HOUR")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Minute), "MINUTE")]
        [InlineData(nameof(SingleEntityQueryConditionBuilderExtension.Second), "SECOND")]
        public void Function_Wrap(string name, string function, object arg1 = null)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQuery<Entity1>();

            var m = typeof(SingleEntityQueryConditionBuilderExtension)
                .GetMethod(name,
                    arg1 == null ? new Type[] { typeof(SingleEntityQueryConditionBuilder) }
                                 : new Type[] { typeof(SingleEntityQueryConditionBuilder), arg1.GetType() });
            m.Should().NotBeNull();

            var sb = query.Where.Add().Property("A").Eq().Property("B");
            m.Invoke(null, arg1 == null ? new object[] { sb } : new object[] { sb, arg1 });

            var expr = ("DEBUG " + query.Where.ToString()).ParseSql().Statement(0).DebugExpr();

            expr.Should().BeOpExpression("EQ_OP");

            expr.ExprOpArg(0).Should().BeFieldExpression()
                .And.HaveFieldName("a");

            expr.ExprOpArg(1).Should().BeCallExpression(function);
            expr.ExprOpArg(1).ExprFnCallArg(0).Should()
                .BeFieldExpression()
                .And.HaveFieldName("b");
        }
    }
}
