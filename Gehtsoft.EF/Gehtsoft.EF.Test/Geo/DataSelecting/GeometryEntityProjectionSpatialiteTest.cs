using AwesomeAssertions;
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
    /// Behavioural entity-level geometry projection / order-by / group-by / aggregation on a live SQLite +
    /// SpatiaLite database (values via <see cref="GeometryEntityProjectionChecks"/>), plus the whole-entity
    /// read: a <c>GetSelectEntitiesQuery&lt;T&gt;()</c> auto-selects the geometry column through the WKB
    /// output wrap so the property is populated with portable WKB and decodes on read. Generic geometry,
    /// SRID 0 (Cartesian). Skips when the native library is unavailable. Server engines are covered by the
    /// acceptance test.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryEntityProjectionSpatialiteTest
    {
        [Entity(Scope = "geo_ent_proj_sl", Table = "geo_ent_proj")]
        public class GeoEntProj
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry, Srid = 0)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void EntityProjection_OrderBy_GroupBy_Aggregation()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoEntProj>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntProj)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                GeometryEntityProjectionChecks.RunAll<GeoEntProj>(connection, table, shape, "Shape");
            });
        }

        [Fact]
        public void WholeEntityRead_PopulatesGeometryProperty()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoEntProj>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntProj)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");
                GeometryRoundTripSupport.InsertShape(connection, table, shape, new Point(3, 4) { SRID = 0 });

                using var select = connection.GetSelectEntitiesQuery<GeoEntProj>();
                var list = select.ReadAll<GeoEntProj>();

                list.Should().ContainSingle();
                list[0].Shape.Should().NotBeNull();
                var decoded = (Point)new NtsGeometryCodec().FromWkb(list[0].Shape, 0);
                decoded.X.Should().Be(3);
                decoded.Y.Should().Be(4);
            });
        }
    }
}
