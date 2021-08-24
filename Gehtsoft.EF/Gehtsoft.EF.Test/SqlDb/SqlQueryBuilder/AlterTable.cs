using System.Data;
using System.Linq;
using FluentAssertions;
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
            ast.Select("/ALTER_TABLE").Should().HaveCount(1);
            var stmt = ast.SelectNode("/ALTER_TABLE");

            stmt.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue("testTable");
            stmt.Select("/*_CLAUSE").Should().HaveCount(1);
            stmt.SelectNode("/*_CLAUSE/DROP_FIELD_CLAUSE/FIELD_DEFINITION_NAME")
                .Should().Exist()
                .And.Subject.SelectNode("/IDENTIFIER").Should().HaveValue("col2");
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
            var stmt = ast.SelectNode("/ALTER_TABLE");

            stmt.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue("testTable");
            stmt.SelectNode("/*_CLAUSE/DROP_FIELD_CLAUSE/FIELD_DEFINITION_NAME")
                .Should().Exist()
                .And.Subject.SelectNode("/IDENTIFIER").Should().HaveValue("col1");

            ast = queries[1].ParseSql();
            stmt = ast.SelectNode("/ALTER_TABLE");

            stmt.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue("testTable");
            stmt.SelectNode("/*_CLAUSE/DROP_FIELD_CLAUSE/FIELD_DEFINITION_NAME")
                .Should().Exist()
                .And.Subject.SelectNode("/IDENTIFIER").Should().HaveValue("col3");
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

            var tableNode = ast.SelectNode("/ALTER_TABLE[1]");
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

            ast.Select("/ALTER_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should()
                .HaveCount(2)
                .And.HaveElementMatching(node => node.Select("/*_NOT_NULL").Any())
                .And.HaveElementMatching(node => node.Select("/*_AUTOINCREMENT").Any());
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

            ast.Select("/ALTER_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should()
                .HaveCount(1)
                .And.HaveElementMatching(node => node.Select("/*_NOT_NULL").Any());
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

            ast.Select("/ALTER_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should()
                .HaveCount(0);
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

            ast.Select("/ALTER_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should()
                .HaveCount(2)
                .And.HaveElementMatching(node => node.Select("/*_NOT_NULL").Any())
                .And.HaveElementMatching(node => node.Select("/*_UNIQUE").Any());
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

            ast.Select("/ALTER_TABLE[1]//*_FLAG/*_DEFAULT").Should().HaveCount(1);
            ast.SelectNode("/ALTER_TABLE[1]//*_CLAUSE[1]//*_FLAG/*_DEFAULT/*[1]")
                .Should().HaveValue("0");
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

            ast.Select("/ALTER_TABLE[1]//*_DEFINITION[1]//*_FLAG")
                .Should()
                .HaveCount(0);

            ast = queries[1].ParseSql();

            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue("tableName_col1");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue("tableName");
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue("col1");
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

            ast.Select("/ALTER_TABLE[1]//FIELD_DEFINITION").Should().HaveCount(1);
            var fkField = ast.SelectNode("/ALTER_TABLE[1]//FIELD_DEFINITION");
            fkField.Select("//*_FLAG/*_NOT_NULL").Should().HaveCount(0);

            ast = queries[1].ParseSql();
            ast.Select("/ALTER_TABLE[1]//FOREIGN_KEY_DEFINITION").Should().HaveCount(1);
            var fkDefinition = ast.SelectNode("/ALTER_TABLE[1]//FOREIGN_KEY_DEFINITION");

            fkDefinition.SelectNode("/FIELD_*_NAME[1]/IDENTIFIER").Should().HaveValue("ref");
            fkDefinition.SelectNode("/TABLE_NAME/IDENTIFIER").Should().HaveValue("dictionaryName");
            fkDefinition.SelectNode("/FIELD_*_NAME[2]/IDENTIFIER").Should().HaveValue("id");

            ast = queries[2].ParseSql();
            var index = ast.SelectNode("/CREATE_INDEX[1]");
            index.Should().Exist();

            index.SelectNode("/TABLE_NAME[1]/IDENTIFIER").Should().HaveValue($"{td.Name}_{td[1].Name}");
            index.SelectNode("/TABLE_NAME[2]/IDENTIFIER").Should().HaveValue(td.Name);
            index.Select("/SORT_SPECIFICATION_LIST").Should().HaveCount(1);
            index.SelectNode("//SORT_SPECIFICATION[1]/FIELD/IDENTIFIER").Should().HaveValue(td[1].Name);
        }
    }
}
