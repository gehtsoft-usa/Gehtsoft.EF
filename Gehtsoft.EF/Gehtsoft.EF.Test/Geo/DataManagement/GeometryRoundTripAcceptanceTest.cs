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

namespace Gehtsoft.EF.Test.Geo.DataManagement
{
    /// <summary>
    /// Acceptance-tier geometry value round-trip through the pure-SQL builder surface against every
    /// configured live server engine (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8): create a geometry table
    /// via the shipping <see cref="CatalogEntityController"/>, INSERT a point (WKB parameter wrapped in the
    /// dialect constructor), SELECT it back (column wrapped in the WKB output function), UPDATE and reselect.
    /// The geometry is 2-D (MySQL is 2-D only) and the column has no spatial index (a value round-trip needs
    /// none). SQLite/SpatiaLite is covered by its own dedicated test.
    /// </summary>
    public class GeometryRoundTripAcceptanceTest : IClassFixture<GeometryRoundTripAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryRoundTripAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryRoundTripAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geort_acc", Table = "geo_rt_acc")]
        public class GeoRtAcc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            public byte[] Shape { get; set; }
        }

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_rt_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoRtAcc>())
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
        public void InsertSelectUpdate_RoundTripsGeometry(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoRtAcc), "geort_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoRtAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                GeometryRoundTripSupport.InsertShape(connection, table, shape, new Point(1.5, 2.5) { SRID = 4326 });

                Geometry readBack = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                readBack.Should().BeOfType<Point>();
                ((Point)readBack).X.Should().Be(1.5);
                ((Point)readBack).Y.Should().Be(2.5);

                GeometryRoundTripSupport.UpdateShape(connection, table, shape, new Point(-71.0, 42.0) { SRID = 4326 });

                Geometry afterUpdate = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                ((Point)afterUpdate).X.Should().Be(-71.0);
                ((Point)afterUpdate).Y.Should().Be(42.0);
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}
