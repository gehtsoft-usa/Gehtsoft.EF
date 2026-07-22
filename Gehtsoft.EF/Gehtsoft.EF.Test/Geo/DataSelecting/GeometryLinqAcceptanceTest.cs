using System;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Linq;
using Gehtsoft.EF.Db.SqlDb.QueryBuilder;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Acceptance-tier geometry LINQ queries against every configured live server engine (MSSQL, Oracle,
    /// PostGIS, MariaDB, MySQL 8): a topological predicate (<c>SqlSpatial.Intersects</c>) and a geometry
    /// scalar comparison (<c>SqlSpatial.Area(s.Shape) &gt; 10</c>) in a LINQ <c>Where</c> over the SRID-0
    /// planar technique, plus Oracle's <c>Crosses</c> throwing <see cref="EfExceptionCode.FeatureNotSupported"/>
    /// when the query compiles. Data is loaded through the entity insert path; results are read via LINQ.
    /// SQLite/SpatiaLite is covered by the LINQ playground.
    /// </summary>
    public class GeometryLinqAcceptanceTest : IClassFixture<GeometryLinqAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryLinqAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryLinqAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geolinq_acc", Table = "geo_linq_acc")]
        public class GeoLinqAcc
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
            if (connection.DoesObjectExist("geo_linq_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoLinqAcc>())
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

        private static void Insert(SqlDbConnection connection, TableDescriptor table, TableDescriptor.ColumnInfo shape, string wkt)
            => GeometryRoundTripSupport.InsertShape(connection, table, shape, GeometryRoundTripSupport.Wkt(wkt));

        [Theory]
        [MemberData(nameof(Engines))]
        public void LinqSpatialWhere_Predicate_Scalar_Crosses(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoLinqAcc), "geolinq_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoLinqAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                Insert(connection, table, shape, GeometryRoundTripSupport.Box(0, 0, 2, 2));    // area 4
                Insert(connection, table, shape, GeometryRoundTripSupport.Box(0, 0, 4, 4));    // area 16
                Insert(connection, table, shape, GeometryRoundTripSupport.Box(10, 10, 12, 12)); // area 4, far away

                byte[] probe = GeometryRoundTripSupport.ToWkb(GeometryRoundTripSupport.Wkt(GeometryRoundTripSupport.Box(1, 1, 1.5, 1.5)));

                // Intersects a probe box inside the two origin boxes, disjoint from the far one -> 2
                var intersecting = connection.GetCollectionOf<GeoLinqAcc>()
                    .Where(s => SqlSpatial.Intersects(s.Shape, probe))
                    .Select(s => s.ID)
                    .ToList();
                intersecting.Should().HaveCount(2);

                // Area > 10 -> only the 4x4 box
                var big = connection.GetCollectionOf<GeoLinqAcc>()
                    .Where(s => SqlSpatial.Area(s.Shape) > 10.0)
                    .Select(s => s.ID)
                    .ToList();
                big.Should().HaveCount(1);

                byte[] crossLine = GeometryRoundTripSupport.ToWkb(GeometryRoundTripSupport.Wkt("LINESTRING(0 2, 2 0)"));
                if (IsOracle(connectionName))
                {
                    ((Action)(() => connection.GetCollectionOf<GeoLinqAcc>()
                        .Where(s => SqlSpatial.Crosses(s.Shape, crossLine))
                        .Select(s => s.ID)
                        .ToList()))
                        .Should().Throw<EfSqlException>().Which.ErrorCode.Should().Be(EfExceptionCode.FeatureNotSupported);
                }
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}
