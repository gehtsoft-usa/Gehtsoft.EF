using System;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.DynamicProperties.DataSelecting
{
    /// <summary>
    /// Deep (white-box) tests for projecting a dynamic property into a custom entity query
    /// (<see cref="SelectEntitiesQueryBase"/>). They assert the generated SQL via its parsed AST -
    /// the LEFT JOIN to the side table, its `owner = pk AND name = @p` condition, the value column
    /// chosen per type, aggregate wrapping, and that repeated projections reuse / add joins.
    /// </summary>
    public class DynamicPropertiesProjectionSqlTest
    {
        private const string OwnerTable = "dp_p3_owner";
        private const string PropsTable = "dp_p3_owner_props";

        [Entity(Scope = "dp_p3", Table = OwnerTable)]
        [DynamicProperties]
        public class Owner : IDynamicPropertiesOwner
        {
            [AutoId] public int Id { get; set; }
            [EntityProperty(Size = 32, Nullable = true)] public string Name { get; set; }
            public DynamicPropertyBag DynamicProperties { get; private set; }
        }

        [Fact]
        public void Resultset_JoinsSideTable_LeftJoin_WithNameFilter()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "color");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables().Should().HaveCount(2);
            select.Table(0).Should().HaveTableName(OwnerTable).And.NotBeJoin();
            select.Table(1).Should().HaveTableName(PropsTable).And.BeJoin("JOIN_TYPE_LEFT");

            string ownerAlias = select.Table(0).TableAlias().Value;
            string propsAlias = select.Table(1).TableAlias().Value;

            // ON dp.owner = owner.id AND dp.name = @p
            var on = select.Table(1).TableJoinCondition();
            on.Should().BeOpExpression("AND_OP");

            on.ExprOpArg(0).Should().BeOpExpression("EQ_OP");
            on.ExprOpArg(0).ExprOpArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("owner");
            on.ExprOpArg(0).ExprOpArg(1).Should().BeFieldExpression().And.HaveFieldAlias(ownerAlias).And.HaveFieldName("id");

            on.ExprOpArg(1).Should().BeOpExpression("EQ_OP");
            on.ExprOpArg(1).ExprOpArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("name");
            on.ExprOpArg(1).ExprOpArg(1).Should().BeParamExpression();

            // the resultset column is dp.v_str
            select.Should().HaveResultsetSize(1);
            select.ResultsetItem(0).ResultsetExpr()
                .Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("v_str");
        }

        [Theory]
        [InlineData(DynamicPropertyValueType.String, "v_str")]
        [InlineData(DynamicPropertyValueType.Integer, "v_int")]
        [InlineData(DynamicPropertyValueType.Long, "v_int")]
        [InlineData(DynamicPropertyValueType.Boolean, "v_int")]
        [InlineData(DynamicPropertyValueType.DateTime, "v_int")]
        [InlineData(DynamicPropertyValueType.Real, "v_real")]
        public void Resultset_ValueColumn_PerType(DynamicPropertyValueType type, string expectedColumn)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("p", type, "p");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveResultsetSize(1);
            select.ResultsetItem(0).ResultsetExpr()
                .Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName(expectedColumn);
        }

        [Theory]
        [InlineData(AggFn.Min, "MIN")]
        [InlineData(AggFn.Max, "MAX")]
        [InlineData(AggFn.Sum, "SUM")]
        [InlineData(AggFn.Avg, "AVG")]
        public void Resultset_Aggregate_WrapsValueColumn(AggFn aggFn, string expectedFunction)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>(aggFn, "size", DynamicPropertyValueType.Integer, "size");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveResultsetSize(1);
            var expr = select.ResultsetItem(0).ResultsetExpr();
            expr.Should().BeCallExpression(expectedFunction);
            expr.ExprFnCallArgCount().Should().Be(1);
            expr.ExprFnCallArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("v_int");
        }

        [Fact]
        public void Resultset_Aggregate_Count_IsCountDistinctOfValueColumn()
        {
            // AggFn.Count renders COUNT(DISTINCT ...), consistent with the framework's own
            // AddToResultset(AggFn.Count, column).
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>(AggFn.Count, "size", DynamicPropertyValueType.Integer, "cnt");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveResultsetSize(1);
            var expr = select.ResultsetItem(0).ResultsetExpr();
            expr.Should().BeCallExpression("COUNT");
            expr.Value.Should().Be("DISTINCT");
            expr.ExprFnCallArgCount().Should().Be(1);
            expr.ExprFnCallArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("v_int");
        }

        [Fact]
        public void Resultset_SameProperty_SameType_JoinedOnce()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "a");
            query.AddDynamicPropertyToResultset<Owner>(AggFn.Max, "color", DynamicPropertyValueType.String, "b");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            // one owner + one side-table join
            select.AllTables().Should().HaveCount(2);
            select.Should().HaveResultsetSize(2);
        }

        [Fact]
        public void Where_WhenProjected_FiltersJoinedColumnDirectly_NoSubquery()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            // project first, then filter the same property/type -> the WHERE reuses the join
            query.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "color");
            query.Where.DynamicPropertyOf<Owner>("color").Eq("red");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            // owner + one side-table join; the props table is NOT inside a sub-query
            select.AllTables().Should().HaveCount(2);
            string propsAlias = select.Table(1).TableAlias().Value;

            var condition = select.SelectWhere().ClauseCondition();
            condition.Should().BeOpExpression("EQ_OP");
            condition.ExprOpArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("v_str");
            condition.ExprOpArg(1).Should().BeParamExpression();
        }

        [Fact]
        public void Where_WhenNotProjected_UsesInSubquery()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddToResultset(typeof(Owner));
            query.Where.DynamicPropertyOf<Owner>("color").Eq("red");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            // no join added; the side table lives inside the IN sub-query (Phase-2 behavior)
            select.AllTables().Should().HaveCount(1);

            var condition = select.SelectWhere().ClauseCondition();
            condition.Should().BeOpExpression("IN_OP");
            condition.ExprOpArg(1).Should().BeSubquery();
        }

        [Fact]
        public void Where_WhenProjectedDifferentType_FallsBackToInSubquery()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            // projected as String, but filtered with an integer operand -> types differ -> no reuse
            query.AddDynamicPropertyToResultset<Owner>("size", DynamicPropertyValueType.String, "s");
            query.Where.DynamicPropertyOf<Owner>("size").Eq(10);
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            var condition = select.SelectWhere().ClauseCondition();
            condition.Should().BeOpExpression("IN_OP");
            condition.ExprOpArg(1).Should().BeSubquery();
        }

        [Fact]
        public void OrderBy_ReferencesJoinedColumn()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("size", DynamicPropertyValueType.Integer, "size");
            query.AddDynamicPropertyToOrderBy<Owner>("size", DynamicPropertyValueType.Integer, SortDir.Desc);
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveSortOrder(1);
            select.Should().HaveSortOrder(0, propsAlias, "v_int", "DESC");
        }

        [Fact]
        public void GroupBy_ReferencesJoinedColumn()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("color", DynamicPropertyValueType.String, "color");
            query.AddDynamicPropertyToGroupBy<Owner>("color", DynamicPropertyValueType.String);
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveGroupBy(1);
            select.Should().HaveGroupBy(0, propsAlias, "v_str");
        }

        [Fact]
        public void OrderBy_NotProjected_Throws()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddToResultset(typeof(Owner));

            Assert.Throws<InvalidOperationException>(() =>
                query.AddDynamicPropertyToOrderBy<Owner>("size", DynamicPropertyValueType.Integer));
        }

        [Fact]
        public void GroupBy_NotProjected_Throws()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddToResultset(typeof(Owner));

            Assert.Throws<InvalidOperationException>(() =>
                query.AddDynamicPropertyToGroupBy<Owner>("color", DynamicPropertyValueType.String));
        }

        [Fact]
        public void OrderBy_ProjectedDifferentType_Throws()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            // projected as String; ordering by the Integer form has no matching join
            query.AddDynamicPropertyToResultset<Owner>("size", DynamicPropertyValueType.String, "s");

            Assert.Throws<InvalidOperationException>(() =>
                query.AddDynamicPropertyToOrderBy<Owner>("size", DynamicPropertyValueType.Integer));
        }

        [Fact]
        public void Having_ReferencesAggregateOfJoinedColumn()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>(AggFn.Sum, "size", DynamicPropertyValueType.Integer, "total");
            query.HavingDynamicPropertyOf<Owner>("size", DynamicPropertyValueType.Integer).Sum().Gt(100);
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();
            string propsAlias = select.Table(1).TableAlias().Value;

            select.Should().HaveHavingClause();
            var condition = select.SelectHaving().ClauseCondition();
            condition.Should().BeOpExpression("GT_OP");
            condition.ExprOpArg(0).Should().BeCallExpression("SUM");
            condition.ExprOpArg(0).ExprFnCallArg(0).Should().BeFieldExpression().And.HaveFieldAlias(propsAlias).And.HaveFieldName("v_int");
            condition.ExprOpArg(1).Should().BeParamExpression();
        }

        [Fact]
        public void Having_NotProjected_Throws()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddToResultset(typeof(Owner));

            Assert.Throws<InvalidOperationException>(() =>
                query.HavingDynamicPropertyOf<Owner>("size", DynamicPropertyValueType.Integer));
        }

        [Fact]
        public void Resultset_SameProperty_DifferentType_JoinedTwice()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesQueryBase<Owner>();
            query.AddDynamicPropertyToResultset<Owner>("p", DynamicPropertyValueType.String, "s");
            query.AddDynamicPropertyToResultset<Owner>("p", DynamicPropertyValueType.Integer, "i");
            query.PrepareQuery();

            var select = query.Builder.Query.ParseSql().SelectStatement();

            // one owner + two distinct side-table joins (different value type => different column)
            select.AllTables().Should().HaveCount(3);
            select.Table(1).Should().HaveTableName(PropsTable).And.BeJoin("JOIN_TYPE_LEFT");
            select.Table(2).Should().HaveTableName(PropsTable).And.BeJoin("JOIN_TYPE_LEFT");
            select.Should().HaveResultsetSize(2);
        }
    }
}
