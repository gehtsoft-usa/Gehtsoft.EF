using System;
using System.Reflection;
using AwesomeAssertions;
using Gehtsoft.EF.Db.SqlDb;
using Gehtsoft.EF.Db.SqlDb.EntityQueries;
using Gehtsoft.EF.Db.SqlDb.EntityQueries.Catalog;
using Gehtsoft.EF.Entities;
using Gehtsoft.EF.Entities.Geometry;
using Gehtsoft.EF.Test.Catalog;
using Gehtsoft.EF.Test.Utils;
using Xunit;

namespace Gehtsoft.EF.Test.Geo.TableManagement
{
    /// <summary>
    /// Behavioural geometry DDL against the live server engines (MSSQL, MySQL, Oracle, PostGIS) through the
    /// shipping <see cref="CatalogEntityController"/> path: create a table with a geometry column + spatial
    /// index, and add a geometry column to an existing table on update. The declared spatial index carries a
    /// bounding box (SQL Server and Oracle require one) and the geometry is 2-D (MySQL is 2-D only). SQLite is
    /// excluded — SpatiaLite behaviour has its own dedicated tests. A test skips implicitly (fails informatively)
    /// if the engine lacks its spatial option (PostGIS extension / Oracle Locator).
    /// </summary>
    public class GeometryEngineAcceptanceTest : IClassFixture<GeometryEngineAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryEngineAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryEngineAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        // before: no geometry column
        [Entity(Scope = "geoacc_v1", Table = "geo_acc")]
        public class GeoAccV1
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }
        }

        // after: geometry column with a bounding-boxed spatial index (2-D, so every engine accepts it)
        [Entity(Scope = "geoacc_v2", Table = "geo_acc")]
        public class GeoAccV2
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point)]
            [SpatialIndex(-180, -90, 180, 90)]
            public byte[] Shape { get; set; }
        }

        private static void DropGeoAcc(SqlDbConnection connection)
        {
            // Drop through the V2 descriptor (the one carrying the geometry column) so the Oracle drop path
            // clears USER_SDO_GEOM_METADATA for that column - otherwise the row lingers and blocks recreate.
            if (connection.DoesObjectExist("geo_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoAccV2>())
                    q.Execute();
        }

        // Skips a PostgreSQL database that has no PostGIS extension (geometry type absent). MySQL and MariaDB
        // are both supported (the driver selects the dialect from the server version), so neither is skipped.
        private static void SkipIfGeometryUnsupported(SqlDbConnection connection, string connectionName)
        {
            string driver = AppConfiguration.Instance.GetSqlConnection(connectionName).Driver;
            if (driver == "npgsql" &&
                ScalarString(connection, "SELECT COALESCE((SELECT 'yes' FROM pg_type WHERE typname = 'geometry' LIMIT 1), 'no')") != "yes")
                Assert.Skip("PostGIS is not installed in this database (run 'CREATE EXTENSION postgis;').");
        }

        private static string ScalarString(SqlDbConnection connection, string sql)
        {
            using (var q = connection.GetQuery(sql, true))
            {
                q.ExecuteReader();
                return q.ReadNext() ? q.GetValue<object>(0)?.ToString() : null;
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void Create_GeometryTableWithSpatialIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropGeoAcc(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoAccV2), "geoacc_v2").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("geo_acc", null, "table").Should().BeTrue("the table was created");
                connection.DoesObjectExist("geo_acc", "shape", "column").Should().BeTrue("the geometry column was created");
            }
            finally
            {
                DropGeoAcc(connection);
            }
        }

        [Theory]
        [MemberData(nameof(Engines))]
        public void Update_AddsGeometryColumnWithSpatialIndex(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropGeoAcc(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                // before: table with no geometry column
                new CatalogEntityController(typeof(GeoAccV1), "geoacc_v1").CreateTables(connection, "1.0.0");
                connection.DoesObjectExist("geo_acc", "shape", "column").Should().BeFalse("no geometry column yet");

                // migrate to the model that declares the geometry column + spatial index
                CatalogTestSupport.Seed(connection, "geoacc_v2", "geo_acc", typeof(GeoAccV1), "1.0.0");
                new CatalogEntityController(typeof(GeoAccV2), "geoacc_v2").UpdateTables(connection, "2.0.0", EntityUpdateMode.Update);

                connection.DoesObjectExist("geo_acc", "shape", "column").Should().BeTrue("the geometry column was added on update");
            }
            finally
            {
                DropGeoAcc(connection);
            }
        }
    }
}
