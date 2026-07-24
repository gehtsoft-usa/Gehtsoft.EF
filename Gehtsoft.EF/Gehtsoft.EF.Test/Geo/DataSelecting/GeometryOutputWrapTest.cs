using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free test of the SELECT output-wrap: a geometry column is projected through the dialect's
    /// WKB output function so a portable <c>byte[]</c> comes back, never the raw column. The dummy dialect
    /// renders the portable OGC grammar (per-driver renderings are covered by GeometryRenderTest); the
    /// generated statement is asserted by exact string (the test SQL grammar has no spatial-function rule).
    /// </summary>
    public class GeometryOutputWrapTest
    {
        private static TableDescriptor GeoTable()
            => new TableDescriptor("geo_rt", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, System.Array.Empty<SpatialIndexDefinition>()),
                },
            });

        [Fact]
        public void Select_WrapsGeometryColumnInWkbOutputFunction()
        {
            using var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            var table = GeoTable();

            var select = connection.GetSelectQueryBuilder(table);
            select.AddGeometryValueToResultset(table["shape"], "shape");
            select.PrepareQuery();

            // the table alias comes from a process-global counter, so bind it from the generated FROM
            // clause rather than hard-coding it; the rest of the statement is asserted exactly.
            string alias = select.Query.Substring(select.Query.LastIndexOf(' ') + 1);
            select.Query.Should().Be($"SELECT ST_AsBinary({alias}.shape) AS shape FROM geo_rt AS {alias}");
        }
    }
}
