using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Behavioural geometry scalar projection / order-by-distance / group-by / aggregation on a live
    /// SQLite + SpatiaLite database, asserting the actual computed values (SpatiaLite's ST_Area/ST_Distance
    /// are Cartesian, so a 2x2 box is 4 and (3,4)->(0,0) is 5). Skips when the native library is absent.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryProjectionSpatialiteTest
    {
        [Entity(Scope = "geo_proj_sl", Table = "geo_proj")]
        public class GeoProj
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void Projection_OrderBy_GroupBy_Aggregation()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoProj>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoProj)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                GeometryProjectionChecks.RunAll(connection, table, shape);
            });
        }
    }
}
