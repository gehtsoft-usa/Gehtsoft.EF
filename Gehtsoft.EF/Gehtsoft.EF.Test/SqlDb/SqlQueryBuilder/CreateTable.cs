using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Moq;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class CreateTable
    {
        [Theory]
        [InlineData(DbType.Int32, "INTEGER", null, null, null)]
        [InlineData(DbType.Int64, "NUMERIC", null, 19, null)]
        [InlineData(DbType.Double, "NUMERIC", 12, 12, 5)]
        [InlineData(DbType.Decimal, "NUMERIC", 18, 18, 2)]
        [InlineData(DbType.Boolean, "VARCHAR", null, 1, null)]
        [InlineData(DbType.Binary, "BLOB", null, null, null)]
        [InlineData(DbType.Binary, "BLOB", 12, 12, null)]
        [InlineData(DbType.String, "VARCHAR", null, null, null)]
        [InlineData(DbType.Date, "DATE", null, null, null)]
        [InlineData(DbType.DateTime, "TIMESTAMP", null, null, null)]
        [InlineData(DbType.Guid, "VARCHAR", null, 40, null)]

        public void ColumnType(DbType columnType, string expectedType, int? columnSize, int? columnSizeExpected, int? columnPrecision)
        {
            (columnPrecision == null || columnSize != null).Should().BeTrue(because: "If precision is set, the size must be set too!");

            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = columnSize ?? 0,
                Precision = columnPrecision ?? 0,
                DbType = columnType,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            var tableNode = ast.SelectNode("/CREATE_TABLE[1]");

            tableNode.Select("//*_CLAUSE").Should().HaveCount(1);
            tableNode.Select("//FIELD_DEFINITION").Should().HaveCount(1, "the query must have only one field definition");
            tableNode.SelectNode("//*_DEFINITION[1]/*_NAME/*[1]").Should().HaveValue("columnName");
            tableNode.SelectNode("//*_DEFINITION[1]/*_TYPE/*[1]").Should().HaveValue(expectedType);

            if (columnSizeExpected != null)
                tableNode.SelectNode("//*_DEFINITION[1]/*_TYPE/*_SIZE/INT[1]")
                    .Should().Exist()
                    .And.HaveValue(columnSizeExpected.ToString());

            if (columnPrecision != null)
                tableNode.SelectNode("//*_DEFINITION[1]/*_TYPE/*_SIZE/INT[2]")
                    .Should().Exist()
                    .And.HaveValue(columnPrecision.ToString());
        }

        [Fact]
        public void TableName()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("CREATE_TABLE").Should().HaveCount(1);

            ast.SelectNode("/CREATE_TABLE[1]/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName");
        }

        [Fact]
        public void PrimaryKey()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_PRIMARY_KEY").Any());

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG").Should().HaveCount(1, "no more flags must be specified with primary key");
        }

        [Fact]
        public void Autoincrement()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Autoincrement = true,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_PRIMARY_KEY").Any());

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_AUTOINCREMENT").Any());
        }

        [Fact]
        public void NotNull()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Nullable = false,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_NOT_NULL").Any());
        }

        [Fact]
        public void Nullable()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Nullable = false,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveNoElementMatching(node => !node.Select("/*_NOT_NULL").Any());
        }

        [Fact]
        public void Unique()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Unique = true,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_UNIQUE").Any());

            ast.Select("/CREATE_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should().HaveElementMatching(node => node.Select("/*_NOT_NULL").Any());
        }

        [Fact]
        public void DefaultValue()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Table = table,
                DefaultValue = 123
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = 0,
                Precision = 0,
                DbType = DbType.String,
                Table = table,
                DefaultValue = "abcdef"
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//*_CLAUSE[1]//*_FLAG/*_DEFAULT").Should().HaveCount(1);
            ast.SelectNode("/CREATE_TABLE[1]//*_CLAUSE[1]//*_FLAG/*_DEFAULT/*[1]")
                .Should().HaveValue("123");

            ast.Select("/CREATE_TABLE[1]//*_CLAUSE[2]//*_FLAG/*_DEFAULT").Should().HaveCount(1);
            ast.SelectNode("/CREATE_TABLE[1]//*_CLAUSE[2]//*_FLAG/*_DEFAULT/*[1]")
                .Should().HaveValue("'abcdef'");
        }

        [Fact]
        public void Sorted()
        {
            using var connection = new DummySqlConnection();

            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f1",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Sorted = true,
                Table = table,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f2",
                Size = 32,
                Precision = 0,
                DbType = DbType.String,
                Sorted = false,
                Table = table,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f3",
                Size = 32,
                Precision = 0,
                DbType = DbType.String,
                Sorted = true,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);

            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(2);

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_f1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");

            index = ast.SelectNode("/CREATE_INDEX[2]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_f3");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f3");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ForeignKey(bool nullable)
        {
            using var connection = new DummySqlConnection();

            TableDescriptor dictionary = new TableDescriptor()
            {
                Name = "dictionaryName",
            };

            dictionary.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "id",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Table = dictionary,
            });

            TableDescriptor table = new TableDescriptor()
            {
                Name = "table",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "id",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Table = table,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "ref",
                DbType = DbType.Int32,
                Size = 0,
                Precision = 0,
                ForeignTable = dictionary,
                Nullable = nullable,
                Table = table,
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_TABLE[1]//FIELD_DEFINITION").Should().HaveCount(2);
            ast.Select("/CREATE_TABLE[1]//FOREIGN_KEY_DEFINITION").Should().HaveCount(1);

            var fkField = ast.SelectNode("/CREATE_TABLE[1]//FIELD_DEFINITION", 2);
            fkField.Select("//*_FLAG/*_NOT_NULL").Should().HaveCount(nullable ? 0 : 1);

            var fkDefinition = ast.SelectNode("/CREATE_TABLE[1]//FOREIGN_KEY_DEFINITION");

            fkDefinition.SelectNode("/FIELD_*_NAME[1]/IDENTIFIER").Should().HaveValue("ref");
            fkDefinition.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue("dictionaryName");
            fkDefinition.SelectNode("/FIELD_*_NAME[2]/IDENTIFIER").Should().HaveValue("id");

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue($"{table.Name}_{table[1].Name}");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue(table.Name);
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue(table[1].Name);
        }

        private static TableDescriptor StageCompositeIndexText(Func<IEnumerable<CompositeIndex>> indexes)
        {
            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f1",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                Table = table,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f2",
                Size = 32,
                Precision = 0,
                DbType = DbType.String,
                Table = table,
            });

            table.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "f3",
                Size = 32,
                Precision = 0,
                DbType = DbType.String,
                Table = table,
            });

            var metadata = new Mock<ICompositeIndexMetadata>();
            metadata.Setup(m => m.Indexes).Returns(indexes);
            table.Metadata = metadata.Object;

            return table;
        }

        [Theory]
        [InlineData(SortDir.Asc)]
        [InlineData(SortDir.Desc)]
        public void CompositeIndex_SimpleField(SortDir direction)
        {
            using var connection = new DummySqlConnection();

            var table = StageCompositeIndexText(() => new[] { new CompositeIndex("index1") { { "f1", direction } } });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);
            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            var indexNode = ast.SelectNode("/CREATE_INDEX[1]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST/SORT_SPECIFICATION").Should().HaveCount(1);
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
            indexNode.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(direction == SortDir.Asc ? 0 : 1);
        }

        [Fact]
        public void CompositeIndex_SimpleField_MultipleIndexes()
        {
            using var connection = new DummySqlConnection();

            var table = StageCompositeIndexText(() => new[] {
                new CompositeIndex("index1") { { "f1" } },
                new CompositeIndex("index2") { { "f2" } },
                new CompositeIndex("index3") { { "f3" } }
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(3);
            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            var indexNode = ast.SelectNode("/CREATE_INDEX[1]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
            indexNode.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(0);

            indexNode = ast.SelectNode("/CREATE_INDEX[2]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index2");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f2");
            indexNode.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(0);

            indexNode = ast.SelectNode("/CREATE_INDEX[3]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index3");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f3");
            indexNode.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(0);
        }

        [Fact]
        public void CompositeIndex_SimpleList()
        {
            using var connection = new DummySqlConnection();

            var table = StageCompositeIndexText(() => new[] {
                new CompositeIndex("index1") { { "f1" }, { "f2" }, { "f3" } }  });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);
            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            var indexNode = ast.SelectNode("/CREATE_INDEX[1]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST/SORT_SPECIFICATION").Should().HaveCount(3);
            indexNode.SelectNode("/SORT_SPECIFICATION_LIST//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("f1");
            indexNode.SelectNode("/SORT_SPECIFICATION_LIST//SORT_SPECIFICATION[2]/FIELD/IDENTIFIER").Should().HaveValue("f2");
            indexNode.SelectNode("/SORT_SPECIFICATION_LIST//SORT_SPECIFICATION[3]/FIELD/IDENTIFIER").Should().HaveValue("f3");
        }

        [Fact]
        public void CompositeIndex_Function_FunctionsSupported()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = true;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
                r[0].FailIfUnsupported = true;
                return r;
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(1);
            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            var indexNode = ast.SelectNode("/CREATE_INDEX[1]");
            indexNode.Should().Exist();

            indexNode.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_index1");
            indexNode.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            indexNode.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/*_CALL/UPPER").Should().Exist();
            indexNode.SelectNode("//SORT_SPECIFICATION[1]/*_CALL/*[2]/IDENTIFIER").Should().HaveValue("f1");
            indexNode.Select("//SORT_SPECIFICATION[1]/DESC").Should().HaveCount(0);
        }

        [Fact]
        public void CompositeIndex_Function_NotSupported_Ignore()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
                r[0].FailIfUnsupported = false;
                return r;
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("/CREATE_INDEX").Should().HaveCount(0);
        }

        [Fact]
        public void CompositeIndex_Function_NotSupported_Fail()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
                r[0].FailIfUnsupported = true;
                return r;
            });

            var builder = connection.GetCreateTableBuilder(table);
            ((Action)(() => builder.PrepareQuery())).Should().Throw<EfSqlException>();
        }

        [Fact]
        public void View()
        {
            using var connection = new DummySqlConnection();
            var table = StageCompositeIndexText(() => Array.Empty<CompositeIndex>());

            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["f1"]);
            select.AddToResultset(table["f2"]);
            select.AddToResultset(table["f3"]);

            var view = connection.GetCreateViewBuilder("viewName", select);

            view.PrepareQuery();
            var ast = view.Query.ParseSql();
            ast.Select("/CREATE_VIEW").Should().HaveCount(1);
            ast.SelectNode("/CREATE_VIEW[1]/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("viewName");
            ast.Select("//SELECT").Should().HaveCount(1);

            ast.Select("//SELECT//SELECT_SUBLIST/*").Should().HaveCount(3);
            ast.SelectNode("//SELECT//SELECT_SUBLIST/*", 1)
                .Should().HaveSymbol("EXPR_ALIAS")
                .And.Match(n => n.SelectNode("/FIELD/IDENTIFIER(f1)", 1) != null);

            ast.SelectNode("//SELECT//SELECT_SUBLIST/*", 2)
                .Should().HaveSymbol("EXPR_ALIAS")
                .And.Match(n => n.SelectNode("/FIELD/IDENTIFIER(f2)", 1) != null);

            ast.SelectNode("//SELECT//SELECT_SUBLIST/*", 3)
                .Should().HaveSymbol("EXPR_ALIAS")
                .And.Match(n => n.SelectNode("/FIELD/IDENTIFIER(f3)", 1) != null);

            ast.SelectNode("//SELECT/TABLE_EXPRESSION/FROM_CLAUSE//TABLE_PRIMARY[1]")
                .Should().Exist()
                .And.Match(n => n.SelectNode("/TABLE_NAME/IDENTIFIER(tableName)", 1) != null);
        }
    }
}
