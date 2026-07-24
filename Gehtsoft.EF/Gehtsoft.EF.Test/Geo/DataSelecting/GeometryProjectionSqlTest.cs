using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the geometry scalar projection / order-by / group-by surface: a projected
    /// measurement, order-by-distance (whose ORDER BY expression must be byte-identical to the projected
    /// distance), and a grouped aggregate. Dummy dialect renders the OGC grammar; exact-string, alias-bound.
    /// </summary>
    public class GeometryProjectionSqlTest
    {
        private static TableDescriptor GeoTable()
            => new TableDescriptor("geo_rt", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 0, GeometrySubtype.Geometry, false, false, true, System.Array.Empty<SpatialIndexDefinition>()),
                },
            });

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        private static string AliasOf(string query)
        {
            const string marker = "FROM geo_rt AS ";
            int start = query.IndexOf(marker, System.StringComparison.Ordinal) + marker.Length;
            int end = query.IndexOf(' ', start);
            return query.Substring(start, (end < 0 ? query.Length : end) - start);
        }

        [Fact]
        public void Project_ScalarMeasurement()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddGeometryScalarToResultset(SqlGeoFunctionId.Area, table["shape"], DbType.Double, "area");
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be($"SELECT ST_Area({a}.shape) AS area FROM geo_rt AS {a}");
        }

        [Fact]
        public void OrderByDistance_UsesByteIdenticalExpression()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddGeometryScalarToResultset(SqlGeoFunctionId.Distance, table["shape"], DbType.Double, "d", parameterName: "p");
            select.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Distance, table["shape"], SortDir.Asc, parameterName: "p");
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be(
                $"SELECT ST_Distance({a}.shape, ST_GeomFromWKB(@p, 0)) AS d FROM geo_rt AS {a} " +
                $"ORDER BY ST_Distance({a}.shape, ST_GeomFromWKB(@p, 0))");
        }

        [Fact]
        public void GroupByGeoScalar_WithAggregate()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddGeometryScalarToResultset(SqlGeoFunctionId.Area, table["shape"], DbType.Double, "area");
            select.AddToResultset(AggFn.Count, "cnt");
            select.AddGeometryScalarToGroupBy(SqlGeoFunctionId.Area, table["shape"]);
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be(
                $"SELECT ST_Area({a}.shape) AS area, COUNT(*) AS cnt FROM geo_rt AS {a} GROUP BY ST_Area({a}.shape)");
        }
    }
}
