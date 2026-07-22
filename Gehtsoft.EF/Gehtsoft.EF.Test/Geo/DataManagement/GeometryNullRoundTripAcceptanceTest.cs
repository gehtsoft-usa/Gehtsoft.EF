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
    /// Acceptance-tier NULL-geometry round-trip through the pure-SQL builder surface against every configured
    /// live server engine (MSSQL, Oracle, PostGIS, MariaDB, MySQL 8): create a geometry table via the shipping
    /// <see cref="CatalogEntityController"/>, INSERT a NULL geometry (the <c>FromWkb</c> value-wrap is still
    /// emitted, its parameter bound to SQL NULL) and SELECT it back through the WKB output-wrap, expecting
    /// <c>null</c>. Because the SQL text is fixed at <c>PrepareQuery</c> before the value binds, the dialect's
    /// geometry constructor is always invoked on a NULL argument, so this verifies
    /// <c>FromWkb(NULL) = NULL</c> / <c>AsBinary(NULL) = NULL</c> per driver. SQLite/SpatiaLite is covered by
    /// its own dedicated test. The geometry column is nullable, 2-D (MySQL is 2-D only) and has no spatial index.
    /// </summary>
    public class GeometryNullRoundTripAcceptanceTest : IClassFixture<GeometryNullRoundTripAcceptanceTest.Fixture>
    {
        private static readonly Assembly Asm = typeof(GeometryNullRoundTripAcceptanceTest).Assembly;

        public class Fixture : SqlConnectionFixtureBase
        {
        }

        private readonly Fixture mFixture;

        public GeometryNullRoundTripAcceptanceTest(Fixture fixture)
        {
            mFixture = fixture;
        }

        public static TheoryData<string> Engines() => SqlConnectionSources.SqlConnectionNames("-sqlite");

        [Entity(Scope = "geonull_acc", Table = "geo_null_acc")]
        public class GeoNullAcc
        {
            [EntityProperty(Field = "id", AutoId = true)]
            public int ID { get; set; }

            [GeometryEntityProperty(Field = "shape", Subtype = GeometrySubtype.Point, Nullable = true)]
            public byte[] Shape { get; set; }
        }

        private static void DropTable(SqlDbConnection connection)
        {
            if (connection.DoesObjectExist("geo_null_acc", null, "table"))
                using (var q = connection.GetDropEntityQuery<GeoNullAcc>())
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
        public void InsertNull_ReadsBackNull(string connectionName)
        {
            var connection = mFixture.GetInstance(connectionName);
            SkipIfGeometryUnsupported(connection, connectionName);
            DropTable(connection);
            CatalogTestSupport.ResetCatalog(connection, Asm);
            try
            {
                new CatalogEntityController(typeof(GeoNullAcc), "geonull_acc").CreateTables(connection, "1.0.0");

                TableDescriptor table = AllEntities.Inst[typeof(GeoNullAcc)].TableDescriptor;
                TableDescriptor.ColumnInfo shape = GeometryRoundTripSupport.ColumnByName(table, "shape");

                // Write a NULL geometry: the dialect FromWkb value-wrap is still emitted, its parameter bound to NULL.
                GeometryRoundTripSupport.InsertShape(connection, table, shape, null);

                // Read it back through the WKB output-wrap: expect null on every engine.
                Geometry readBack = GeometryRoundTripSupport.SelectShape(connection, table, shape);
                readBack.Should().BeNull();
            }
            finally
            {
                DropTable(connection);
            }
        }
    }
}
