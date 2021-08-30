using System;
using System.Data;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class CreateIndex
    {
        private static TableDescriptor StageTable()
        {
            var table = new TableDescriptor()
            {
                Name = "tableName"
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f1",
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f2",
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f3",
            });
            return table;
        }

        [Fact]
        public void OneColumn()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();
            var builder = connection.GetCreateIndexBuilder(table, new CompositeIndex("index1") { { "f1" } });
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
        }

        [Fact]
        public void Function_WhenSupported()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = true;
            var table = StageTable();
            var builder = connection.GetCreateIndexBuilder(table, new CompositeIndex("index1") { { SqlFunctionId.Abs, "f1" } });
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/*_CALL/ABS").Should().Exist();
            index.SelectNode("//SORT_SPECIFICATION[1]/*_CALL/*[2]/IDENTIFIER").Should().HaveValue("f1");
        }

        [Fact]
        public void Function_WhenNotSupported_Ignore()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;
            var table = StageTable();
            var builder = connection.GetCreateIndexBuilder(table, new CompositeIndex("index1") { { SqlFunctionId.Abs, "f1" } });
            builder.PrepareQuery();
            builder.Query.Should().BeEmpty();
        }

        [Fact]
        public void Function_WhenNotSupported_Fail()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;
            var table = StageTable();
            var ci = new CompositeIndex("index1") { { SqlFunctionId.Abs, "f1" } };
            ci.FailIfUnsupported = true;
            var builder = connection.GetCreateIndexBuilder(table, ci);
            ((Action)(() => builder.PrepareQuery())).Should().Throw<EfSqlException>();
        }

        [Fact]
        public void Direction()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();
            var builder = connection.GetCreateIndexBuilder(table, new CompositeIndex("index1") { { "f1", SortDir.Desc } });
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
            index.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(1);
        }

        [Fact]
        public void MultiColumn()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();
            var builder = connection.GetCreateIndexBuilder(table, new CompositeIndex("index1") { { "f1" }, { "f2" }, { "f3" } });
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST/SORT_SPECIFICATION").Should().HaveCount(3);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
            index.SelectNode("//SORT_SPECIFICATION[2]/FIELD/IDENTIFIER").Should().HaveValue("f2");
            index.SelectNode("//SORT_SPECIFICATION[3]/FIELD/IDENTIFIER").Should().HaveValue("f3");
        }
    }

    public class Union
    {
        private static TableDescriptor StageTable()
        {
            var table = new TableDescriptor()
            {
                Name = "tableName"
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f1",
                DbType = DbType.Int32
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f2",
                DbType = DbType.Int32
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f3",
                DbType = DbType.Int32
            });
            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f4",
                DbType = DbType.String
            });
            return table;
        }

        [Fact]
        public void UnionAll()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var select2 = connection.GetSelectQueryBuilder(table);
            select2.AddToResultset(table["f2"], "a");

            var union = connection.GetUnionQueryBuilder(select1);
            union.AddQuery(select2, false);

            union.PrepareQuery();
            var ast = union.Query.ParseSql();

            ast.Select("/UNION").Should().HaveCount(1);

            var stmt = ast.SelectNode("/UNION");

            stmt.Select("/SELECT").Should().HaveCount(2);
            stmt.Select("/UNION_OP").Should().HaveCount(1);
            stmt.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(0);

            stmt.SelectNode("/UNION_OP/*").Should().HaveSymbol("UNION_ALL");

            var s = stmt.SelectNode("/SELECT", 1);
            s.ResultsetItem(0)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f1");

            s = stmt.SelectNode("/SELECT", 2);
            s.ResultsetItem(0)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f2");
        }

        [Fact]
        public void UnionDistinctWithOrder()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");
            select1.AddToResultset(table["f2"], "b");

            var select2 = connection.GetSelectQueryBuilder(table);
            select2.AddToResultset(table["f2"], "a");
            select2.AddToResultset(table["f3"], "b");

            var union = connection.GetUnionQueryBuilder(select1);
            union.AddQuery(select2, true);
            union.AddOrderBy(union.QueryTableDescriptor["b"], SortDir.Desc);

            union.PrepareQuery();
            var ast = union.Query.ParseSql();

            ast.Select("/UNION").Should().HaveCount(1);

            var stmt = ast.SelectNode("/UNION");

            stmt.Select("/SELECT").Should().HaveCount(2);
            stmt.Select("/UNION_OP").Should().HaveCount(1);
            stmt.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);

            stmt.SelectNode("/UNION_OP/*").Should().HaveSymbol("UNION_DISTINCT");

            var s = stmt.SelectNode("/SELECT", 1);
            s.ResultsetItem(0)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f1");
            s.ResultsetItem(1)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f2");

            s = stmt.SelectNode("/SELECT", 2);
            s.ResultsetItem(0)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f2");
            s.ResultsetItem(1)
                .ResultsetExpr().Should().BeFieldExpression()
                .And.HaveFieldName("f3");

            s = stmt.SelectSort();
            s.SortOrder(0)
                .SortOrderExpr()
                .Should().BeFieldExpression()
                .And.HaveFieldName("b");

            s.SortOrder(0)
                .SortOrderDirection()
                .Should().Be("DESC");
        }

        [Fact]
        public void Error_NotMatching_Count()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var select2 = connection.GetSelectQueryBuilder(table);
            select2.AddToResultset(table["f2"], "a");
            select2.AddToResultset(table["f2"], "b");

            var union = connection.GetUnionQueryBuilder(select1);
            ((Action)(() => union.AddQuery(select2, false))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Error_NotMatching_Name()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var select2 = connection.GetSelectQueryBuilder(table);
            select2.AddToResultset(table["f2"], "b");

            var union = connection.GetUnionQueryBuilder(select1);
            ((Action)(() => union.AddQuery(select2, false))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Error_NotMatching_Type()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var select2 = connection.GetSelectQueryBuilder(table);
            select2.AddToResultset(table["f4"], "a");

            var union = connection.GetUnionQueryBuilder(select1);
            ((Action)(() => union.AddQuery(select2, false))).Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Error_Column_Not_Belong()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var union = connection.GetUnionQueryBuilder(select1);
            ((Action)(() => union.AddOrderBy(table["f1"]))).Should().Throw<ArgumentException>();

            ((Action)(() => union.PrepareQuery())).Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Error_One_Table()
        {
            using var connection = new DummySqlConnection();
            var table = StageTable();

            var select1 = connection.GetSelectQueryBuilder(table);
            select1.AddToResultset(table["f1"], "a");

            var union = connection.GetUnionQueryBuilder(select1);
            ((Action)(() => union.PrepareQuery())).Should().Throw<InvalidOperationException>();
        }
    }
}
