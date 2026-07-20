using System;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Acceptance-tier coverage of the whole geometry WHERE surface against every live server engine
    /// (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8): all eight topological predicates, the within-distance
    /// predicate, and the accessors. The column is a generic geometry with SRID 0 (Cartesian), so every
    /// engine evaluates the operations planar and the expected results are identical everywhere (MySQL 8
    /// would otherwise treat SRID 4326 geodetically and validate lat/lon). Oracle Locator has no
    /// <c>Crosses</c> and no <c>IsEmpty</c>, so those throw there. SQLite/SpatiaLite has its own tests.
    /// </summary>
    public class GeometrySpatialOpsAcceptanceTest : IClassFixture<GeometrySpatialOpsAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometrySpatialOpsAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometrySpatialOpsAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geoops_acc", Table = "geo_ops_acc")]
        public class GeoOps
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Geometry, Srid = 0)]
            public byte[] Shape { get; set; }
        }

        private static bool IsOracle(string connectionName)
            => AppConfiguration.Instance.GetSqlConnection(connectionName).Driver == "oracle";

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_ops_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoOps>())
                    q.Execute();
        }

        private static void SkipIfGeometryUnsupported(SqlDbConnection connection, string connectionName)
        {
            if (AppConfiguration.Instance.GetSqlConnection(connectionName).Driver == "npgsql")
            {
                using var q = connection.GetQuery(
                    "SELECT COALESCE((SELECT 'yes' FROM pg_type WHERE typname = 'geometry' LIMIT 1), 'no')", true);
                q.ExecuteReader();
                if ((q.ReadNext() ? q.GetValue<object>(0)?.ToString() : null) != "yes")
                    Assert.Skip("PostGIS is not installed in this database (run 'CREATE EXTENSION postgis;').");
            }
        }

        private SqlDbConnection Prepare(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            new CatalogEntityController(typeof(GeoOps), "geoops_acc").CreateTables(connection, "1.0.0");
            return connection;
        }

        // (op, stored geometry, probe that makes it TRUE, probe that makes it FALSE)
        public static TheoryData<string, SqlGeoPredicateId, string, string, string> PredicateCases()
        {
            var box = GeometryRoundTripSupport.Box;
            var data = new TheoryData<string, SqlGeoPredicateId, string, string, string>();
            foreach (var connection in SqlConnectionSources.SqlConnections("-sqlite"))
            {
                string e = connection.Name;
                data.Add(e, SqlGeoPredicateId.Equals, box(0, 0, 2, 2), box(0, 0, 2, 2), box(0, 0, 1, 1));
                data.Add(e, SqlGeoPredicateId.Disjoint, box(0, 0, 2, 2), box(5, 5, 7, 7), box(1, 1, 3, 3));
                data.Add(e, SqlGeoPredicateId.Intersects, box(0, 0, 2, 2), box(1, 1, 3, 3), box(5, 5, 7, 7));
                data.Add(e, SqlGeoPredicateId.Within, box(1, 1, 2, 2), box(0, 0, 3, 3), box(5, 5, 7, 7));
                data.Add(e, SqlGeoPredicateId.Contains, box(0, 0, 3, 3), box(1, 1, 2, 2), box(5, 5, 7, 7));
                data.Add(e, SqlGeoPredicateId.Touches, box(0, 0, 2, 2), box(2, 0, 4, 2), box(1, 1, 3, 3));
                data.Add(e, SqlGeoPredicateId.Overlaps, box(0, 0, 2, 2), box(1, 1, 3, 3), box(5, 5, 7, 7));
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(PredicateCases))]
        public void TopologicalPredicate(string connectionName, SqlGeoPredicateId op, string stored, string probeTrue, string probeFalse)
        {
            var connection = Prepare(connectionName);
            try
            {
                var table = AllEntities.Inst[typeof(GeoOps)].TableDescriptor;
                var shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(stored));
                GeometryRoundTripSupport.CountWhere(connection, table, shape, op, GeometryRoundTripSupport.Wkt(probeTrue))
                    .Should().Be(1, $"{op} should hold");
                GeometryRoundTripSupport.CountWhere(connection, table, shape, op, GeometryRoundTripSupport.Wkt(probeFalse))
                    .Should().Be(0, $"{op} should not hold");
            }
            finally
            {
                DropTable(connection);
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void Crosses(string connectionName)
        {
            var connection = Prepare(connectionName);
            try
            {
                var table = AllEntities.Inst[typeof(GeoOps)].TableDescriptor;
                var shape = GeometryRoundTripSupport.ColumnByName(table, "shape");
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("LINESTRING(0 0, 2 2)"));

                if (IsOracle(connectionName))
                {
                    // Oracle Locator cannot express OGC Crosses (decision 11).
                    ((Action)(() => GeometryRoundTripSupport.CountWhere(connection, table, shape,
                        SqlGeoPredicateId.Crosses, GeometryRoundTripSupport.Wkt("LINESTRING(0 2, 2 0)"))))
                        .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
                    return;
                }

                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Crosses,
                    GeometryRoundTripSupport.Wkt("LINESTRING(0 2, 2 0)")).Should().Be(1, "the lines cross");
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Crosses,
                    GeometryRoundTripSupport.Wkt("LINESTRING(3 3, 5 5)")).Should().Be(0, "the lines do not cross");
            }
            finally
            {
                DropTable(connection);
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void WithinDistance(string connectionName)
        {
            var connection = Prepare(connectionName);
            try
            {
                var table = AllEntities.Inst[typeof(GeoOps)].TableDescriptor;
                var shape = GeometryRoundTripSupport.ColumnByName(table, "shape");
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 3)"));

                // distance 0 (identical point) is within any threshold on every engine, planar or geodetic;
                // a distinct point is beyond a tiny threshold whether the units are degrees or metres.
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.DWithin,
                    GeometryRoundTripSupport.Wkt("POINT(3 3)"), distance: 0.5).Should().Be(1);
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.DWithin,
                    GeometryRoundTripSupport.Wkt("POINT(10 10)"), distance: 0.5).Should().Be(0);
            }
            finally
            {
                DropTable(connection);
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void Accessors(string connectionName)
        {
            var connection = Prepare(connectionName);
            try
            {
                var table = AllEntities.Inst[typeof(GeoOps)].TableDescriptor;
                var shape = GeometryRoundTripSupport.ColumnByName(table, "shape");
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt("POINT(3 4)"));

                // X / Y carry portable values on a Cartesian (SRID 0) geometry - no lat/long axis swap.
                CountScalar(connection, table, shape, SqlGeoFunctionId.X, CmpOp.Gt, 2).Should().Be(1, "X = 3 > 2");
                CountScalar(connection, table, shape, SqlGeoFunctionId.X, CmpOp.Gt, 5).Should().Be(0, "X = 3 is not > 5");
                CountScalar(connection, table, shape, SqlGeoFunctionId.Y, CmpOp.Gt, 3).Should().Be(1, "Y = 4 > 3");
                CountScalar(connection, table, shape, SqlGeoFunctionId.Y, CmpOp.Gt, 5).Should().Be(0, "Y = 4 is not > 5");

                // GeometryType / Envelope produce engine-specific values; assert only that the SQL is valid
                // on the engine and yields a non-null result for the stored row.
                SmokeNotNull(connection, table, shape, SqlGeoFunctionId.GeometryType).Should().Be(1);
                SmokeNotNull(connection, table, shape, SqlGeoFunctionId.Envelope).Should().Be(1);

                if (IsOracle(connectionName))
                {
                    // Oracle Locator has no clean IsEmpty (decision / Increment 1); SDO_SRID is null here.
                    ((Action)(() => SmokeNotNull(connection, table, shape, SqlGeoFunctionId.IsEmpty)))
                        .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
                }
                else
                {
                    SmokeNotNull(connection, table, shape, SqlGeoFunctionId.IsEmpty).Should().Be(1);
                    SmokeNotNull(connection, table, shape, SqlGeoFunctionId.Srid).Should().Be(1, "SRID 0 is not null");
                }
            }
            finally
            {
                DropTable(connection);
            }
        }

        private static int CountScalar(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape,
            SqlGeoFunctionId op, CmpOp cmp, double value)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(AggFn.Count);
            select.Where.GeoScalar(op, shape).Is(cmp).Value(value);
            using var query = connection.GetQuery(select);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<int>(0) : 0;
        }

        private static int SmokeNotNull(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape,
            SqlGeoFunctionId op)
        {
            var select = connection.GetSelectQueryBuilder(table);
            select.AddToResultset(AggFn.Count);
            select.Where.GeoScalar(op, shape).NotNull();
            using var query = connection.GetQuery(select);
            query.ExecuteReader();
            return query.ReadNext() ? query.GetValue<int>(0) : 0;
        }
    }
}
