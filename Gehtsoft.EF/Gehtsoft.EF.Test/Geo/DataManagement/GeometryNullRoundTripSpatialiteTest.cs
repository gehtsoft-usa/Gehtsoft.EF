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
    /// Verifies that a NULL geometry propagates correctly through the driver-dependent conversion functions
    /// on a live SQLite + SpatiaLite database. The SQL text is fixed at <c>PrepareQuery</c> before any value
    /// binds, so the INSERT still emits the <c>ST_GeomFromWKB(@p, srid)</c> value-wrap and the SELECT still
    /// emits the <c>ST_AsBinary</c> output-wrap — the constructor/accessor functions are therefore always
    /// invoked, here on a NULL argument. This pins down <c>FromWkb(NULL) = NULL</c> and
    /// <c>AsBinary(NULL) = NULL</c>: writing a null geometry stores SQL NULL and reads back as <c>null</c>.
    /// The server engines are covered by <c>GeometryNullRoundTripAcceptanceTest</c>.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryNullRoundTripSpatialiteTest
    {
        [Entity(Scope = "geo_null_rt", Table = "geo_null_rt")]
        public class GeoNullRt
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Nullable = true)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void InsertNull_ReadsBackNull()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<GeoNullRt>())
                    q.Execute();

                TableDescriptor table = AllEntities.Inst[typeof(GeoNullRt)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // Write a NULL geometry: the FromWkb value-wrap is still emitted, its parameter bound to NULL.
                GeometryRoundTripSupport.InsertShape(connection, table, shape, null);

                // Read it back through the AsBinary output-wrap: expect null, not an exception or empty geometry.
                Geometry readBack = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                readBack.Should().BeNull();
            });
        }
    }
}
