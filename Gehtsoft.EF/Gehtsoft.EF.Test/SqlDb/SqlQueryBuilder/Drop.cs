using System.Data;
using System.Linq;
using AwesomeAssertions;
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

            ast.Should().HaveDropTable("tableName").And.HaveIfExists();
        }

        [Fact]
        public void View()
        {
            using var connection = new DummySqlConnection();
            var builder = connection.GetDropViewBuilder("viewName");
            builder.PrepareQuery();
            var ast = builder.Query.ParseSql();

            ast.Should().HaveDropView("viewName").And.HaveIfExists();
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

            ast.Should().HaveDropIndex("tableName_indexName", "tableName").And.HaveIfExists();
        }
    }
}
