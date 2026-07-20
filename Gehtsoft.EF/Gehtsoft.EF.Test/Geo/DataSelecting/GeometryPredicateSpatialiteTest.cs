using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Behavioural spatial WHERE + mass delete on a live SQLite + SpatiaLite database: a topological
    /// predicate (<c>ST_Intersects</c>), a within-distance predicate (<c>ST_Distance(...) &lt;= d</c>,
    /// planar degrees here) and a mass delete driven by a spatial filter. Skips when the native library
    /// is unavailable; serialized against other SQLite tests through the global enable-spatial flag.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryPredicateSpatialiteTest
    {
        [Entity(Scope = "geo_pred_sl", Table = "geo_pred")]
        public class GeoPred
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void SpatialWhere_And_MassDelete()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoPred>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoPred)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                var a = new Point(0, 0) { SRID = 4326 };
                var b = new Point(10, 10) { SRID = 4326 };
                var c = new Point(100, 100) { SRID = 4326 };
                GeometryRoundTripSupport.InsertShape(connection, table, shape, a);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, b);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, c);

                // a point intersects another only when they are equal -> exactly row B
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b)
                    .Should().Be(1);

                // within ~20 (planar degrees) of the origin -> A(0,0) and B(10,10), not C(100,100)
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.DWithin, a, distance: 20)
                    .Should().Be(2);

                // mass-delete the row that intersects B -> A and C remain
                GeometryRoundTripSupport.DeleteWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b);
                GeometryRoundTripSupport.CountAll(connection, table).Should().Be(2);
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b)
                    .Should().Be(0);
            });
        }
    }
}
