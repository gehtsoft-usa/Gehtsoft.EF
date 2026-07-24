using System.Collections.Generic;
using System.Data;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Geo.NetTopologySuite;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Shared value-correctness checks for the ENTITY-level geometry projection / order-by / group-by /
    /// aggregation surface — the entity twin of <see cref="GeometryProjectionChecks"/>. Rows are inserted
    /// through the shared pure-SQL helper (insert is Area 2, not the concern here); every read is built and
    /// run through the entity query API (<c>GetSelectEntitiesQuery&lt;T&gt;</c> + <c>AddGeometryScalarTo*</c>).
    /// Generic geometry, SRID 0 (Cartesian) so every engine computes identical planar values. The entity type
    /// <typeparamref name="T"/> owns the geometry property named <paramref name="property"/> mapped to
    /// <paramref name="shape"/>.
    /// </summary>
    internal static class GeometryEntityProjectionChecks
    {
        private const double Eps = 1e-6;

        public static void RunAll<T>(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, string property)
        {
            // --- Area of a 2x2 box = 4 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
            Scalar<T>(connection, SqlGeoFunctionId.Area, property).Should().BeApproximately(4.0, Eps);

            // --- Length of a linestring of length 3 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("LINESTRING(0 0, 3 0)"));
            Scalar<T>(connection, SqlGeoFunctionId.Length, property).Should().BeApproximately(3.0, Eps);

            // --- X / Y of POINT(3 4); distance to the origin = 5 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 4)"));
            Scalar<T>(connection, SqlGeoFunctionId.X, property).Should().BeApproximately(3.0, Eps);
            Scalar<T>(connection, SqlGeoFunctionId.Y, property).Should().BeApproximately(4.0, Eps);
            Distance<T>(connection, property, GeometryRoundTripSupport.Wkt("POINT(0 0)")).Should().BeApproximately(5.0, Eps);

            // --- order-by-distance (nearest first) + top-N via Limit ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 0)")); // d = 3
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(1 0)")); // d = 1
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(2 0)")); // d = 2
            var origin = GeometryRoundTripSupport.Wkt("POINT(0 0)");
            var ordered = DistancesOrdered<T>(connection, property, origin, 0);
            ordered.Should().HaveCount(3);
            ordered[0].Should().BeApproximately(1.0, Eps);
            ordered[1].Should().BeApproximately(2.0, Eps);
            ordered[2].Should().BeApproximately(3.0, Eps);
            var nearest2 = DistancesOrdered<T>(connection, property, origin, 2);
            nearest2.Should().HaveCount(2);
            nearest2[1].Should().BeApproximately(2.0, Eps);

            // --- AVG(Area) over all rows: boxes of area 4 and 16 -> 10 ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 4, 4)));
            AvgArea<T>(connection, property).Should().BeApproximately(10.0, Eps);

            // --- GROUP BY a geo scalar (Area) + COUNT, ordered by Area: {4:2, 16:1} ---
            GeometryRoundTripSupport.DeleteAll(connection, table);
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
            GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 4, 4)));
            var groups = AreaGroups<T>(connection, property);
            groups.Should().HaveCount(2);
            groups[0].Area.Should().BeApproximately(4.0, Eps);
            groups[0].Count.Should().Be(2);
            groups[1].Area.Should().BeApproximately(16.0, Eps);
            groups[1].Count.Should().Be(1);
        }

        private static double Scalar<T>(SqlDbConnection connection, SqlGeoFunctionId op, string property)
        {
            using var q = connection.GetSelectEntitiesQueryBase<T>();
            q.AddGeometryScalarToResultset<T>(op, property, DbType.Double, "v");
            q.ExecuteReader();
            return q.ReadNext() ? q.GetValue<double>("v") : double.NaN;
        }

        private static double Distance<T>(SqlDbConnection connection, string property, NetTopologySuite.Geometries.Geometry origin)
        {
            using var q = connection.GetSelectEntitiesQueryBase<T>();
            q.AddGeometryScalarToResultset<T>(SqlGeoFunctionId.Distance, property, DbType.Double, "v", parameterName: "p");
            q.Query.BindGeometryParam("p", origin);
            q.ExecuteReader();
            return q.ReadNext() ? q.GetValue<double>("v") : double.NaN;
        }

        private static List<double> DistancesOrdered<T>(SqlDbConnection connection, string property, NetTopologySuite.Geometries.Geometry origin, int limit)
        {
            using var q = connection.GetSelectEntitiesQueryBase<T>();
            q.AddGeometryScalarToResultset<T>(SqlGeoFunctionId.Distance, property, DbType.Double, "d", parameterName: "p");
            q.AddGeometryScalarToOrderBy<T>(SqlGeoFunctionId.Distance, property, SortDir.Asc, parameterName: "p");
            if (limit > 0)
                q.Limit = limit;
            q.Query.BindGeometryParam("p", origin);
            q.ExecuteReader();
            var distances = new List<double>();
            while (q.ReadNext())
                distances.Add(q.GetValue<double>("d"));
            return distances;
        }

        private static double AvgArea<T>(SqlDbConnection connection, string property)
        {
            using var q = connection.GetSelectEntitiesQueryBase<T>();
            q.AddGeometryScalarToResultset<T>(AggFn.Avg, SqlGeoFunctionId.Area, property, DbType.Double, "v");
            q.ExecuteReader();
            return q.ReadNext() ? q.GetValue<double>("v") : double.NaN;
        }

        private static List<(double Area, int Count)> AreaGroups<T>(SqlDbConnection connection, string property)
        {
            using var q = connection.GetSelectEntitiesQueryBase<T>();
            q.AddGeometryScalarToResultset<T>(SqlGeoFunctionId.Area, property, DbType.Double, "area");
            q.AddToResultset(AggFn.Count, "ID", "cnt");
            q.AddGeometryScalarToGroupBy<T>(SqlGeoFunctionId.Area, property);
            q.AddGeometryScalarToOrderBy<T>(SqlGeoFunctionId.Area, property, SortDir.Asc);
            q.ExecuteReader();
            var rows = new List<(double, int)>();
            while (q.ReadNext())
                rows.Add((q.GetValue<double>("area"), q.GetValue<int>("cnt")));
            return rows;
        }
    }
}
