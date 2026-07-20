using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the geometry WHERE surface on the SELECT and DELETE builders: a complete
    /// topological predicate, a within-distance predicate, a scalar measurement used as an operand, and a
    /// mass delete with a spatial filter. The dummy dialect renders the portable OGC grammar (per-driver
    /// renderings are covered by GeometryRenderTest); the generated statement is asserted by exact string
    /// (the test SQL grammar has no spatial-function rule). The table alias comes from a process-global
    /// counter, so it is bound from the generated FROM clause rather than hard-coded.
    /// </summary>
    public class GeometryPredicateSqlTest
    {
        private static TableDescriptor GeoTable()
            => new TableDescriptor("geo_rt", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, new SpatialIndexDefinition[0]),
                },
            });

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        // "... FROM geo_rt AS entityN WHERE ..." -> "entityN"
        private static string AliasOf(string query)
        {
            int start = query.IndexOf("AS ", System.StringComparison.Ordinal) + 3;
            int end = query.IndexOf(' ', start);
            return query.Substring(start, end - start);
        }

        [Fact]
        public void Where_TopologicalPredicate_WrapsParameterAndUsesColumn()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["id"]);
            select.Where.GeoPredicate(SqlGeoPredicateId.Intersects, table["shape"], "p");
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be($"SELECT {a}.id FROM geo_rt AS {a} WHERE ST_Intersects({a}.shape, ST_GeomFromWKB(@p, 4326))");
        }

        [Fact]
        public void Where_WithinDistance_RendersDistanceComparison()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["id"]);
            select.Where.GeoPredicate(SqlGeoPredicateId.DWithin, table["shape"], "p", 100);
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be($"SELECT {a}.id FROM geo_rt AS {a} WHERE (ST_Distance({a}.shape, ST_GeomFromWKB(@p, 4326)) <= 100)");
        }

        [Fact]
        public void Where_ScalarMeasurement_UsedAsComparisonOperand()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(table["id"]);
            select.Where.GeoScalar(SqlGeoFunctionId.Area, table["shape"]).Gt().Parameter("min");
            select.PrepareQuery();

            string a = AliasOf(select.Query);
            select.Query.Should().Be($"SELECT {a}.id FROM geo_rt AS {a} WHERE ST_Area({a}.shape) > @min");
        }

        [Fact]
        public void Delete_WithSpatialFilter_UsesTableQualifiedPredicate()
        {
            using var connection = GeoConnection();
            var table = GeoTable();
            var delete = connection.GetDeleteQueryBuilder(table);
            delete.Where.GeoPredicate(SqlGeoPredicateId.Intersects, table["shape"], "p");
            delete.PrepareQuery();

            delete.Query.Should().Be("DELETE FROM geo_rt WHERE ST_Intersects(geo_rt.shape, ST_GeomFromWKB(@p, 4326))");
        }
    }
}
