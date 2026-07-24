using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Behavioural round-trip of a geometry entity property through the <b>entity</b> insert/update query
    /// path on a live SQLite + SpatiaLite database. Confirms that <c>GetInsertEntityQuery</c> /
    /// <c>GetUpdateEntityQuery</c> round-trip a <c>byte[]</c> (WKB) geometry property purely by inheriting the
    /// Increment-1 auto-wrap (the entity builders are zero-touch: they delegate to the pure-SQL
    /// Insert/UpdateQueryBuilder which wraps the bound WKB parameter in <c>ST_GeomFromWKB</c>). A nullable
    /// property set to <c>null</c> stores SQL NULL and reads back as <c>null</c>. Read-back uses the Phase-4
    /// pure-SQL select output-wrap because entity SELECT of a geometry is Area 3 (a later increment). The NTS
    /// object-property variant + the full engine matrix are covered by
    /// <c>GeometryEntityRoundTripAcceptanceTest</c>. Skips when the native library is unavailable.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryEntityRoundTripSpatialiteTest
    {
        [Entity(Scope = "geo_ent_rt", Table = "geo_ent_rt")]
        public class GeoEntRt
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Nullable = true)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void EntityInsertUpdate_RoundTripsGeometryProperty()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoEntRt>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntRt)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // INSERT through the entity path: the byte[] WKB property is auto-wrapped in ST_GeomFromWKB.
                var entity = new GeoEntRt { Shape = GeometryRoundTripSupport.ToWkb(new Point(1.5, 2.5) { SRID = 4326 }) };
                using (var q = connection.GetInsertEntityQuery<GeoEntRt>())
                    q.Execute(entity);

                Geometry readBack = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                readBack.Should().BeOfType<Point>();
                ((Point)readBack).X.Should().Be(1.5);
                ((Point)readBack).Y.Should().Be(2.5);

                // UPDATE (by id) through the entity path re-wraps on write.
                entity.Shape = GeometryRoundTripSupport.ToWkb(new Point(-71.0, 42.0) { SRID = 4326 });
                using (var q = connection.GetUpdateEntityQuery<GeoEntRt>())
                    q.Execute(entity);

                Geometry afterUpdate = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                ((Point)afterUpdate).X.Should().Be(-71.0);
                ((Point)afterUpdate).Y.Should().Be(42.0);

                // A null geometry stores SQL NULL and reads back as null (FromWkb(NULL) = NULL).
                entity.Shape = null;
                using (var q = connection.GetUpdateEntityQuery<GeoEntRt>())
                    q.Execute(entity);

                GeometryRoundTripSupport.SelectShape(connection, table, shape).Should().BeNull();
            });
        }
    }
}
