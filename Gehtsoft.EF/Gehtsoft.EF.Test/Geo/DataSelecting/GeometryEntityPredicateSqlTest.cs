using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.Metadata;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Utils.DummyDb;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Deep, DB-free tests of the entity-level geometry WHERE surface (<c>GeoPredicateOf</c>/<c>GeoScalarOf</c>).
    /// The entity methods resolve the geometry column through the Phase-5 resolution seam and delegate the
    /// rendering to the pure-SQL condition builder, so the WHERE clause carries the same dialect-rendered
    /// predicate the pure-SQL layer produces: a topological predicate wraps the bound WKB operand in the
    /// constructor and references the column; a within-distance predicate becomes a distance comparison; a
    /// scalar becomes a comparable operand; a native-subquery operand is embedded as-is (no constructor wrap).
    /// The dummy dialect renders the portable OGC grammar (per-driver renderings are in GeometryRenderTest);
    /// the WHERE clause is asserted by substring because the test grammar has no spatial-function rule.
    /// </summary>
    public class GeometryEntityPredicateSqlTest
    {
        [Entity(Scope = "geo_ent_pred_sql", Table = "geo_ent_pred_sql")]
        public class GeoEntPredSql
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Srid = 4326)]
            public byte[] Shape { get; set; }
        }

        private static DummySqlConnection GeoConnection()
        {
            var connection = new DummySqlConnection();
            connection.DummyDbSpecifics.SupportsGeometrySpec = true;
            return connection;
        }

        private static byte[] PointWkb() => GeometryRoundTripSupport.ToWkb(new Point(1, 2));

        private static TableDescriptor PlainGeoTable()
            => new TableDescriptor("geo_sub", new[]
            {
                new TableDescriptor.ColumnInfo { Name = "id", DbType = DbType.Int32, PrimaryKey = true },
                new TableDescriptor.ColumnInfo
                {
                    Name = "shape", DbType = DbType.Binary, Nullable = true,
                    Geometry = new GeometryColumnMetadata(typeof(byte[]), 4326, GeometrySubtype.Point, false, false, true, System.Array.Empty<SpatialIndexDefinition>()),
                },
            });

        [Fact]
        public void Where_GeoPredicateOf_WrapsOperandAndUsesColumn()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoEntPredSql>();
            q.Where.GeoPredicateOf<GeoEntPredSql>("Shape", SqlGeoPredicateId.Intersects, PointWkb());
            string where = q.Where.ToString();

            where.Should().Contain("ST_Intersects(");
            where.Should().Contain(".shape, ST_GeomFromWKB(");
            where.Should().Contain(", 4326))");
        }

        [Fact]
        public void Where_GeoPredicateOf_WithinDistance_RendersDistanceComparison()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoEntPredSql>();
            q.Where.GeoPredicateOf<GeoEntPredSql>("Shape", SqlGeoPredicateId.DWithin, PointWkb(), distance: 100);
            string where = q.Where.ToString();

            where.Should().Contain("ST_Distance(");
            where.Should().Contain(".shape, ST_GeomFromWKB(");
            where.Should().Contain("<= 100)");
        }

        [Fact]
        public void Where_GeoScalarOf_UsedAsComparisonOperand()
        {
            using var connection = GeoConnection();
            using var q = connection.GetMultiDeleteEntityQuery<GeoEntPredSql>();
            q.Where.GeoScalarOf<GeoEntPredSql>("Shape", SqlGeoFunctionId.Area).Gt(500.0);
            string where = q.Where.ToString();

            where.Should().Contain("ST_Area(");
            where.Should().Contain(".shape) > @");
        }

        [Fact]
        public void Where_GeoPredicateOf_NativeSubquery_EmbeddedWithoutConstructorWrap()
        {
            using var connection = GeoConnection();
            var subTable = PlainGeoTable();
            var sub = connection.GetSelectQueryBuilder(subTable);
            sub.AddGeometryValueToResultset(subTable["shape"], "g", GeometryValueForm.Native);

            using var q = connection.GetMultiDeleteEntityQuery<GeoEntPredSql>();
            q.Where.GeoPredicateOf<GeoEntPredSql>("Shape", SqlGeoPredicateId.Intersects, (AQueryBuilder)sub);
            string where = q.Where.ToString();

            // the geometry column is qualified and the operand is the raw subquery - the subquery projects
            // the native column with no ST_AsBinary, and it is not wrapped in ST_GeomFromWKB.
            where.Should().Contain("ST_Intersects(");
            where.Should().Contain("(SELECT");
            where.Should().NotContain("ST_GeomFromWKB(");
            where.Should().NotContain("ST_AsBinary(");
        }
    }
}
