using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Behavioural entity-level spatial WHERE + mass delete on a live SQLite + SpatiaLite database, mirroring
    /// the pure-SQL <c>GeometryPredicateSpatialiteTest</c> through the entity API: a topological predicate
    /// (<c>GeoPredicateOf(Intersects)</c>), a within-distance predicate (<c>GeoPredicateOf(DWithin)</c>) and a
    /// mass delete (<c>GetMultiDeleteEntityQuery(...).Where.GeoPredicateOf</c>). Filtering is asserted via
    /// COUNT queries, so the not-yet-geo-aware whole-entity read (Area 3 / Increment 5) is not exercised. The
    /// operand geometry is a NetTopologySuite object (encoded to WKB by the NTS module's own codec, so no
    /// global codec registration is needed and the test can share the SpatiaLite collection). Skips when the
    /// native library is unavailable. The server engines are covered by the acceptance test.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryEntityPredicateSpatialiteTest
    {
        [Entity(Scope = "geo_ent_where_sl", Table = "geo_ent_where")]
        public class GeoEntWhere
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void EntitySpatialWhere_And_MassDelete()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoEntWhere>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntWhere)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                var a = new Point(0, 0) { SRID = 4326 };
                var b = new Point(10, 10) { SRID = 4326 };
                var c = new Point(100, 100) { SRID = 4326 };
                GeometryRoundTripSupport.InsertShape(connection, table, shape, a);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, b);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, c);

                // a point intersects another only when they are equal -> exactly row B
                CountWhere(connection, SqlGeoPredicateId.Intersects, b).Should().Be(1);

                // within ~20 (planar degrees) of the origin -> A(0,0) and B(10,10), not C(100,100)
                CountWhere(connection, SqlGeoPredicateId.DWithin, a, distance: 20).Should().Be(2);

                // mass-delete the row that intersects B through the entity API -> A and C remain
                using (var del = connection.GetMultiDeleteEntityQuery<GeoEntWhere>())
                {
                    del.Where.GeoPredicateOf<GeoEntWhere>("Shape", SqlGeoPredicateId.Intersects, b);
                    del.Execute();
                }

                CountWhere(connection, SqlGeoPredicateId.Intersects, b).Should().Be(0);
                using (var all = connection.GetSelectEntitiesCountQuery<GeoEntWhere>())
                    all.RowCount.Should().Be(2);
            });
        }

        private static int CountWhere(SqlDbConnection connection, SqlGeoPredicateId op, Geometry operand, double distance = 0)
        {
            using var q = connection.GetSelectEntitiesCountQuery<GeoEntWhere>();
            q.Where.GeoPredicateOf<GeoEntWhere>("Shape", op, operand, distance: distance);
            return q.RowCount;
        }
    }
}
