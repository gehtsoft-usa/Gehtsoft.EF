using System.Collections.Generic;
using System.Data;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Geo.NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Gehtsoft.EF.Test.Geo
{
    /// <summary>
    /// Shared helpers that drive a geometry value round-trip through the pure-SQL builder surface:
    /// INSERT wraps the bound WKB parameter in the dialect's constructor function, SELECT wraps the
    /// column in the WKB output function, and UPDATE re-wraps on write. The geometry object is bound and
    /// read through the NTS module extension methods, so the core SQL layer stays on <c>byte[]</c>.
    /// Used by both the SpatiaLite behavioural test and the all-engine acceptance test.
    /// </summary>
    internal static class GeometryRoundTripSupport
    {
        public static TableDescriptor.ColumnInfo ColumnByName(TableDescriptor table, string name)
        {
            foreach (TableDescriptor.ColumnInfo column in table)
                if (column.Name == name)
                    return column;
            return null;
        }

        // INSERT/UPDATE rely on the geometry auto-wrap: the builders detect the column's Geometry metadata
        // and wrap the bound WKB parameter in the dialect's FromWkb constructor with no explicit expression.
        // (The explicit SetColumnValueExpressions/AddUpdateColumnExpression override path keeps its own live
        // coverage through the playground; here we exercise the metadata-driven auto-wrap end to end.)
        public static void InsertShape(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, Geometry value)
        {
            var builder = connection.GetInsertQueryBuilder(table);
            builder.ReturnAutoincrement = false;
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam(shape.Name, value);
            query.ExecuteNoData();
        }

        public static void UpdateShape(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, Geometry value)
        {
            var builder = connection.GetUpdateQueryBuilder(table);
            builder.AddUpdateColumn(shape);
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam(shape.Name, value);
            query.ExecuteNoData();
        }

        public static Geometry SelectShape(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryValueToResultset(shape, "shape");
            using var query = connection.GetQuery(builder);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetGeometry("shape") : null;
        }

        // Counts the rows whose geometry satisfies the predicate against the bound query geometry.
        public static int CountWhere(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape,
            SqlGeoPredicateId op, Geometry other, double distance = 0)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddToResultset(AggFn.Count);
            builder.Where.GeoPredicate(op, shape, "p", distance);
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam("p", other);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<int>(0) : 0;
        }

        // Builds an NTS geometry from WKT (SRID 0 by default - Cartesian, so predicate/measurement results
        // are planar and identical across every engine).
        public static Geometry Wkt(string wkt, int srid = 0) => (Geometry)new NtsGeometryCodec().FromWkt(wkt, srid);

        // Serializes an NTS geometry to plain OGC WKB - used to populate a byte[] geometry entity property.
        public static byte[] ToWkb(Geometry g) => new NtsGeometryCodec().ToWkb(g, false);

        // A closed CCW box ring as WKT (CCW exterior orientation keeps Oracle SDO happy).
        public static string Box(double minX, double minY, double maxX, double maxY)
            => System.FormattableString.Invariant(
                $"POLYGON(({minX} {minY}, {maxX} {minY}, {maxX} {maxY}, {minX} {maxY}, {minX} {minY}))");

        // Removes every row of the table.
        public static void DeleteAll(SqlDbConnection connection, TableDescriptor table)
        {
            var builder = connection.GetDeleteQueryBuilder(table);
            using var query = connection.GetQuery(builder);
            query.ExecuteNoData();
        }

        // Projects a unary geometry scalar (Area, Length, X, Y, ...) of the single stored row.
        public static double SelectScalar(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, SqlGeoFunctionId op)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryScalarToResultset(op, shape, DbType.Double, "v");
            using var query = connection.GetQuery(builder);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<double>("v") : double.NaN;
        }

        // Projects the distance from the single stored row's geometry to a bound query geometry.
        public static double SelectDistance(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, Geometry other)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryScalarToResultset(SqlGeoFunctionId.Distance, shape, DbType.Double, "v", parameterName: "p");
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam("p", other);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<double>("v") : double.NaN;
        }

        // Projects the distance to a bound origin AND orders by that same (byte-identical) distance
        // expression, returning the distances in ascending order; top-N when limit > 0. Proves both the
        // scalar projection and order-by-distance (nearest-neighbour) without relying on row ids.
        public static List<double> DistancesOrdered(SqlDbConnection connection, TableDescriptor table,
            TableDescriptor.ColumnInfo shape, Geometry origin, int limit = 0)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddGeometryScalarToResultset(SqlGeoFunctionId.Distance, shape, DbType.Double, "d", parameterName: "p");
            builder.AddGeometryScalarToOrderBy(SqlGeoFunctionId.Distance, shape, SortDir.Asc, parameterName: "p");
            if (limit > 0)
                builder.Limit = limit;
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam("p", origin);
            query.ExecuteReader();
            var distances = new List<double>();
            while (query.ReadNext())
                distances.Add(query.GetValue<double>("d"));
            return distances;
        }

        // Counts all rows in the table.
        public static int CountAll(SqlDbConnection connection, TableDescriptor table)
        {
            var builder = connection.GetSelectQueryBuilder(table);
            builder.AddToResultset(AggFn.Count);
            using var query = connection.GetQuery(builder);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<int>(0) : 0;
        }

        // Mass-deletes the rows whose geometry satisfies the predicate against the bound query geometry.
        public static void DeleteWhere(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape,
            SqlGeoPredicateId op, Geometry other, double distance = 0)
        {
            var builder = connection.GetDeleteQueryBuilder(table);
            builder.Where.GeoPredicate(op, shape, "p", distance);
            using var query = connection.GetQuery(builder);
            query.BindGeometryParam("p", other);
            query.ExecuteNoData();
        }
    }
}
