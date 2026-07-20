using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Behavioural round-trip of a geometry value through the pure-SQL builder surface on a live
    /// SQLite + SpatiaLite database: INSERT wraps the bound WKB parameter in <c>ST_GeomFromWKB</c>,
    /// SELECT wraps the column in <c>ST_AsBinary</c>, and UPDATE re-wraps on write. The geometry object
    /// is bound and read through the NTS module extension methods (<see cref="GeometrySqlExtensions"/>),
    /// keeping the core SQL layer on <c>byte[]</c>. Skips when the native library is unavailable;
    /// serialized against other SQLite tests through the global enable-spatial flag. The other engines are
    /// covered by <c>GeometryRoundTripAcceptanceTest</c>.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryRoundTripSpatialiteTest
    {
        [Entity(Scope = "geo_roundtrip", Table = "geo_rt")]
        public class GeoRt
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void InsertSelectUpdate_RoundTripsGeometryThroughValueWrapAndOutputWrap()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoRt>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoRt)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // 2-D points to match the POINT/XY column (the codec emits WKB with exactly the
                // geometry's own ordinates, so a 2-D point serializes as 2-D WKB).
                GeometryRoundTripSupport.InsertShape(connection, table, shape, new Point(1.5, 2.5) { SRID = 4326 });

                Geometry readBack = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                readBack.Should().BeOfType<Point>();
                ((Point)readBack).X.Should().Be(1.5);
                ((Point)readBack).Y.Should().Be(2.5);

                GeometryRoundTripSupport.UpdateShape(connection, table, shape, new Point(-71.0, 42.0) { SRID = 4326 });

                Geometry afterUpdate = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                ((Point)afterUpdate).X.Should().Be(-71.0);
                ((Point)afterUpdate).Y.Should().Be(42.0);
            });
        }
    }
}
