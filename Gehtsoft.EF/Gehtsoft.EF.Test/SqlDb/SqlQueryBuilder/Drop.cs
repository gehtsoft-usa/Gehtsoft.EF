using System.Data;
using System.Linq;
using FluentAssertions;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.SqlParser;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.SqlQueryBuilder
{
    public class Drop
    {
        [Fact]
        public void Table()
        {
            using var connection = new DummySqlConnection();
            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };
            var builder = connection.GetDropTableBuilder(table);
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("DROP_TABLE").Should().HaveCount(1);
            ast.SelectNode("/DROP_TABLE[1]/IF_EXIST").Should().Exist();

            ast.SelectNode("/DROP_TABLE[1]/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName");
        }

        [Fact]
        public void View()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetDropViewBuilder("viewName");
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Select("DROP_VIEW").Should().HaveCount(1);
            ast.SelectNode("/DROP_VIEW[1]/IF_EXIST").Should().Exist();
            ast.SelectNode("/DROP_VIEW[1]/TABLE_NAME/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("viewName");
        }

        [Fact]
        public void Index()
        {
            TableDescriptor table = new TableDescriptor()
            {
                Name = "tableName",
            };

            using var connection = new DummySqlConnection();
            var builder = connection.GetDropIndexBuilder(table, "indexName");
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();
            ast.Select("DROP_INDEX").Should().HaveCount(1);

            ast.SelectNode("/DROP_INDEX[1]/IF_EXIST").Should().Exist();
            ast.SelectNode("/DROP_INDEX[1]/TABLE_NAME[1]/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName_indexName");

            ast.SelectNode("/DROP_INDEX[1]/TABLE_NAME[2]/IDENTIFIER")
                .Should().Exist()
                .And.HaveValue("tableName");
        }
    }
}
