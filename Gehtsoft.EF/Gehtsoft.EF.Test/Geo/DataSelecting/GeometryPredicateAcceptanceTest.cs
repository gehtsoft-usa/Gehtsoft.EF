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
using NetTopologySuite.Geometries;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.DataSelecting
{
    /// <summary>
    /// Acceptance-tier spatial WHERE + mass delete through the pure-SQL builder surface against every
    /// configured live server engine (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8). Uses only the topological
    /// <c>Intersects</c> predicate, whose truth is SRID-independent (unlike distance, which is planar on
    /// some engines and geodetic/metres on others) so the expected result is identical everywhere.
    /// SQLite/SpatiaLite is covered by its own dedicated test.
    /// </summary>
    public class GeometryPredicateAcceptanceTest : IClassFixture<GeometryPredicateAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryPredicateAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryPredicateAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geopred_acc", Table = "geo_pred_acc")]
        public class GeoPredAcc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_pred_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoPredAcc>())
                    q.Execute();
        }

        private static void SkipIfGeometryUnsupported(SqlDbConnection connection, string connectionName)
        {
            string driver = AppConfiguration.Instance.GetSqlConnection(connectionName).Driver;
            if (driver == "npgsql")
            {
                using var q = connection.GetQuery(
                    "SELECT COALESCE((SELECT 'yes' FROM pg_type WHERE typname = 'geometry' LIMIT 1), 'no')", true);
                q.ExecuteReader();
                string present = q.ReadNext() ? q.GetValue<object>(0)?.ToString() : null;
                if (present != "yes")
                    Assert.Skip("PostGIS is not installed in this database (run 'CREATE EXTENSION postgis;').");
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void SpatialWhere_And_MassDelete(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoPredAcc), "geopred_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoPredAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // valid latitude/longitude ranges - MySQL 8 enforces them for SRID 4326 (the other engines
                // treat 4326 planar); the three points only need to be distinct for the equality-intersect
                var a = new Point(0, 0) { SRID = 4326 };
                var b = new Point(10, 20) { SRID = 4326 };
                var c = new Point(30, 40) { SRID = 4326 };
                GeometryRoundTripSupport.InsertShape(connection, table, shape, a);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, b);
                GeometryRoundTripSupport.InsertShape(connection, table, shape, c);

                // a point intersects another only when equal -> exactly row B
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b)
                    .Should().Be(1);

                // mass-delete the row intersecting B -> A and C remain
                GeometryRoundTripSupport.DeleteWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b);
                GeometryRoundTripSupport.CountAll(connection, table).Should().Be(2);
                GeometryRoundTripSupport.CountWhere(connection, table, shape, SqlGeoPredicateId.Intersects, b)
                    .Should().Be(0);
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}
