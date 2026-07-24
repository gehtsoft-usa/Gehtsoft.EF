using System.Linq;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Entity.Tools;
using Gehtsoft.EF.Test.Utils.DummyDb;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// DB-free tests of the geometry LINQ surface: the <see cref="SqlSpatial"/> marker methods compile to the
    /// dialect's spatial SQL. A predicate in <c>Where</c> becomes a complete boolean condition wrapping the
    /// bound WKB operand in the constructor; a within-distance predicate becomes a distance comparison; a
    /// geometry scalar in <c>Select</c>/<c>OrderBy</c> becomes the measurement expression. The dummy dialect
    /// renders the portable OGC grammar; the generated SQL is asserted by substring (the test grammar has no
    /// spatial-function rule). The operand bytes are irrelevant here (nothing executes) - only the SQL shape.
    /// </summary>
    public class GeometryLinqSqlTest
    {
        [Entity(Scope = "geo_linq_sql", Table = "geo_linq_sql")]
        public class GeoLinq
        {
            [EntityProperty(Field = "id", AutoId = true)] public int ID { get; set; }
            [EntityProperty(Field = "name", Size = 32)] public string Name { get; set; }
            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry, Srid = 4326)] public byte[] Shape { get; set; }
        }

        private static QueryableEntityProvider Provider(out DummySqlConnection connection)
        {
            connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return new QueryableEntityProvider(new ExistingConnectionFactory(connection));
        }

        [Fact]
        public void Where_GeoPredicate_WrapsOperandAndUsesColumn()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Intersects(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                string sql = query.Query.Builder.Query;

                sql.Should().Contain("ST_Intersects(");
                sql.Should().Contain(".shape, ST_GeomFromWKB(");
                sql.Should().Contain(", 4326))");
            }
        }

        [Fact]
        public void Where_DWithin_RendersDistanceComparison()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.DWithin(s.Shape, wkb, 100.0)));
                query.Query.Builder.PrepareQuery();
                string sql = query.Query.Builder.Query;

                sql.Should().Contain("ST_Distance(");
                sql.Should().Contain(".shape, ST_GeomFromWKB(");
                sql.Should().Contain("<= 100");
            }
        }

        [Fact]
        public void Select_GeoScalar_RendersMeasurement()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Select(s => new { s.Name, A = SqlSpatial.Area(s.Shape) }));
                query.Query.Builder.PrepareQuery();
                string sql = query.Query.Builder.Query;

                sql.Should().Contain("ST_Area(");
                sql.Should().Contain(".shape)");
            }
        }

        [Fact]
        public void OrderBy_GeoDistance_RendersOrderByMeasurement()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.OrderBy(s => SqlSpatial.Distance(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                string sql = query.Query.Builder.Query;

                sql.Should().Contain("ORDER BY ST_Distance(");
                sql.Should().Contain("ST_GeomFromWKB(");
                sql.Should().Contain(", 4326)");
            }
        }

        [Fact]
        public void Where_Contains_RendersStContains()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Contains(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Contains(");
            }
        }

        [Fact]
        public void Where_Within_RendersStWithin()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Within(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Within(");
            }
        }

        [Fact]
        public void Where_Disjoint_RendersStDisjoint()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Disjoint(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Disjoint(");
            }
        }

        [Fact]
        public void Where_Touches_RendersStTouches()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Touches(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Touches(");
            }
        }

        [Fact]
        public void Where_Overlaps_RendersStOverlaps()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Overlaps(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Overlaps(");
            }
        }

        [Fact]
        public void Where_Crosses_RendersStCrosses()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.Crosses(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Crosses(");
            }
        }

        [Fact]
        public void Where_SpatialEquals_RendersStEquals()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                byte[] wkb = { 1 };
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Where(s => SqlSpatial.SpatialEquals(s.Shape, wkb)));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Equals(");
            }
        }

        [Fact]
        public void Select_Length_RendersStLength()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Select(s => new { s.Name, L = SqlSpatial.Length(s.Shape) }));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Length(");
            }
        }

        [Fact]
        public void Select_X_RendersStX()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Select(s => new { s.Name, X = SqlSpatial.X(s.Shape) }));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_X(");
            }
        }

        [Fact]
        public void Select_Y_RendersStY()
        {
            var provider = Provider(out var connection);
            using (connection)
            {
                var query = provider.CompileToQuery<GeoLinq>(connection, e => e.Select(s => new { s.Name, Y = SqlSpatial.Y(s.Shape) }));
                query.Query.Builder.PrepareQuery();
                query.Query.Builder.Query.Should().Contain("ST_Y(");
            }
        }
    }
}
