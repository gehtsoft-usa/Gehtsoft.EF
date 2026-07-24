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
    /// Behavioural proof, on a live SQLite + SpatiaLite database, that a geometry predicate can take its
    /// operand from a subquery when that subquery projects a <see cref="GeometryValueForm.Native"/>
    /// geometry (the raw column, no WKB output wrap): cities whose point intersects the region polygon
    /// returned by a subquery. This exercises the whole native-form path - native projection feeding a
    /// server-side predicate operand - which the WKB output form could not (a WKB blob is not a geometry).
    /// Skips when the native library is unavailable; serialized through the global enable-spatial flag.
    /// </summary>
    [Collection("SpatialiteSqlite")]
    public class GeometryNativeSubquerySpatialiteTest
    {
        [Entity(Scope = "geo_nsq_region", Table = "geo_nsq_region")]
        public class Region
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Polygon)]
            public byte[] Shape { get; set; }
        }

        [Entity(Scope = "geo_nsq_city", Table = "geo_nsq_city")]
        public class City
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        [Fact]
        public void GeoPredicate_WithNativeSubqueryOperand_Executes()
        {
            SpatialiteTestSupport.RunWithSpatialite(connection =>
            {
                using (var q = connection.GetCreateEntityQuery<Region>())
                    q.Execute();
                using (var q = connection.GetCreateEntityQuery<City>())
                    q.Execute();

                TableDescriptor regionTable = AllEntities.Inst[typeof(Region)].TableDescriptor;
                TableDescriptor cityTable = AllEntities.Inst[typeof(City)].TableDescriptor;
                TableDescriptor.ColumnInfo regionShape = GeometryRoundTripSupport.ColumnByName(regionTable, "shape");
                TableDescriptor.ColumnInfo cityShape = GeometryRoundTripSupport.ColumnByName(cityTable, "shape");

                // one region: the box (0,0)-(50,50)
                GeometryRoundTripSupport.InsertShape(connection, regionTable, regionShape,
                    GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 50, 50), 4326));

                // three cities: two inside the box, one far outside
                GeometryRoundTripSupport.InsertShape(connection, cityTable, cityShape, new Point(5, 5) { SRID = 4326 });
                GeometryRoundTripSupport.InsertShape(connection, cityTable, cityShape, new Point(10, 10) { SRID = 4326 });
                GeometryRoundTripSupport.InsertShape(connection, cityTable, cityShape, new Point(100, 100) { SRID = 4326 });

                // subquery yields the region polygon in its NATIVE form (a single row -> a scalar operand)
                var sub = connection.GetSelectQueryBuilder(regionTable);
                sub.AddGeometryValueToResultset(regionShape, "s", GeometryValueForm.Native);

                // count cities intersecting the polygon produced by the subquery -> the two inside
                var builder = connection.GetSelectQueryBuilder(cityTable);
                builder.AddToResultset(AggFn.Count);
                builder.Where.GeoPredicate(SqlGeoPredicateId.Intersects, cityShape, sub);
                using var query = connection.GetQuery(builder);
                query.ExecuteReader();
                int count = query.ReadNext() ? query.GetValue<int>(0) : 0;

                count.Should().Be(2);
            });
        }
    }
}
