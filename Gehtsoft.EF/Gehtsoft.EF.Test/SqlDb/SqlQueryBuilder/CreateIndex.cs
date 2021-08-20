using System;
using System.Data;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
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
}
