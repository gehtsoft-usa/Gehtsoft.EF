using System;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Geo.NetTopologySuite;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Acceptance-tier entity-level spatial WHERE against every configured live server engine (MSSQL, Oracle,
    /// PostGIS, MariaDB, MySQL 8): a topological <c>GeoPredicateOf(Intersects)</c> and a
    /// <c>GeoScalarOf(Area).Gt(...)</c> filter over the SRID-0 planar technique (generic geometry, so every
    /// engine computes identical planar values), plus Oracle's <c>Crosses</c> throwing
    /// <see cref="EfExceptionCode.FeatureNotSupported"/>. Filtering is asserted via COUNT queries (the
    /// whole-entity geometry read is Area 3 / Increment 5). The operand geometry is a NetTopologySuite object
    /// encoded by the NTS module. SQLite/SpatiaLite has its own dedicated test.
    /// </summary>
    public class GeometryEntityPredicateAcceptanceTest : IClassFixture<GeometryEntityPredicateAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryEntityPredicateAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryEntityPredicateAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geoentpred_acc", Table = "geo_ent_pred_acc")]
        public class GeoEntPredAcc
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
            if (connection.DoesObjectExist("geo_ent_pred_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoEntPredAcc>())
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

        [Theory]
        [MemberData(nameof(Engines))]
        public void EntitySpatialWhere_Predicate_Scalar_Crosses(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoEntPredAcc), "geoentpred_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoEntPredAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // three planar polygons (SRID 0): p1 area 4, p2 area 16 (both around the origin), p3 area 4 far away
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 2, 2)));
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(0, 0, 4, 4)));
                GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(10, 10, 12, 12)));

                // Intersects a small probe box inside p1 and p2, disjoint from p3 -> 2
                var probe = GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(1, 1, 1.5, 1.5));
                CountPredicate(connection, SqlGeoPredicateId.Intersects, probe).Should().Be(2);

                // Area > 10 -> only p2 (area 16)
                using (var q = connection.GetSelectEntitiesCountQuery<GeoEntPredAcc>())
                {
                    q.Where.GeoScalarOf<GeoEntPredAcc>("Shape", SqlGeoFunctionId.Area).Gt(10.0);
                    q.RowCount.Should().Be(1);
                }

                // Oracle Locator cannot express OGC Crosses (decision 11) - it throws at render time.
                var crossLine = GeometryRoundTripSupport.Wkt("LINESTRING(0 2, 2 0)");
                if (IsOracle(connectionName))
                {
                    ((Action)(() =>
                    {
                        using var q = connection.GetSelectEntitiesCountQuery<GeoEntPredAcc>();
                        q.Where.GeoPredicateOf<GeoEntPredAcc>("Shape", SqlGeoPredicateId.Crosses, crossLine);
                    })).Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
                }
            }
            finally
            {
                DropTable(connection);
            }
        }

        private static int CountPredicate(SqlDbConnection connection, SqlGeoPredicateId op, NetTopologySuite.Geometries.Geometry operand)
        {
            using var q = connection.GetSelectEntitiesCountQuery<GeoEntPredAcc>();
            q.Where.GeoPredicateOf<GeoEntPredAcc>("Shape", op, operand);
            return q.RowCount;
        }
    }
}
