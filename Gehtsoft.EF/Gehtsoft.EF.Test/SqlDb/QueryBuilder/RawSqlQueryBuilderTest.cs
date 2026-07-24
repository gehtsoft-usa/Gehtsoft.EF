using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.SqlDb.QueryBuilder
{
    /// <summary>
    /// The raw-SQL query builder is a pass-through that carries a literal SQL string: PrepareQuery is a
    /// no-op and Query returns the text verbatim, so a caller can feed hand-written SQL through the same
    /// builder pipeline as the composed builders.
    /// </summary>
    public class RawSqlQueryBuilderTest
    {
        [Fact]
        public void GetRawSqlQueryBuilder_CarriesQueryTextVerbatim()
        {
            using var connection = new DummySqlConnection();

            RawSqlQueryBuilder builder = connection.GetRawSqlQueryBuilder("SELECT 1 FROM dummy");
            builder.PrepareQuery(); // no-op, but must be callable before Query

            builder.Query.Should().Be("SELECT 1 FROM dummy");
        }
    }
}
