using System.Data;
using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Entity.Utils;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class AlterTable
    {
        [Fact]
        public void DropField()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "testTable"
            };
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
            });
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col2",
                DbType = DbType.String,
            });
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col3",
                DbType = DbType.String,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, null, new[] { td[1] });

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();
            ast.Should().HaveAlterTable("testTable").And.HaveDropColumn("col2");
        }

        [Fact]
        public void DropMultiFields()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "testTable"
            };
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
            });
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col2",
                DbType = DbType.String,
            });
            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col3",
                DbType = DbType.String,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, null, new[] { td[0], td[2] });

            var queries = builder.GetQueries();
            queries.Should().HaveCount(2);

            var ast = queries[0].ParseSql();
            ast.Should().HaveAlterTable("testTable").And.HaveDropColumn("col1");

            ast = queries[1].ParseSql();
            ast.Should().HaveAlterTable("testTable").And.HaveDropColumn("col3");
        }

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

            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "columnName",
                Size = columnSize ?? 0,
                Precision = columnPrecision ?? 0,
                DbType = columnType,
                Table = td,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumnCount(1)
                .And.HaveColumn(1, "columnName", expectedType, columnSizeExpected, columnPrecision);
        }

        [Fact]
        public void Autoincrement()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Autoincrement = true,
                Table = td,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.BeNotNull()
                .And.BeAutoincrement();
        }

        [Fact]
        public void NotNull()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Table = td,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.BeNotNull();
        }

        [Fact]
        public void Nullable()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Table = td,
                Nullable = true,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.BeNullable();
        }

        [Fact]
        public void Unique()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Table = td,
                Unique = true,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.BeUnique()
                .And.BeNotNull();
        }

        [Fact]
        public void DefaultValue()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Table = td,
                DefaultValue = 0,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(1);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.HaveDefault("0");
        }

        [Fact]
        public void Sorted()
        {
            TableDescriptor td = new TableDescriptor()
            {
                Name = "tableName",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "col1",
                DbType = DbType.Int32,
                Table = td,
                Nullable = true,
                Sorted = true,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[0] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(2);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("tableName")
                .And.HaveColumn(1, "col1", "INTEGER")
                .And.BeNullable();

            ast = queries[1].ParseSql();

            ast.Should().HaveCreateIndex("tableName_col1", "tableName")
                .And.HaveIndexColumn(1, "col1");
        }

        [Fact]
        public void ForeignKey()
        {
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

            TableDescriptor td = new TableDescriptor()
            {
                Name = "table",
            };

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "id",
                Size = 0,
                Precision = 0,
                DbType = DbType.Int32,
                PrimaryKey = true,
                Table = td,
            });

            td.Add(new TableDescriptor.ColumnInfo()
            {
                Name = "ref",
                DbType = DbType.Int32,
                Size = 0,
                Precision = 0,
                ForeignTable = dictionary,
                Nullable = true,
                Table = td,
            });

            using var connection = new DummySqlConnection();
            var builder = new AlterTableQueryBuilder(connection.GetLanguageSpecifics());
            builder.SetTable(td, new[] { td[1] }, null);

            var queries = builder.GetQueries();
            queries.Should().HaveCount(3);

            var ast = queries[0].ParseSql();

            ast.Should().HaveAlterTable("table")
                .And.HaveColumnCount(1)
                .And.HaveColumn(1, "ref", "INTEGER")
                .And.BeNullable();

            ast = queries[1].ParseSql();
            ast.Should().HaveAlterTable("table")
                .And.HaveForeignKeyCount(1)
                .And.HaveForeignKey("ref", "dictionaryName", "id");

            ast = queries[2].ParseSql();
            ast.Should().HaveCreateIndex($"{td.Name}_{td[1].Name}", td.Name)
                .And.HaveIndexColumn(1, td[1].Name);
        }
    }
}
