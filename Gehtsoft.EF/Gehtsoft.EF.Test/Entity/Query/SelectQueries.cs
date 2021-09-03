using System;
using System.Data;
using System.Threading.Tasks;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Entity.Query
{
    public class SelectQueries
    {
        #region entities
        [Entity(Scope = "select_queries", Table = "dict1")]
        public class Dict1
        {
            [AutoId]
            public int Id { get; set; }
            [EntityProperty(Field = "n1")]
            public int N1 { get; set; }
        }

        [Entity(Scope = "select_queries", Table = "dict2")]
        public class Dict2
        {
            [AutoId]
            public int Id { get; set; }
            [EntityProperty(Field = "n2")]
            public int N2 { get; set; }
            [ForeignKey(Field = "d1")]
            public Dict1 D1 { get; set; }
        }

        [Entity(Scope = "select_queries", Table = "table1")]
        public class Entity1
        {
            [AutoId]
            public int Id { get; set; }
            [EntityProperty(Field = "a")]
            public int A { get; set; }
            [EntityProperty(Field = "b")]
            public int B { get; set; }
            [ForeignKey(Field = "d1")]
            public Dict1 D1 { get; set; }
            [ForeignKey(Field = "d2")]
            public Dict2 D2 { get; set; }
        }

        [Entity(Scope = "select_queries", Table = "table2")]
        public class Entity2
        {
            [AutoId]
            public int Id { get; set; }

            [ForeignKey(Field = "d1", Nullable = true)]
            public Dict1 D1 { get; set; }
        }
        #endregion

        [Fact]
        public void Join_Auto_OneTable()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict1>();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Fact]
        public void Join_Auto_TwoTables_Not_Nullable()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict2>();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict2")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");
        }

        [Fact]
        public void Join_Auto_TwoTables_Nullable()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity2>();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("table2")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_LEFT");
        }

        [Fact]
        public void Join_None_Unless_Requested_TwoTables()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict2>();
            query.AddToResultset("N2");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0)
                .Should().HaveTableName("dict2")
                .And.NotBeJoin();
        }

        [Fact]
        public void Join_Auto_Of_Requested_TwoTables_Many_To_One()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict2>();
            query.AddEntity<Dict1>();
            query.AddToResultset("N2");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict2")
                .And.NotBeJoin();
            select.Table(1)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");

            var join = select.Table(1).TableJoinCondition();

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("d1");
        }

        [Fact]
        public void Join_Auto_Of_Requested_TwoTables_One_To_Many_Inner()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict1>();
            query.AddEntity<Entity1>("Id");
            query.AddToResultset("N1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("table1")
                .And.BeJoin("JOIN_TYPE_INNER");

            var join = select.Table(1).TableJoinCondition();

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("d1");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("id");
        }

        [Fact]
        public void Join_Auto_Of_Requested_TwoTables_One_To_Many_Outer_Forced()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict1>();
            query.AddEntity<Entity1>("Id", true);
            query.AddToResultset("N1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("table1")
                .And.BeJoin("JOIN_TYPE_LEFT");

            var join = select.Table(1).TableJoinCondition();

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("d1");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("id");
        }

        [Fact]
        public void Join_Manually()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict1>();
            query.AddEntity(typeof(Entity1), TableJoinType.Left, typeof(Entity1), "D1", CmpOp.Eq, typeof(Dict1), "Id");
            query.AddToResultset("N1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("table1")
                .And.BeJoin("JOIN_TYPE_LEFT");

            var join = select.Table(1).TableJoinCondition();

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("d1");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("id");
        }

        [Fact]
        public void Join_Auto_Of_Requested_TwoTables_One_To_Many_Outer_Auto()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Dict1>();
            query.AddEntity<Entity2>("Id", false);
            query.AddToResultset("N1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(2);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("table2")
                .And.BeJoin("JOIN_TYPE_RIGHT");

            var join = select.Table(1).TableJoinCondition();

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("d1");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("id");
        }

        [Fact]
        public void Join_Auto_WholeTree_Auto()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(4);

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;
            var alias3 = select.Table(2).TableAlias().Value;
            var alias4 = select.Table(3).TableAlias().Value;

            select.Table(0)
                .Should().HaveTableName("table1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");

            var join = select.Table(1).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("d1");

            select.Table(2)
                .Should().HaveTableName("dict2")
                .And.BeJoin("JOIN_TYPE_INNER");

            join = select.Table(2).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("d2");

            select.Table(3)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");

            join = select.Table(3).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias4)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("d1");
        }

        [Fact]
        public void Join_Auto_WholeTree_ByDemand()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddWholeTree();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.AllTables()
                .Should().HaveCount(4);

            var alias1 = select.Table(0).TableAlias().Value;
            var alias2 = select.Table(1).TableAlias().Value;
            var alias3 = select.Table(2).TableAlias().Value;
            var alias4 = select.Table(3).TableAlias().Value;

            select.Table(0)
                .Should().HaveTableName("table1")
                .And.NotBeJoin();

            select.Table(1)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");

            var join = select.Table(1).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias2)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("d1");

            select.Table(2)
                .Should().HaveTableName("dict2")
                .And.BeJoin("JOIN_TYPE_INNER");

            join = select.Table(2).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias1)
                .And.HaveFieldName("d2");

            select.Table(3)
                .Should().HaveTableName("dict1")
                .And.BeJoin("JOIN_TYPE_INNER");

            join = select.Table(3).TableJoinCondition();

            join.Should().BeOpExpression("EQ_OP");

            join.ExprOpArg(0)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias4)
                .And.HaveFieldName("id");

            join.ExprOpArg(1)
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(alias3)
                .And.HaveFieldName("d1");
        }

        [Theory]

        [InlineData("A", 0, "a")]
        [InlineData("D1.N1", 1, "n1")]
        [InlineData("D2.N2", 2, "n2")]
        [InlineData("D2.D1.N1", 3, "n1")]
        public void Where_Property_ByPath(string path, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.Where.Property(path).Eq().Parameter("p1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveWhereClause();

            var where = select.SelectWhere().ClauseCondition();

            where.Should().BeBinaryOp("EQ_OP");

            var a1 = where.ExprOpArg(0);

            a1.Should()
                .BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Fact]
        public void Where_Property_ByName()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.Where.PropertyOf<Dict1>("N1", 1).Eq().Parameter("p1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveWhereClause();

            var where = select.SelectWhere().ClauseCondition();

            where.Should().BeBinaryOp("EQ_OP");

            var a1 = where.ExprOpArg(0);

            a1.Should()
                .BeFieldExpression()
                .And.HaveFieldAlias(select.Table(3).TableAlias().Value)
                .And.HaveFieldName("n1");
        }

        [Fact]
        public void Count()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict1>();
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);
            select.Should().HaveNoWhereClause();
            select.Should().HaveNoGroupBy();
            select.Should().HaveNoSortOrder();

            select.ResultsetItem(0).ResultsetExpr()
                .Should().BeCountAllCall();

            select.AllTables()
                .Should().HaveCount(1);

            select.Table(0)
                .Should().HaveTableName("dict1")
                .And.NotBeJoin();
        }

        [Fact]
        public void Count_Execute_Explicit()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict1>();
            query.PrepareQuery();
            
            var command = query.Query.Command as DummyDbCommand;
            var result = new DummyDbDataReaderResult();
            result.Columns = new DummyDbDataReaderColumnCollection() { new DummyDbDataReaderColumn("", DbType.Int32) };
            result.Data = new DummyDbDataReaderColumnDataRows() { new DummyDbDataReaderColumnDataCollection(15) };
            command.ReturnReader = new DummyDbDataReader() { result };

            query.Execute();
            query.RowCount.Should().Be(15);
        }

        [Fact]
        public void Count_Execute_Implicit()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict1>();
            query.PrepareQuery();

            var command = query.Query.Command as DummyDbCommand;
            var result = new DummyDbDataReaderResult();
            result.Columns = new DummyDbDataReaderColumnCollection() { new DummyDbDataReaderColumn("", DbType.Int32) };
            result.Data = new DummyDbDataReaderColumnDataRows() { new DummyDbDataReaderColumnDataCollection(15) };
            command.ReturnReader = new DummyDbDataReader() { result };

            query.RowCount.Should().Be(15);
            query.RowCount.Should().Be(15);
        }

        [Fact]
        public async Task Count_Execute_Async()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Dict1>();
            query.PrepareQuery();

            var command = query.Query.Command as DummyDbCommand;
            var result = new DummyDbDataReaderResult();
            result.Columns = new DummyDbDataReaderColumnCollection() { new DummyDbDataReaderColumn("", DbType.Int32) };
            result.Data = new DummyDbDataReaderColumnDataRows() { new DummyDbDataReaderColumnDataCollection(15) };
            command.ReturnReader = new DummyDbDataReader() { result };

            await query.ExecuteAsync();
            query.RowCount.Should().Be(15);
        }

        [Fact]
        public void Having_Property_ByName()
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.Having.PropertyOf<Dict1>("N1", 1).Eq().Parameter("p1");
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveHavingClause();

            var where = select.SelectHaving().ClauseCondition();

            where.Should().BeBinaryOp("EQ_OP");

            var a1 = where.ExprOpArg(0);

            a1.Should()
                .BeFieldExpression()
                .And.HaveFieldAlias(select.Table(3).TableAlias().Value)
                .And.HaveFieldName("n1");
        }

        [Theory]
        [InlineData("A", 0, "a")]
        [InlineData("D1", 0, "d1")]
        [InlineData("D1.N1", 1, "n1")]
        [InlineData("D2.N2", 2, "n2")]
        [InlineData("D2.D1.N1", 3, "n1")]
        public void OrderBy_Property_ByPath(string path, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.AddOrderBy(path);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveSortOrder(1);

            select.SelectSort().SortOrder(0)
                .SortOrderExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData("A", typeof(Entity1), 0, 0, "a")]
        [InlineData("D1", typeof(Entity1), 0, 0, "d1")]
        [InlineData("N1", typeof(Dict1), 0, 1, "n1")]
        [InlineData("N2", typeof(Dict2), 0, 2, "n2")]
        [InlineData("N1", typeof(Dict1), 1, 3, "n1")]
        public void OrderBy_Property_ByType(string property, Type type, int typeOccurrence, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            if (typeOccurrence == 0)
                query.AddOrderBy(type, property);
            else
                query.AddOrderBy(type, typeOccurrence, property);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveSortOrder(1);

            select.SelectSort().SortOrder(0)
                .SortOrderExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData("A", 0, "a")]
        [InlineData("D1", 0, "d1")]
        [InlineData("D1.N1", 1, "n1")]
        [InlineData("D2.N2", 2, "n2")]
        [InlineData("D2.D1.N1", 3, "n1")]
        public void GroupBy_Property_ByPath(string path, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            query.AddGroupBy(path);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveGroupBy(1);

            select.SelectGroupBy().GroupOrder(0)
                .SortOrderExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData("A", typeof(Entity1), 0, 0, "a")]
        [InlineData("N1", typeof(Dict1), 0, 1, "n1")]
        [InlineData("N2", typeof(Dict2), 0, 2, "n2")]
        [InlineData("N1", typeof(Dict1), 1, 3, "n1")]
        public void GroupBy_Property_ByType(string property, Type type, int typeOccurrence, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetSelectEntitiesCountQuery<Entity1>();
            if (typeOccurrence == 0)
                query.AddGroupBy(type, property);
            else
                query.AddGroupBy(type, typeOccurrence, property);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveGroupBy(1);

            select.SelectGroupBy().GroupOrder(0)
                .SortOrderExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData("A", 0, "a")]
        [InlineData("D1.N1", 1, "n1")]
        [InlineData("D2.N2", 2, "n2")]
        [InlineData("D2.D1.N1", 3, "n1")]
        public void Resultset_Property_ByPath(string path, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddWholeTree();
            query.AddToResultset(path);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);

            select.ResultsetItem(0)
                .ResultsetExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData("A", typeof(Entity1), 0, 0, "a")]
        [InlineData("N1", typeof(Dict1), 0, 1, "n1")]
        [InlineData("N2", typeof(Dict2), 0, 2, "n2")]
        [InlineData("N1", typeof(Dict1), 1, 3, "n1")]
        public void Resultset_Property_ByType(string property, Type type, int typeOccurrence, int table, string name)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddWholeTree();
            
            if (typeOccurrence == 0)
                query.AddToResultset(type, property);
            else
                query.AddToResultset(type, typeOccurrence, property);
            
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);

            select.ResultsetItem(0)
                .ResultsetExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldAlias(select.Table(table).TableAlias().Value)
                .And.HaveFieldName(name);
        }

        [Theory]
        [InlineData(AggFn.Min, "MIN", "A", "a")]
        [InlineData(AggFn.Max, "MAX", "A", "a")]
        [InlineData(AggFn.Sum, "SUM", "A", "a")]
        [InlineData(AggFn.Avg, "AVG", "A", "a")]
        [InlineData(AggFn.Count, null, null, null)]
        public void Resultset_AggregateFunction_Path(AggFn aggFn, string expectedFunction, string property, string expectedField)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddToResultset(aggFn, property);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);

            var expr = select.ResultsetItem(0)
                .ResultsetExpr();

            if (expectedFunction != null)
            {
                expr.Should().BeCallExpression(expectedFunction);
                expr.ExprFnCallArgCount().Should().Be(1);
                expr.ExprFnCallArg(0)
                    .Should().BeFieldExpression()
                    .And.HaveFieldName(expectedField);
            }
            else
                expr.Should().BeCountAllCall();
        }

        [Theory]
        [InlineData(AggFn.Min, "MIN", "A", "a")]
        [InlineData(AggFn.Max, "MAX", "A", "a")]
        [InlineData(AggFn.Sum, "SUM", "A", "a")]
        [InlineData(AggFn.Avg, "AVG", "A", "a")]
        public void Resultset_AggregateFunction_Property(AggFn aggFn, string expectedFunction, string property, string expectedField)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddToResultset(aggFn, typeof(Entity1), property);
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);

            var expr = select.ResultsetItem(0)
                .ResultsetExpr();

            if (expectedFunction != null)
            {
                expr.Should().BeCallExpression(expectedFunction);
                expr.ExprFnCallArgCount().Should().Be(1);
                expr.ExprFnCallArg(0)
                    .Should().BeFieldExpression()
                    .And.HaveFieldName(expectedField);
            }
            else
                expr.Should().BeCountAllCall();
        }

        [Theory]
        [InlineData("MIN", "A", typeof(Entity1), 0, "a")]
        [InlineData("MAX", "N1", typeof(Dict1), 0, "n1")]
        [InlineData("MAX", "N1", typeof(Dict1), 1, "n1")]
        [InlineData("SUM", "Id", typeof(Dict2), 0, "id")]
        [InlineData("AVG", "A", typeof(Entity1), 0, "a")]
        public void Resultset_AggregateFunction_Expression(string expectedFunction, string property, Type propertyType, int propertyOccurrence, string expectedField)
        {
            using var connection = new DummySqlConnection();
            using var query = connection.GetGenericSelectEntityQuery<Entity1>();
            query.AddWholeTree();
            var e = query.FindType(propertyType, propertyOccurrence);
            
            query.AddExpressionToResultset($"{expectedFunction}({e.Alias}.{e.Table[property].Name})", DbType.Double, "my");
            
            query.PrepareQuery();
            var select = query.Builder.Query.ParseSql().SelectStatement();

            select.Should().HaveResultsetSize(1);

            var expr = select.ResultsetItem(0)
                .ResultsetExpr();

            if (expectedFunction != null)
            {
                expr.Should().BeCallExpression(expectedFunction);
                expr.ExprFnCallArgCount().Should().Be(1);
                expr.ExprFnCallArg(0)
                    .Should().BeFieldExpression()
                    .And.HaveFieldAlias(e.Alias)
                    .And.HaveFieldName(expectedField);
            }
            else
                expr.Should().BeCountAllCall();
        }

    }
}

