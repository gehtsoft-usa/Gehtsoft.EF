using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Shared value-correctness checks for the geometry projection / order-by / group-by / aggregation
    /// surface. Runs against an empty geometry table (generic geometry, SRID 0 - Cartesian, so every
    /// engine computes identical planar values) and asserts the actual returned numbers, not just that the
    /// SQL executes.
    /// </summary>
    internal static class GeometryProjectionChecks
    {
        private const double Eps = 1e-6;

        public static void RunAll(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape)
        {
            // --- Area of a 2x2 box = 4 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
            GeometryRoundTripSupport.SelectScalar(connection, table, shape, SqlGeoFunctionId.Area)
                .Should().BeApproximately(4.0, Eps);

            // --- Length (perimeter) of a linestring of length 3 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("LINESTRING(0 0, 3 0)"));
            GeometryRoundTripSupport.SelectScalar(connection, table, shape, SqlGeoFunctionId.Length)
                .Should().BeApproximately(3.0, Eps);

            // --- X / Y of POINT(3 4); distance to the origin = 5 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 4)"));
            GeometryRoundTripSupport.SelectScalar(connection, table, shape, SqlGeoFunctionId.X).Should().BeApproximately(3.0, Eps);
            GeometryRoundTripSupport.SelectScalar(connection, table, shape, SqlGeoFunctionId.Y).Should().BeApproximately(4.0, Eps);
            GeometryRoundTripSupport.SelectDistance(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(0 0)"))
                .Should().BeApproximately(5.0, Eps);

            // --- order-by-distance (nearest first) + top-N ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 0)")); // d = 3
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(1 0)")); // d = 1
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(2 0)")); // d = 2
            var origin = GeometryRoundTripSupport.Wkt("POINT(0 0)");
            var ordered = GeometryRoundTripSupport.DistancesOrdered(connection, table, shape, origin);
            ordered.Should().HaveCount(3);
            ordered[0].Should().BeApproximately(1.0, Eps);
            ordered[1].Should().BeApproximately(2.0, Eps);
            ordered[2].Should().BeApproximately(3.0, Eps);
            var nearest2 = GeometryRoundTripSupport.DistancesOrdered(connection, table, shape, origin, limit: 2);
            nearest2.Should().HaveCount(2);
            nearest2[1].Should().BeApproximately(2.0, Eps);

            // --- AVG(Area) over all rows: boxes of area 4 and 16 -> 10 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2))); // 4
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 4, 4))); // 16
            AvgArea(connection, table, shape).Should().BeApproximately(10.0, Eps);

            // --- GROUP BY a geo scalar (Area) + COUNT, ordered by Area: {4:2, 16:1} ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2))); // 4
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2))); // 4
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 4, 4))); // 16
            var groups = AreaGroups(connection, table, shape);
            groups.Should().HaveCount(2);
            groups[0].Area.Should().BeApproximately(4.0, Eps);
            groups[0].Count.Should().Be(2);
            groups[1].Area.Should().BeApproximately(16.0, Eps);
            groups[1].Count.Should().Be(1);
        }

        private static double AvgArea(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryScalarToResultset(AggFn.Avg, SqlGeoFunctionId.Area, shape, DbType.Double, "v");
            using var query = connection.GetQuery(builder);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<double>("v") : double.NaN;
        }

        private static System.Collections.Generic.List<(double Area, int Count)> AreaGroups(
            SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryScalarToResultset(SqlGeoFunctionId.Area, shape, DbType.Double, "area");
            builder.AddToResultset(AggFn.Count, "cnt");
            builder.AddGeometryScalarToGroupBy(SqlGeoFunctionId.Area, shape);
            builder.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Area, shape, SortDir.Asc);
            using var query = connection.GetQuery(builder);
            query.ExecuteReader();
            var rows = new System.Collections.Generic.List<(double, int)>();
            while (query.ReadNext())
                rows.Add((query.GetValue<double>("area"), query.GetValue<int>("cnt")));
            return rows;
        }
    }
}
