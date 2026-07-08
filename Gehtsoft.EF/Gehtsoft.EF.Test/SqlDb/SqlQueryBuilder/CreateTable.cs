using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AwesomeAssertions;
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumnCount(1)
                .And.HaveColumn(1, "columnName", expectedType, columnSizeExpected, columnPrecision);
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

            ast.Should().HaveCreateTable("tableName");
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.BePrimaryKey();
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.BePrimaryKey()
                .And.BeAutoincrement();
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.BeNotNull();
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.BeNotNull();
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.BeUnique()
                .And.BeNotNull();
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

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(1, "columnName", "INTEGER")
                .And.HaveDefault("123");

            ast.Should().HaveCreateTable("tableName")
                .And.HaveColumn(2, "columnName", "VARCHAR")
                .And.HaveDefault("'abcdef'");
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

            ast.Should().HaveCreateIndexCount(2);
            ast.Should().HaveCreateIndex("tableName_f1", "tableName", 1).And.HaveIndexColumn(1, "f1");
            ast.Should().HaveCreateIndex("tableName_f3", "tableName", 2).And.HaveIndexColumn(1, "f3");
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

            ast.Should().HaveCreateTable("table")
                .And.HaveColumnCount(2)
                .And.HaveForeignKeyCount(1)
                .And.HaveForeignKey("ref", "dictionaryName", "id");

            var fkColumn = ast.Should().HaveCreateTable("table").And.HaveColumn(2, "ref", "INTEGER");
            if (nullable)
                fkColumn.And.BeNullable();
            else
                fkColumn.And.BeNotNull();

            ast.Should().HaveCreateIndex($"{table.Name}_{table[1].Name}", table.Name)
                .And.HaveIndexColumn(1, table[1].Name);
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

            ast.Should().HaveCreateIndexCount(1);
            ast.Should().HaveCreateIndex("tableName_index1", "tableName")
                .And.HaveIndexColumn(1, "f1", direction);
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

            ast.Should().HaveCreateIndexCount(3);
            ast.Should().HaveCreateIndex("tableName_index1", "tableName", 1).And.HaveIndexColumn(1, "f1");
            ast.Should().HaveCreateIndex("tableName_index2", "tableName", 2).And.HaveIndexColumn(1, "f2");
            ast.Should().HaveCreateIndex("tableName_index3", "tableName", 3).And.HaveIndexColumn(1, "f3");
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

            ast.Should().HaveCreateIndexCount(1);
            ast.Should().HaveCreateIndex("tableName_index1", "tableName")
                .And.HaveIndexColumn(1, "f1")
                .And.HaveIndexColumn(2, "f2")
                .And.HaveIndexColumn(3, "f3");
        }

        [Fact]
        public void CompositeIndex_Function_FunctionsSupported()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = true;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
                return r;
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveCreateIndexCount(1);
            ast.Should().HaveCreateIndex("tableName_index1", "tableName")
                .And.HaveIndexFunctionColumn(1, "UPPER", "f1");
        }

        [Fact]
        public void CompositeIndex_Function_WhenExcluded_Skipped()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
                r[0].ExcludeFor = new[] { connection.ConnectionType };
                return r;
            });

            var builder = connection.GetCreateTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveCreateIndexCount(0);
        }

        [Fact]
        public void CompositeIndex_Function_NotSupported_Fail()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportFunctionsInIndexesSpec = false;

            var table = StageCompositeIndexText(() =>
            {
                var r = new[] { new CompositeIndex("index1") { { SqlFunctionId.Upper, "f1" } } };
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

            ast.Should().HaveCreateView("viewName");

            var viewSelect = ast.ViewSelect();
            viewSelect.Resultset().Should().HaveCount(3);
            viewSelect.ResultsetItem(0).ResultsetExpr().Should().BeFieldExpression().And.HaveFieldName("f1");
            viewSelect.ResultsetItem(1).ResultsetExpr().Should().BeFieldExpression().And.HaveFieldName("f2");
            viewSelect.ResultsetItem(2).ResultsetExpr().Should().BeFieldExpression().And.HaveFieldName("f3");
            viewSelect.Table(0).Should().HaveTableName("tableName");
        }
    }
}
